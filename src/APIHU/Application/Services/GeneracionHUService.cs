using APIHU.Application.DTOs;
using APIHU.Application.Interfaces;
using APIHU.Domain.Interfaces;

namespace APIHU.Application.Services;

/// <summary>
/// Servicio principal de generación de Historias de Usuario.
/// Es una fachada delgada sobre el orchestrator que mapea ResultadoPipeline a GenerarHUResponse.
/// </summary>
public class GeneracionHUService : IGeneracionHUService
{
    private readonly IHuProcessingOrchestrator _orchestrator;
    private readonly IAIProviderService _aiProvider;
    private readonly ILogger<GeneracionHUService> _logger;

    public GeneracionHUService(
        IHuProcessingOrchestrator orchestrator,
        IAIProviderService aiProvider,
        ILogger<GeneracionHUService> logger)
    {
        _orchestrator = orchestrator;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task<GenerarHUResponse> GenerarHUsAsync(
        GenerarHURequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Iniciando generación de HUs para proyecto: {Proyecto}",
                request.Proyecto ?? "Sin proyecto");

            var resultado = await _orchestrator.EjecutarPipelineCompletoAsync(request, cancellationToken);

            if (!resultado.Exitoso)
            {
                return new GenerarHUResponse
                {
                    Exitoso = false,
                    Mensaje = resultado.Error ?? "Error desconocido en el pipeline",
                    HistoriasUsuario = new List<HistoriaUsuarioResponse>(),
                    CorrelationId = resultado.CorrelationId
                };
            }

            var totalHUs = resultado.HistoriasUsuario?.Count ?? 0;
            var mensaje = resultado.RespuestaTruncada
                ? $"Se generaron {totalHUs} HUs (la respuesta del modelo se truncó: probablemente hay más HUs que no se pudieron generar — sube MaxTokens o divide el texto)"
                : $"Se generaron {totalHUs} historias de usuario exitosamente";

            return new GenerarHUResponse
            {
                Exitoso = true,
                Mensaje = mensaje,
                TextoLimpio = resultado.Limpieza?.TextoLimpio,
                HistoriasUsuario = resultado.HistoriasUsuario ?? new List<HistoriaUsuarioResponse>(),
                Metadata = new GenerarHUMetadata
                {
                    FechaGeneracion = DateTime.UtcNow,
                    Proyecto = request.Proyecto,
                    TotalHUs = totalHUs,
                    Idioma = request.Idioma ?? "es",
                    VersionPrompt = request.VersionPrompt ?? "v2",
                    DuracionMs = (int)(resultado.TiempoTotal?.TotalMilliseconds ?? 0),
                    RespuestaTruncada = resultado.RespuestaTruncada,
                    AdvertenciaTruncamiento = resultado.RespuestaTruncada
                        ? "El modelo alcanzó MaxTokens. Se rescataron las HUs completas. Si tu texto es muy largo, considera dividirlo o subir Gemini__MaxTokens en .env."
                        : null
                },
                CorrelationId = resultado.CorrelationId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar HUs");
            return new GenerarHUResponse
            {
                Exitoso = false,
                Mensaje = $"Error al generar historias de usuario: {ex.Message}",
                HistoriasUsuario = new List<HistoriaUsuarioResponse>()
            };
        }
    }
}
