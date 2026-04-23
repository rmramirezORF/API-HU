using System.Collections.Concurrent;
using APIHU.Application.DTOs;
using APIHU.Application.Interfaces;
using APIHU.Application.Services;
using APIHU.Domain.Entities;
using APIHU.Domain.Interfaces;

namespace APIHU.Infrastructure.BackgroundServices;

/// <summary>
/// Background Service para procesamiento asíncrono de HUs
/// Preparado para cola de procesamiento futuro
/// </summary>
public class HuProcessingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HuProcessingBackgroundService> _logger;
    private readonly BackgroundServiceOptions _options;
    
    // Cola de procesamiento en memoria (preparado para Redis/MQ en futuro)
    private readonly ConcurrentQueue<PendingHuRequest> _pendingRequests;
    private readonly CancellationTokenSource _cts;

    public HuProcessingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<HuProcessingBackgroundService> logger,
        BackgroundServiceOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
        _pendingRequests = new ConcurrentQueue<PendingHuRequest>();
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Encola una request para procesamiento asíncrono
    /// </summary>
    public void EnqueueRequest(PendingHuRequest request)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Background service deshabilitado, no se encolará la request");
            return;
        }

        _pendingRequests.Enqueue(request);
        _logger.LogInformation(
            "Request encolada para procesamiento asíncrono. CorrelationId: {CorrelationId}. Cola: {Count}",
            request.CorrelationId, _pendingRequests.Count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Service de procesamiento de HUs iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Procesar requests pendientes
                while (_pendingRequests.TryDequeue(out var request))
                {
                    await ProcessRequestAsync(request, stoppingToken);
                }

                // Esperar antes de verificar nuevamente
                await Task.Delay(_options.ProcessInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // CancellationToken solicitada, salir del loop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el Background Service");
                await Task.Delay(_options.ErrorRetryDelay, stoppingToken);
            }
        }

        _logger.LogInformation("Background Service de procesamiento de HUs detenido");
    }

    private async Task ProcessRequestAsync(PendingHuRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Procesando request asíncrona. CorrelationId: {CorrelationId}",
            request.CorrelationId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IHuProcessingOrchestrator>();
            var generacionRepo = scope.ServiceProvider.GetRequiredService<IGeneracionRepository>();

            // Ejecutar pipeline
            var resultado = await orchestrator.EjecutarPipelineCompletoAsync(
                request.Request, 
                cancellationToken);

            // Guardar en base de datos
            var generacion = await orchestrator.GuardarGeneracionAsync(
                request.Request, 
                resultado, 
                cancellationToken);

            // Guardar las HUs generadas
            if (resultado.Exitoso && resultado.HistoriasUsuario != null)
            {
                var huRepo = scope.ServiceProvider.GetRequiredService<IHistoriaUsuarioRepository>();
                
                foreach (var huResponse in resultado.HistoriasUsuario)
                {
                    var hu = MapToEntity(huResponse, generacion.Id);
                    await huRepo.AgregarAsync(hu, cancellationToken);
                }
            }

            // Notificar completion (en futuro: SignalR, Webhooks, etc.)
            await NotifyCompletionAsync(request, resultado, cancellationToken);

            _logger.LogInformation(
                "Request procesada exitosamente. CorrelationId: {CorrelationId}, HUs: {Count}",
                request.CorrelationId, resultado.HistoriasUsuario?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al procesar request asíncrona. CorrelationId: {CorrelationId}",
                request.CorrelationId);

            // Notificar error
            await NotifyErrorAsync(request, ex, cancellationToken);
        }
    }

    private async Task NotifyCompletionAsync(
        PendingHuRequest request, 
        ResultadoPipeline resultado,
        CancellationToken cancellationToken)
    {
        // TODO: Implementar notificación (SignalR, Webhook, etc.)
        _logger.LogDebug(
            "Notificación de completion para CorrelationId: {CorrelationId}",
            request.CorrelationId);
    }

    private async Task NotifyErrorAsync(
        PendingHuRequest request, 
        Exception ex,
        CancellationToken cancellationToken)
    {
        // TODO: Implementar notificación de error
        _logger.LogDebug(
            "Notificación de error para CorrelationId: {CorrelationId}, Error: {Error}",
            request.CorrelationId, ex.Message);
    }

    private HistoriaUsuario MapToEntity(HistoriaUsuarioResponse response, int generacionId)
    {
        return new HistoriaUsuario
        {
            Titulo = response.Titulo ?? string.Empty,
            Como = response.Como ?? string.Empty,
            Quiero = response.Quiero ?? string.Empty,
            Para = response.Para ?? string.Empty,
            Descripcion = response.Descripcion,
            GeneracionHUId = generacionId
        };
    }

    public override void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Request pendiente de procesamiento
/// </summary>
public class PendingHuRequest
{
    public string CorrelationId { get; set; } = string.Empty;
    public GenerarHURequest Request { get; set; } = null!;
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
}

/// <summary>
/// Opciones de configuración del Background Service
/// </summary>
public class BackgroundServiceOptions
{
    public bool Enabled { get; set; } = false; // Deshabilitado por defecto
    public TimeSpan ProcessInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ErrorRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxConcurrentProcessing { get; set; } = 5;
}

/// <summary>
/// Extensiones para registrar el Background Service
/// </summary>
public static class HuBackgroundServiceExtensions
{
    public static IServiceCollection AddHuBackgroundService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new BackgroundServiceOptions();
        configuration.GetSection("BackgroundService").Bind(options);
        services.AddSingleton(options);

        services.AddHostedService<HuProcessingBackgroundService>();

        return services;
    }
}