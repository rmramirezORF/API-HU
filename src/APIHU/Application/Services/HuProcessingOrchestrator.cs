using System.Diagnostics;
using APIHU.Application.DTOs;
using APIHU.Application.Interfaces;
using APIHU.Domain.Entities;
using APIHU.Domain.Interfaces;

namespace APIHU.Application.Services;

/// <summary>
/// Orchestrator central que controla todo el flujo de procesamiento de HUs
/// Maneja logs por cada etapa, medición de tiempo y validación final
/// </summary>
public class HuProcessingOrchestrator : IHuProcessingOrchestrator
{
    private readonly IAIProviderService _aiProvider;
    private readonly IPromptService _promptService;
    private readonly IHuValidatorService _validator;
    private readonly IGeneracionRepository _generacionRepository;
    private readonly ILogger<HuProcessingOrchestrator> _logger;

    public HuProcessingOrchestrator(
        IAIProviderService aiProvider,
        IPromptService promptService,
        IHuValidatorService validator,
        IGeneracionRepository generacionRepository,
        ILogger<HuProcessingOrchestrator> logger)
    {
        _aiProvider = aiProvider;
        _promptService = promptService;
        _validator = validator;
        _generacionRepository = generacionRepository;
        _logger = logger;
    }

    public async Task<ResultadoPipeline> EjecutarPipelineCompletoAsync(
        GenerarHURequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();
        var resultado = new ResultadoPipeline { CorrelationId = correlationId };

        _logger.LogInformation(
            "[{CorrelationId}] ════════════════════════════════════════════════════════",
            correlationId);
        _logger.LogInformation(
            "[{CorrelationId}] INICIO - Pipeline de procesamiento de HUs", correlationId);
        _logger.LogInformation(
            "[{CorrelationId}] Texto de entrada: {Caracteres} caracteres, Proyecto: {Proyecto}",
            correlationId, request.Texto?.Length ?? 0, request.Proyecto ?? "N/A");

        try
        {
            // ============================================
            // ETAPA 1: Limpieza de texto
            // ============================================
            var etapa1Stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("[{CorrelationId}] ▶ ETAPA 1: LIMPIEZA - Iniciando...", correlationId);

            var version = request.VersionPrompt ?? "v1";
            var promptLimpieza = _promptService.ObtenerPromptLimpieza(version)
                .Replace("{texto}", request.Texto ?? string.Empty);

            var respuestaLimpieza = await _aiProvider.EnviarPromptAsync(promptLimpieza, cancellationToken);
            AcumularTokens(resultado, _aiProvider.UltimoUso);
            resultado.Limpieza = ParsearResultadoLimpieza(respuestaLimpieza);

            etapa1Stopwatch.Stop();
            resultado.LimpiezaDuracionMs = etapa1Stopwatch.ElapsedMilliseconds;

            if (!resultado.Limpieza.Exitoso)
            {
                resultado.Exitoso = false;
                resultado.Error = $"Error en limpieza: {resultado.Limpieza.Error}";
                _logger.LogError(
                    "[{CorrelationId}] ✖ ETAPA 1: LIMPIEZA - Falló en {Tiempo}ms. Error: {Error}",
                    correlationId, etapa1Stopwatch.ElapsedMilliseconds, resultado.Limpieza.Error);
                return resultado;
            }

            _logger.LogInformation(
                "[{CorrelationId}] ✓ ETAPA 1: LIMPIEZA - Completada en {Tiempo}ms. Caracteres: {Caracteres}",
                correlationId, etapa1Stopwatch.ElapsedMilliseconds, resultado.Limpieza.TextoLimpio?.Length ?? 0);

            // ============================================
            // ETAPA 2: Estructuración
            // ============================================
            var etapa2Stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("[{CorrelationId}] ▶ ETAPA 2: ESTRUCTURACIÓN - Iniciando...", correlationId);

            var promptEstructuracion = _promptService.ObtenerPromptEstructuracion(version)
                .Replace("{texto}", resultado.Limpieza.TextoLimpio ?? string.Empty);

            var respuestaEstructuracion = await _aiProvider.EnviarPromptAsync(promptEstructuracion, cancellationToken);
            AcumularTokens(resultado, _aiProvider.UltimoUso);
            resultado.Estructuracion = ParsearResultadoEstructuracion(respuestaEstructuracion);

            etapa2Stopwatch.Stop();
            resultado.EstructuracionDuracionMs = etapa2Stopwatch.ElapsedMilliseconds;

            if (!resultado.Estructuracion.Exitoso)
            {
                resultado.Exitoso = false;
                resultado.Error = $"Error en estructuración: {resultado.Estructuracion.Error}";
                _logger.LogError(
                    "[{CorrelationId}] ✖ ETAPA 2: ESTRUCTURACIÓN - Falló en {Tiempo}ms. Error: {Error}",
                    correlationId, etapa2Stopwatch.ElapsedMilliseconds, resultado.Estructuracion.Error);
                return resultado;
            }

            _logger.LogInformation(
                "[{CorrelationId}] ✓ ETAPA 2: ESTRUCTURACIÓN - Completada en {Tiempo}ms. Requerimientos: {Cantidad}",
                correlationId, etapa2Stopwatch.ElapsedMilliseconds, resultado.Estructuracion.Requerimientos?.Count ?? 0);

            // ============================================
            // ETAPA 3: Generación de HUs
            // ============================================
            var etapa3Stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("[{CorrelationId}] ▶ ETAPA 3: GENERACIÓN HU - Iniciando...", correlationId);

            var maxHUs = request.MaximoHUs ?? 5;
            var idioma = request.Idioma ?? "es";

            var requerimientosJson = System.Text.Json.JsonSerializer.Serialize(resultado.Estructuracion.Requerimientos ?? new List<Requerimiento>());

            var promptHU = _promptService.ObtenerPromptHU(version)
                .Replace("{maxHUs}", maxHUs.ToString())
                .Replace("{idioma}", idioma)
                .Replace("{requerimientos}", requerimientosJson)
                .Replace("{texto}", resultado.Limpieza.TextoLimpio ?? string.Empty);

            var respuestaHU = await _aiProvider.EnviarPromptAsync(promptHU, cancellationToken);
            AcumularTokens(resultado, _aiProvider.UltimoUso);
            resultado.HistoriasUsuario = ParsearHistoriasUsuario(respuestaHU);

            etapa3Stopwatch.Stop();
            resultado.GeneracionDuracionMs = etapa3Stopwatch.ElapsedMilliseconds;

            if (resultado.HistoriasUsuario.Count == 0)
            {
                resultado.Exitoso = false;
                resultado.Error = "No se pudieron generar historias de usuario";
                _logger.LogError(
                    "[{CorrelationId}] ✖ ETAPA 3: GENERACIÓN HU - Falló en {Tiempo}ms. Sin HUs generadas",
                    correlationId, etapa3Stopwatch.ElapsedMilliseconds);
                return resultado;
            }

            _logger.LogInformation(
                "[{CorrelationId}] ✓ ETAPA 3: GENERACIÓN HU - Completada en {Tiempo}ms. HUs generadas: {Cantidad}",
                correlationId, etapa3Stopwatch.ElapsedMilliseconds, resultado.HistoriasUsuario.Count);

            // ============================================
            // VALIDACIÓN FINAL
            // ============================================
            _logger.LogInformation("[{CorrelationId}] ▶ VALIDACIÓN FINAL - Iniciando...", correlationId);

            var validationResult = await _validator.ValidarHistoriasAsync(resultado.HistoriasUsuario, cancellationToken);

            if (!validationResult.EsValido)
            {
                _logger.LogWarning(
                    "[{CorrelationId}] ⚠ VALIDACIÓN FINAL - Advertencias: {Advertencias}",
                    correlationId, string.Join(", ", validationResult.Advertencias));
                
                // Las HUs inválidas se marcan pero no fallamos el proceso
                resultado.HistoriasUsuario = validationResult.HistoriasValidas;
            }
            else
            {
                _logger.LogInformation(
                    "[{CorrelationId}] ✓ VALIDACIÓN FINAL - Todas las HUs son válidas",
                    correlationId);
            }

            // ============================================
            // RESULTADO FINAL
            // ============================================
            stopwatch.Stop();
            resultado.Exitoso = true;
            resultado.TiempoTotal = stopwatch.Elapsed;

            _logger.LogInformation(
                "[{CorrelationId}] ════════════════════════════════════════════════════════",
                correlationId);
            _logger.LogInformation(
                "[{CorrelationId}] FIN - Pipeline completado exitosamente",
                correlationId);
            _logger.LogInformation(
                "[{CorrelationId}] Resumen: Limpieza={Limpieza}ms, Estructuración={Estructuracion}ms, Generación={Generacion}ms, Total={Total}ms | Tokens in={TIn} out={TOut} total={TTot}",
                correlationId,
                resultado.LimpiezaDuracionMs,
                resultado.EstructuracionDuracionMs,
                resultado.GeneracionDuracionMs,
                stopwatch.ElapsedMilliseconds,
                resultado.TokensInput,
                resultado.TokensOutput,
                resultado.TokensTotales);
            _logger.LogInformation(
                "[{CorrelationId}] ════════════════════════════════════════════════════════",
                correlationId);

            return resultado;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            resultado.Exitoso = false;
            resultado.Error = ex.Message;
            resultado.TiempoTotal = stopwatch.Elapsed;

            _logger.LogError(ex,
                "[{CorrelationId}] ✖ ERROR EN PIPELINE - Completado con error en {Tiempo}ms. Error: {Error}",
                correlationId, stopwatch.ElapsedMilliseconds, ex.Message);

            return resultado;
        }
    }

