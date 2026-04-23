using System.Text.Json;
using System.Text.RegularExpressions;
using APIHU.Application.DTOs;
using APIHU.Application.Interfaces;
using APIHU.Domain.Interfaces;

namespace APIHU.Application.Services;

/// <summary>
/// Servicio que ejecuta el pipeline de procesamiento de texto
/// </summary>
public class PipelineProcesamientoService : IPipelineProcesamientoService
{
    private readonly IAIProviderService _aiProvider;
    private readonly IPromptService _promptService;
    private readonly ILogger<PipelineProcesamientoService> _logger;

    public PipelineProcesamientoService(
        IAIProviderService aiProvider,
        IPromptService promptService,
        ILogger<PipelineProcesamientoService> logger)
    {
        _aiProvider = aiProvider;
        _promptService = promptService;
        _logger = logger;
    }

    public async Task<ResultadoPipeline> EjecutarPipelineAsync(
        GenerarHURequest request, 
        CancellationToken cancellationToken = default)
    {
        var inicio = DateTime.UtcNow;
        var resultado = new ResultadoPipeline();

        try
        {
            _logger.LogInformation("Iniciando pipeline de procesamiento para texto de {Longitud} caracteres", 
                request.Texto.Length);

            // ============================================
            // ETAPA 1: Limpieza de texto
            // ============================================
            _logger.LogInformation("Ejecutando etapa 1: Limpieza de texto");
            var version = request.VersionPrompt ?? "v1";
            
            var promptLimpieza = _promptService.ObtenerPromptLimpieza(version)
                .Replace("{texto}", request.Texto);

            var respuestaLimpieza = await _aiProvider.EnviarPromptAsync(promptLimpieza, cancellationToken);
            resultado.Limpieza = ParsearResultadoLimpieza(respuestaLimpieza);

            if (!resultado.Limpieza.Exitoso)
            {
                resultado.Exitoso = false;
                resultado.Error = resultado.Limpieza.Error;
                return resultado;
            }

            _logger.LogInformation("Limpieza completada: {Caracteres} caracteres resultantes", 
                resultado.Limpieza.TextoLimpio.Length);

            // ============================================
            // ETAPA 2: Estructuración
            // ============================================
            _logger.LogInformation("Ejecutando etapa 2: Estructuración");
            
            var promptEstructuracion = _promptService.ObtenerPromptEstructuracion(version)
                .Replace("{texto}", resultado.Limpieza.TextoLimpio);

            var respuestaEstructuracion = await _aiProvider.EnviarPromptAsync(promptEstructuracion, cancellationToken);
            resultado.Estructuracion = ParsearResultadoEstructuracion(respuestaEstructuracion);

            if (!resultado.Estructuracion.Exitoso)
            {
                resultado.Exitoso = false;
                resultado.Error = resultado.Estructuracion.Error;
                return resultado;
            }

            _logger.LogInformation("Estructuración completada: {Cantidad} requerimientos identificados", 
                resultado.Estructuracion.Requerimientos.Count);

            // ============================================
            // ETAPA 3: Generación de HUs
            // ============================================
            _logger.LogInformation("Ejecutando etapa 3: Generación de HUs");
            
            var maxHUs = request.MaximoHUs ?? 5;
            var idioma = request.Idioma ?? "es";

            var requerimientosJson = JsonSerializer.Serialize(resultado.Estructuracion.Requerimientos);
            
            var promptHU = _promptService.ObtenerPromptHU(version)
                .Replace("{maxHUs}", maxHUs.ToString())
                .Replace("{idioma}", idioma)
                .Replace("{requerimientos}", requerimientosJson)
                .Replace("{texto}", resultado.Limpieza.TextoLimpio);

            var respuestaHU = await _aiProvider.EnviarPromptAsync(promptHU, cancellationToken);
            resultado.HistoriasUsuario = ParsearHistoriasUsuario(respuestaHU);

            if (resultado.HistoriasUsuario.Count == 0)
            {
                resultado.Exitoso = false;
                resultado.Error = "No se pudieron generar historias de usuario";
                return resultado;
            }

            resultado.Exitoso = true;
            resultado.TiempoTotal = DateTime.UtcNow - inicio;

            _logger.LogInformation("Pipeline completado exitosamente en {Tiempo}ms. Generadas {Cantidad} HUs", 
                resultado.TiempoTotal?.TotalMilliseconds ?? 0, resultado.HistoriasUsuario.Count);

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el pipeline de procesamiento");
            resultado.Exitoso = false;
            resultado.Error = ex.Message;
            resultado.TiempoTotal = DateTime.UtcNow - inicio;
            return resultado;
        }
    }

    private ResultadoLimpieza ParsearResultadoLimpieza(string respuesta)
    {
        try
        {
            respuesta = LimpiarRespuesta(respuesta);
            var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ResultadoLimpieza>(respuesta, opciones) 
                ?? new ResultadoLimpieza { Exitoso = false, Error = "No se pudo parsear la respuesta" };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error al parsear resultado de limpieza");
            return new ResultadoLimpieza 
            { 
                Exitoso = true, 
                TextoLimpio = respuesta, // Usar texto original si falla el parseo
                ElementosEliminados = new List<string>()
            };
        }
    }

    private ResultadoEstructuracion ParsearResultadoEstructuracion(string respuesta)
    {
        try
        {
            respuesta = LimpiarRespuesta(respuesta);
            var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ResultadoEstructuracion>(respuesta, opciones)
                ?? new ResultadoEstructuracion { Exitoso = false, Error = "No se pudo parsear la respuesta" };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error al parsear resultado de estructuración");
            return new ResultadoEstructuracion
            {
                Exitoso = false,
                Error = $"Error al parsear estructuración: {ex.Message}"
            };
        }
    }

    private List<HistoriaUsuarioResponse> ParsearHistoriasUsuario(string respuesta)
    {
        try
        {
            respuesta = LimpiarRespuesta(respuesta);
            var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            var wrapper = JsonSerializer.Deserialize<HistoriaUsuarioWrapper>(respuesta, opciones);
            return wrapper?.HistoriasUsuario ?? new List<HistoriaUsuarioResponse>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error al parsear historias de usuario");
            return new List<HistoriaUsuarioResponse>();
        }
    }

    private string LimpiarRespuesta(string respuesta)
    {
        // Remover bloques de código markdown
        var regex = new Regex(@"```json\s*", RegexOptions.IgnoreCase);
        respuesta = regex.Replace(respuesta, "");

        regex = new Regex(@"```\s*$", RegexOptions.Multiline);
        respuesta = regex.Replace(respuesta, "");

        return respuesta.Trim();
    }
}