using Serilog;
using Serilog.Events;

namespace APIHU.Infrastructure.Logging;

/// <summary>
/// Configuración de Serilog para logging estructurado
/// </summary>
public static class LoggingConfiguration
{
    public static void ConfigureLogging(WebApplicationBuilder builder)
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "API-HU")
            .Enrich.WithProperty("Version", "2.0.0")
            .WriteTo.Console(outputTemplate: 
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logDirectory, "api-hu-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        builder.Host.UseSerilog();

        Log.Information("Logging configurado correctamente. Directorio: {LogDirectory}", logDirectory);
    }
}

/// <summary>
/// Middleware para logging de requests HTTP
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTime.UtcNow;

        // Log de request entrante
        _logger.LogInformation(
            "Request {RequestId} | {Method} {Path} | Query: {Query} | Client: {ClientIP}",
            requestId,
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Connection.RemoteIpAddress);

        try
        {
            await _next(context);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation(
                "Response {RequestId} | {StatusCode} | Duration: {Duration}ms",
                requestId,
                context.Response.StatusCode,
                duration.TotalMilliseconds);
        }
    }
}

/// <summary>
/// Extensiones para registrar el middleware
/// </summary>
public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}