    public async Task<GeneracionHU> GuardarGeneracionAsync(
        GenerarHURequest request,
        ResultadoPipeline resultado,
        CancellationToken cancellationToken = default)
    {
        var generacion = new GeneracionHU
        {
            TextoEntrada = request.Texto ?? string.Empty,
            TextoProcesado = resultado.Limpieza?.TextoLimpio,
            Proyecto = request.Proyecto,
            Idioma = request.Idioma ?? "es",
            TotalHUs = resultado.HistoriasUsuario?.Count ?? 0,
            Exitoso = resultado.Exitoso,
            MensajeError = resultado.Error,
            PromptVersion = request.VersionPrompt ?? "v1",
            Estado = resultado.Exitoso ? EstadoGeneracion.Completado : EstadoGeneracion.Error,
            DuracionMs = (int)(resultado.TiempoTotal?.TotalMilliseconds ?? 0),
            ModeloIA = _aiProvider.ModeloActual,
            TokensConsumidos = resultado.TokensTotales
        };

        return await _generacionRepository.AgregarAsync(generacion, cancellationToken);
    }

    private ResultadoLimpieza ParsearResultadoLimpieza(string respuesta)
    {
        // Garantiza un resultado válido independientemente de lo que responda el modelo
        ResultadoLimpieza FallbackTextoPlano(string texto, string? motivo = null)
        {
            if (!string.IsNullOrEmpty(motivo))
                _logger.LogWarning("Limpieza: {Motivo}. Usando respuesta cruda como texto limpio.", motivo);
            return new ResultadoLimpieza
            {
                Exitoso = true,
                TextoLimpio = texto,
                ElementosEliminados = new List<string>()
            };
        }

        try
        {
            respuesta = LimpiarRespuesta(respuesta);
            var trimmed = respuesta.Trim();

            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                return FallbackTextoPlano(respuesta, "respuesta no es JSON");
            }

            var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = System.Text.Json.JsonSerializer.Deserialize<ResultadoLimpieza>(respuesta, opciones);

            // Si el JSON se parseó pero con schema distinto (TextoLimpio vacío), usar la respuesta cruda
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.TextoLimpio))
            {
                return FallbackTextoPlano(respuesta, "JSON sin campo textoLimpio útil");
            }

            parsed.Exitoso = true;
            parsed.ElementosEliminados ??= new List<string>();
            return parsed;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Error al parsear resultado de limpieza");
            return FallbackTextoPlano(respuesta);
        }
    }

    private ResultadoEstructuracion ParsearResultadoEstructuracion(string respuesta)
    {
        ResultadoEstructuracion FallbackTextoPlano(string texto, string? motivo = null)
        {
            if (!string.IsNullOrEmpty(motivo))
                _logger.LogWarning("Estructuración: {Motivo}. Creando requerimiento único desde texto crudo.", motivo);
            return new ResultadoEstructuracion
            {
                Exitoso = true,
                Requerimientos = new List<Requerimiento>
                {
                    new Requerimiento
                    {
                        Nombre = "Requerimiento detectado",
                        Descripcion = texto.Length > 500 ? texto.Substring(0, 500) : texto,
                        Categoria = "General"
                    }
                }
            };
        }

        try
        {
            respuesta = LimpiarRespuesta(respuesta);
            var trimmed = respuesta.Trim();

            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                return FallbackTextoPlano(respuesta, "respuesta no es JSON");
            }

            var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = System.Text.Json.JsonSerializer.Deserialize<ResultadoEstructuracion>(respuesta, opciones);

            if (parsed == null || parsed.Requerimientos == null || parsed.Requerimientos.Count == 0)
            {
                return FallbackTextoPlano(respuesta, "JSON sin requerimientos útiles");
            }

            parsed.Exitoso = true;
            return parsed;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Error al parsear resultado de estructuración");
            return FallbackTextoPlano(respuesta);
        }
    }

    private List<HistoriaUsuarioResponse> ParsearHistoriasUsuario(string respuesta)
    {
        try
        {
            respuesta = LimpiarRespuesta(respuesta);
            
            // Si la respuesta no empieza con { o [, es texto plano - crear HU desde texto
            var trimmed = respuesta.Trim();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                _logger.LogWarning("La respuesta no es JSON válido, creando HU desde texto plano");
                return new List<HistoriaUsuarioResponse>
                {
                    new HistoriaUsuarioResponse
                    {
                        Titulo = "Historia de Usuario",
                        Descripcion = respuesta.Length > 1000 ? respuesta.Substring(0, 1000) : respuesta,
                        CriteriosAceptacion = new List<CriterioAceptacionResponse>
                        {
                            new CriterioAceptacionResponse { Descripcion = "Criterios definidos por el modelo", Orden = 1, EsObligatorio = true }
                        },
                        TareasTecnicas = new List<TareaTecnicaResponse>
                        {
                            new TareaTecnicaResponse { Descripcion = "Tareas definidas por el modelo", Tipo = "Desarrollo", Orden = 1 }
                        }
                    }
                };
            }
            
            var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            var wrapper = System.Text.Json.JsonSerializer.Deserialize<HistoriaUsuarioWrapper>(respuesta, opciones);
            return wrapper?.HistoriasUsuario ?? new List<HistoriaUsuarioResponse>();
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Error al parsear historias de usuario, creando desde texto plano");
            return new List<HistoriaUsuarioResponse>
            {
                new HistoriaUsuarioResponse
                {
                    Titulo = "Historia de Usuario",
                    Descripcion = respuesta.Length > 1000 ? respuesta.Substring(0, 1000) : respuesta,
                    CriteriosAceptacion = new List<CriterioAceptacionResponse>
                    {
                        new CriterioAceptacionResponse { Descripcion = "Criterios definidos por el modelo", Orden = 1, EsObligatorio = true }
                    },
                    TareasTecnicas = new List<TareaTecnicaResponse>
                    {
                        new TareaTecnicaResponse { Descripcion = "Tareas definidas por el modelo", Tipo = "Desarrollo", Orden = 1 }
                    }
                }
            };
        }
    }

    private static void AcumularTokens(ResultadoPipeline resultado, UsoTokens uso)
    {
        resultado.TokensInput += uso.InputTokens;
        resultado.TokensOutput += uso.OutputTokens;
    }

    private string LimpiarRespuesta(string respuesta)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"```json\s*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        respuesta = regex.Replace(respuesta, "");

        regex = new System.Text.RegularExpressions.Regex(@"```\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
        respuesta = regex.Replace(respuesta, "");

        return respuesta.Trim();
    }
}

internal class HistoriaUsuarioWrapper
{
    public List<HistoriaUsuarioResponse>? HistoriasUsuario { get; set; }
}

/// <summary>
/// Interfaz para el Orchestrator de procesamiento
/// </summary>
public interface IHuProcessingOrchestrator
{
    /// <summary>
    /// Ejecuta el pipeline completo con logging y medición de tiempo
    /// </summary>
    Task<ResultadoPipeline> EjecutarPipelineCompletoAsync(
        GenerarHURequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda la generación en la base de datos
    /// </summary>
    Task<GeneracionHU> GuardarGeneracionAsync(
        GenerarHURequest request,
        ResultadoPipeline resultado,
        CancellationToken cancellationToken = default);
}