using System.Diagnostics;
using APIHU.Application.DTOs;
using APIHU.Application.Interfaces;
using APIHU.Domain.Interfaces;

namespace APIHU.Application.Services;

/// <summary>
/// Orchestrator central que controla todo el flujo de procesamiento de HUs.
/// Maneja logs por cada etapa, medición de tiempo y validación final.
/// </summary>
public class HuProcessingOrchestrator : IHuProcessingOrchestrator
{
    private readonly IAIProviderService _aiProvider;
    private readonly IPromptService _promptService;
    private readonly IHuValidatorService _validator;
    private readonly ILogger<HuProcessingOrchestrator> _logger;

    public HuProcessingOrchestrator(
        IAIProviderService aiProvider,
        IPromptService promptService,
        IHuValidatorService validator,
        ILogger<HuProcessingOrchestrator> logger)
    {
        _aiProvider = aiProvider;
        _promptService = promptService;
        _validator = validator;
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

            var version = request.VersionPrompt ?? "v2";
            var contexto = string.IsNullOrWhiteSpace(request.Contexto) ? "(no se proporcionó contexto adicional)" : request.Contexto;

            var promptLimpieza = _promptService.ObtenerPromptLimpieza(version)
                .Replace("{texto}", request.Texto ?? string.Empty)
                .Replace("{contexto}", contexto);

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
                .Replace("{texto}", resultado.Limpieza.TextoLimpio ?? string.Empty)
                .Replace("{contexto}", contexto);

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

            // Si el usuario no pasa maximoHUs, el modelo decide libremente.
            // Si lo pasa, se respeta como techo duro.
            var maxHUsInstruccion = request.MaximoHUs.HasValue
                ? $"un MÁXIMO de {request.MaximoHUs.Value} HU(s)"
                : "tantas HUs como el texto justifique (sin límite superior)";

            // Compat con prompts v1 que usan {maxHUs} como número entero.
            var maxHUsLegacy = request.MaximoHUs?.ToString() ?? "10";

            var idioma = request.Idioma ?? "es";

            var requerimientosJson = System.Text.Json.JsonSerializer.Serialize(resultado.Estructuracion.Requerimientos ?? new List<Requerimiento>());

            var promptHU = _promptService.ObtenerPromptHU(version)
                .Replace("{maxHUsInstruccion}", maxHUsInstruccion)
                .Replace("{maxHUs}", maxHUsLegacy)
                .Replace("{idioma}", idioma)
                .Replace("{requerimientos}", requerimientosJson)
                .Replace("{texto}", resultado.Limpieza.TextoLimpio ?? string.Empty)
                .Replace("{contexto}", contexto);

            var respuestaHU = await _aiProvider.EnviarPromptAsync(promptHU, cancellationToken);
            AcumularTokens(resultado, _aiProvider.UltimoUso);
            var (historias, truncado) = ParsearHistoriasUsuario(respuestaHU);
            resultado.HistoriasUsuario = historias;
            resultado.RespuestaTruncada = truncado;

            if (truncado)
            {
                _logger.LogWarning(
                    "[{CorrelationId}] ⚠ RESPUESTA TRUNCADA: el modelo alcanzó MaxTokens. " +
                    "Se rescataron {Count} HUs completas. Considera subir Gemini__MaxTokens o dividir el texto.",
                    correlationId, historias.Count);
            }

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

    /// <summary>
    /// Parsea la respuesta del modelo en HUs. Devuelve la lista y un flag indicando
    /// si tuvo que rescatar de JSON truncado (= la respuesta del modelo excedi\u00f3 MaxTokens).
    /// </summary>
    private (List<HistoriaUsuarioResponse> historias, bool truncado) ParsearHistoriasUsuario(string respuesta)
    {
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        (List<HistoriaUsuarioResponse>, bool) FallbackTextoPlano(string texto)
        {
            _logger.LogWarning("No se pudieron rescatar HUs estructuradas. Devolviendo HU \u00fanica con texto crudo.");
            return (new List<HistoriaUsuarioResponse>
            {
                new HistoriaUsuarioResponse
                {
                    Titulo = "Historia de Usuario",
                    Descripcion = texto.Length > 1000 ? texto.Substring(0, 1000) : texto,
                    CriteriosAceptacion = new List<CriterioAceptacionResponse>
                    {
                        new CriterioAceptacionResponse { Descripcion = "Criterios definidos por el modelo", Orden = 1, EsObligatorio = true }
                    },
                    TareasTecnicas = new List<TareaTecnicaResponse>
                    {
                        new TareaTecnicaResponse { Descripcion = "Tareas definidas por el modelo", Tipo = "Desarrollo", Orden = 1 }
                    }
                }
            }, false);
        }

        try
        {
            respuesta = LimpiarRespuesta(respuesta);

            var trimmed = respuesta.Trim();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                return FallbackTextoPlano(respuesta);
            }

            // Intento 1: parseo directo
            try
            {
                var wrapper = System.Text.Json.JsonSerializer.Deserialize<HistoriaUsuarioWrapper>(respuesta, opciones);
                if (wrapper?.HistoriasUsuario != null && wrapper.HistoriasUsuario.Count > 0)
                {
                    return (wrapper.HistoriasUsuario, false);
                }
            }
            catch (System.Text.Json.JsonException jex)
            {
                _logger.LogWarning(
                    "Parse directo fall\u00f3 ({Mensaje}). Intentando rescate de JSON truncado...",
                    jex.Message);
            }

            // Intento 2: rescate de JSON truncado.
            var rescatado = IntentarRescatarHistoriasParciales(respuesta);
            if (rescatado.Count > 0)
            {
                return (rescatado, true);
            }

            return FallbackTextoPlano(respuesta);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error inesperado parseando HUs");
            return FallbackTextoPlano(respuesta);
        }
    }

