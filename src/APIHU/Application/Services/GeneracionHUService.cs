using APIHU.Application.DTOs;
using APIHU.Application.Interfaces;
using APIHU.Domain.Entities;
using APIHU.Domain.Interfaces;

namespace APIHU.Application.Services;

/// <summary>
/// Servicio principal de generación de Historias de Usuario
/// </summary>
public class GeneracionHUService : IGeneracionHUService
{
    private readonly IHuProcessingOrchestrator _orchestrator;
    private readonly IGeneracionRepository _generacionRepository;
    private readonly IHistoriaUsuarioRepository _huRepository;
    private readonly IAIProviderService _aiProvider;
    private readonly ILogger<GeneracionHUService> _logger;

    public GeneracionHUService(
        IHuProcessingOrchestrator orchestrator,
        IGeneracionRepository generacionRepository,
        IHistoriaUsuarioRepository huRepository,
        IAIProviderService aiProvider,
        ILogger<GeneracionHUService> logger)
    {
        _orchestrator = orchestrator;
        _generacionRepository = generacionRepository;
        _huRepository = huRepository;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task<GenerarHUResponse> GenerarHUsAsync(
        GenerarHURequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Iniciando generación de HUs para proyecto: {Proyecto}", request.Proyecto ?? "Sin proyecto");

            // Usar el Orchestrator para mejor logging y tracking
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

            return new GenerarHUResponse
            {
                Exitoso = true,
                Mensaje = $"Se generaron {resultado.HistoriasUsuario?.Count ?? 0} historias de usuario exitosamente",
                TextoLimpio = resultado.Limpieza?.TextoLimpio,
                HistoriasUsuario = resultado.HistoriasUsuario ?? new List<HistoriaUsuarioResponse>(),
                Metadata = new GenerarHUMetadata
                {
                    FechaGeneracion = DateTime.UtcNow,
                    Proyecto = request.Proyecto,
                    TotalHUs = resultado.HistoriasUsuario?.Count ?? 0,
                    Idioma = request.Idioma ?? "es",
                    VersionPrompt = request.VersionPrompt ?? "v1",
                    DuracionMs = (int)(resultado.TiempoTotal?.TotalMilliseconds ?? 0)
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

    public async Task<GenerarHUResponse> GenerarYGuardarHUsAsync(
        GenerarHURequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Iniciando generación y guardado de HUs");

            // Usar el Orchestrator
            var resultado = await _orchestrator.EjecutarPipelineCompletoAsync(request, cancellationToken);

            if (!resultado.Exitoso)
            {
                // Guardar registro de generación fallida
                await GuardarGeneracionFallidaAsync(request, resultado, cancellationToken);

                return new GenerarHUResponse
                {
                    Exitoso = false,
                    Mensaje = resultado.Error ?? "Error desconocido en el pipeline",
                    HistoriasUsuario = new List<HistoriaUsuarioResponse>(),
                    CorrelationId = resultado.CorrelationId
                };
            }

            // Crear registro de generación con campos de producción
            var generacion = new GeneracionHU
            {
                TextoEntrada = request.Texto ?? string.Empty,
                TextoProcesado = resultado.Limpieza?.TextoLimpio,
                Proyecto = request.Proyecto,
                Idioma = request.Idioma ?? "es",
                TotalHUs = resultado.HistoriasUsuario?.Count ?? 0,
                Exitoso = true,
                PromptVersion = request.VersionPrompt ?? "v1",
                Estado = EstadoGeneracion.Completado,
                DuracionMs = (int)(resultado.TiempoTotal?.TotalMilliseconds ?? 0),
                ModeloIA = _aiProvider.ModeloActual,
                TokensConsumidos = resultado.TokensTotales,
                CorrelationId = resultado.CorrelationId,
                FechaCreacion = DateTime.UtcNow
            };

            // Guardar generación
            generacion = await _generacionRepository.AgregarAsync(generacion, cancellationToken);

            // Guardar cada HU
            if (resultado.HistoriasUsuario != null)
            {
                foreach (var huResponse in resultado.HistoriasUsuario)
                {
                    var hu = MapearAEntidad(huResponse, generacion.Id);
                    await _huRepository.AgregarAsync(hu, cancellationToken);
                }
            }

            _logger.LogInformation("Generación guardada con ID {GeneracionId}, {Cantidad} HUs", 
                generacion.Id, generacion.TotalHUs);

            return new GenerarHUResponse
            {
                Exitoso = true,
                Mensaje = $"Se generaron y guardaron {resultado.HistoriasUsuario?.Count ?? 0} historias de usuario",
                HistoriasUsuario = resultado.HistoriasUsuario ?? new List<HistoriaUsuarioResponse>(),
                Metadata = new GenerarHUMetadata
                {
                    FechaGeneracion = DateTime.UtcNow,
                    Proyecto = request.Proyecto,
                    TotalHUs = resultado.HistoriasUsuario?.Count ?? 0,
                    Idioma = request.Idioma ?? "es",
                    VersionPrompt = request.VersionPrompt ?? "v1",
                    DuracionMs = (int)(resultado.TiempoTotal?.TotalMilliseconds ?? 0)
                },
                GeneracionId = generacion.Id,
                CorrelationId = resultado.CorrelationId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar y guardar HUs");
            return new GenerarHUResponse
            {
                Exitoso = false,
                Mensaje = $"Error al generar y guardar historias de usuario: {ex.Message}",
                HistoriasUsuario = new List<HistoriaUsuarioResponse>()
            };
        }
    }

    private async Task GuardarGeneracionFallidaAsync(
        GenerarHURequest request, 
        ResultadoPipeline resultado,
        CancellationToken cancellationToken)
    {
        try
        {
            var generacion = new GeneracionHU
            {
                TextoEntrada = request.Texto ?? string.Empty,
                TextoProcesado = null,
                Proyecto = request.Proyecto,
                Idioma = request.Idioma ?? "es",
                TotalHUs = 0,
                Exitoso = false,
                MensajeError = resultado.Error,
                PromptVersion = request.VersionPrompt ?? "v1",
                Estado = EstadoGeneracion.Error,
                DuracionMs = (int)(resultado.TiempoTotal?.TotalMilliseconds ?? 0),
                CorrelationId = resultado.CorrelationId,
                FechaCreacion = DateTime.UtcNow
            };

            await _generacionRepository.AgregarAsync(generacion, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar generación fallida");
        }
    }

    private HistoriaUsuario MapearAEntidad(HistoriaUsuarioResponse response, int generacionId)
    {
        return new HistoriaUsuario
        {
            Titulo = response.Titulo ?? string.Empty,
            Como = response.Como ?? string.Empty,
            Quiero = response.Quiero ?? string.Empty,
            Para = response.Para ?? string.Empty,
            Descripcion = response.Descripcion,
            GeneracionHUId = generacionId,
            FechaCreacion = DateTime.UtcNow,
            CriteriosAceptacion = response.CriteriosAceptacion?.Select(c => new CriterioAceptacion
            {
                Descripcion = c.Descripcion ?? string.Empty,
                Orden = c.Orden,
                EsObligatorio = c.EsObligatorio
            }).ToList() ?? new List<CriterioAceptacion>(),
            TareasTecnicas = response.TareasTecnicas?.Select(t => new TareaTecnica
            {
                Descripcion = t.Descripcion ?? string.Empty,
                Tipo = t.Tipo,
                Orden = t.Orden
            }).ToList() ?? new List<TareaTecnica>()
        };
    }
}