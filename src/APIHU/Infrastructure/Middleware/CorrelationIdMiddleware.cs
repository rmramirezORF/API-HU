using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace APIHU.Infrastructure.Middleware;

/// <summary>
/// Middleware que genera y gestiona un CorrelationId único para cada request
/// Permite trazabilidad completa a través de logs y respuestas HTTP
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    // Nombre del header para el Correlation ID
    public const string CorrelationIdHeader = "X-Correlation-ID";
    public const string CorrelationIdItemName = "CorrelationId";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Intentar obtener Correlation ID del header o generar uno nuevo
        var correlationId = GetOrCreateCorrelationId(context);

        // Asignar al HttpContext para acceso en cualquier parte
        context.Items[CorrelationIdItemName] = correlationId;

        // Asignar al Activity para trazabilidad con Serilog
        var activity = Activity.Current ?? new Activity("HTTP Request");
        if (Activity.Current == null)
        {
            Activity.Current = activity;
        }
        activity.SetParentId(correlationId);
        activity.AddTag("CorrelationId", correlationId);

        // Agregar el Correlation ID a la respuesta
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        _logger.LogDebug("CorrelationId {CorrelationId} asignado para request {Method} {Path}",
            correlationId, context.Request.Method, context.Request.Path);

        await _next(context);
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // 1. Verificar si ya existe en los headers de la request
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingCorrelationId) &&
            !string.IsNullOrWhiteSpace(existingCorrelationId))
        {
            return existingCorrelationId.ToString();
        }

        // 2. Verificar si hay un Correlation ID en query string (para debugging)
        if (context.Request.Query.TryGetValue("correlationId", out var queryCorrelationId) &&
            !string.IsNullOrWhiteSpace(queryCorrelationId))
        {
            return queryCorrelationId.ToString();
        }

        // 3. Generar un nuevo Correlation ID
        return GenerateCorrelationId();
    }

    private string GenerateCorrelationId()
    {
        // Formato: UUID corto (8 caracteres) + timestamp
        return $"{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }
}

/// <summary>
/// Extensiones para registrar el middleware de Correlation ID
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Obtiene el Correlation ID del HttpContext actual
    /// </summary>
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items[CorrelationIdMiddleware.CorrelationIdItemName] as string;
    }
}

/// <summary>
/// Extensiones para ILogger que incluyen Correlation ID automáticamente
/// </summary>
public static class LoggerExtensions
{
    public static void LogWithCorrelation(
        this ILogger logger,
        LogLevel logLevel,
        string message,
        string? correlationId,
        params object[] args)
    {
        var formattedMessage = correlationId != null 
            ? $"[{correlationId}] {message}" 
            : message;
        
        logger.Log(logLevel, formattedMessage, args);
    }

    public static void LogInformationWithCorrelation(
        this ILogger logger,
        string message,
        string? correlationId,
        params object[] args)
    {
        logger.LogWithCorrelation(LogLevel.Information, message, correlationId, args);
    }

    public static void LogErrorWithCorrelation(
        this ILogger logger,
        Exception exception,
        string message,
        string? correlationId,
        params object[] args)
    {
        var formattedMessage = correlationId != null 
            ? $"[{correlationId}] {message}" 
            : message;
        
        logger.LogError(exception, formattedMessage, args);
    }
}