    /// <summary>
    /// Cuando el JSON viene truncado a mitad del array historiasUsuario, intenta
    /// extraer los objetos completos que ya estaban serializados, recortando despu\u00e9s
    /// del \u00faltimo "}" balanceado y cerrando el array y el wrapper manualmente.
    /// </summary>
    private List<HistoriaUsuarioResponse> IntentarRescatarHistoriasParciales(string respuesta)
    {
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Localizar el inicio del array historiasUsuario
        var arrayKeyIdx = respuesta.IndexOf("\"historiasUsuario\"", StringComparison.OrdinalIgnoreCase);
        if (arrayKeyIdx < 0) return new List<HistoriaUsuarioResponse>();

        var arrayStart = respuesta.IndexOf('[', arrayKeyIdx);
        if (arrayStart < 0) return new List<HistoriaUsuarioResponse>();

        // Recorrer caracteres llevando cuenta de profundidad de objetos {} dentro del array.
        // Cada vez que profundidad vuelve a 0 (despu\u00e9s de haber subido), significa
        // que se cerr\u00f3 un objeto HU completo. Marcamos esa posici\u00f3n.
        var depth = 0;
        var insideString = false;
        var escape = false;
        var lastCompleteObjectEnd = -1;

        for (var i = arrayStart + 1; i < respuesta.Length; i++)
        {
            var c = respuesta[i];

            if (escape) { escape = false; continue; }
            if (c == '\\') { escape = true; continue; }
            if (c == '"') { insideString = !insideString; continue; }
            if (insideString) continue;

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    lastCompleteObjectEnd = i;
                }
            }
            else if (c == ']' && depth == 0)
            {
                // Array cerr\u00f3 normalmente
                lastCompleteObjectEnd = i - 1;
                break;
            }
        }

        if (lastCompleteObjectEnd < 0) return new List<HistoriaUsuarioResponse>();

        // Reconstruir JSON v\u00e1lido cortando despu\u00e9s del \u00faltimo objeto completo
        var prefijo = respuesta.Substring(0, lastCompleteObjectEnd + 1);
        var jsonRecuperado = prefijo + "]}";

        try
        {
            var wrapper = System.Text.Json.JsonSerializer.Deserialize<HistoriaUsuarioWrapper>(jsonRecuperado, opciones);
            return wrapper?.HistoriasUsuario ?? new List<HistoriaUsuarioResponse>();
        }
        catch
        {
            return new List<HistoriaUsuarioResponse>();
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
}