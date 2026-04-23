using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace APIHU.Infrastructure.Middleware;

/// <summary>
/// Middleware de Rate Limiting que limita requests por IP o cliente
/// Configurable desde appsettings
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _clientTracker;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitingOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
        _clientTracker = new ConcurrentDictionary<string, RateLimitEntry>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting if disabled
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        // Skip rate limiting for certain paths (health check, swagger, etc.)
        if (ShouldSkipRateLimiting(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var now = DateTime.UtcNow;

        // Obtener o crear entrada del cliente
        var entry = _clientTracker.GetOrAdd(clientId, _ => new RateLimitEntry());

        // Verificar si necesita reset de ventana de tiempo
        if (entry.WindowStart.Add(_options.Window) < now)
        {
            lock (entry)
            {
                if (entry.WindowStart.Add(_options.Window) < now)
                {
                    entry._requestCount = 0;
                    entry.WindowStart = now;
                }
            }
        }

        // Incrementar contador de forma atómica
        var currentCount = Interlocked.Increment(ref entry._requestCount);

        // Verificar si excede el límite
        if (currentCount > _options.MaxRequests)
        {
            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId}. Requests: {Count}/{Max}. Window: {WindowStart} to {WindowEnd}",
                clientId, currentCount, _options.MaxRequests, 
                entry.WindowStart, entry.WindowStart.Add(_options.Window));

            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.ContentType = "application/json";
            
            var retryAfter = (entry.WindowStart.Add(_options.Window) - now).TotalSeconds;
            context.Response.Headers["Retry-After"] = Math.Ceiling(retryAfter).ToString();
            context.Response.Headers["X-RateLimit-Limit"] = _options.MaxRequests.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = "0";
            context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(entry.WindowStart.Add(_options.Window)).ToUnixTimeSeconds().ToString();

            await context.Response.WriteAsJsonAsync(new
            {
                tipo = "RateLimitExceeded",
                mensaje = $"Se excedió el límite de requests. Máximo {_options.MaxRequests} requests por {_options.Window.TotalMinutes} minutos",
                limite = _options.MaxRequests,
                ventana = $"{_options.Window.TotalMinutes} minutos",
                retryAfterSeconds = (int)Math.Ceiling(retryAfter)
            });

            return;
        }

        // Agregar headers de rate limit a la respuesta
        context.Response.Headers["X-RateLimit-Limit"] = _options.MaxRequests.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = (_options.MaxRequests - currentCount).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(entry.WindowStart.Add(_options.Window)).ToUnixTimeSeconds().ToString();

        _logger.LogDebug(
            "Rate limit check for {ClientId}. Requests: {Count}/{Max}",
            clientId, entry.RequestCount, _options.MaxRequests);

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Usar API Key si está presente, si no usar IP del cliente
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey) &&
            !string.IsNullOrWhiteSpace(apiKey))
        {
            return $"apikey:{apiKey}";
        }

        // Obtener IP real (considerar proxies)
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        // Si es localhost, usar un identificador fijo
        if (ip == "127.0.0.1" || ip == "::1" || ip == "localhost")
        {
            return "localhost";
        }

        return ip;
    }

    private bool ShouldSkipRateLimiting(PathString path)
    {
        // No aplicar rate limiting a estas rutas
        var excludedPaths = new[]
        {
            "/api/hu/health",
            "/swagger",
            "/swagger/index.html",
            "/swagger/v1/swagger.json"
        };

        return excludedPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Entrada de tracking de rate limit por cliente
/// </summary>
internal class RateLimitEntry
{
    public int _requestCount;
    public int RequestCount => _requestCount;
    public DateTime WindowStart { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Opciones de configuración de rate limiting
/// </summary>
public class RateLimitingOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxRequests { get; set; } = 100; // Máximo de requests
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1); // Ventana de tiempo
}

/// <summary>
/// Extensiones para registrar el middleware de rate limiting
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }

    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new RateLimitingOptions();
        configuration.GetSection("RateLimiting").Bind(options);
        services.AddSingleton(options);
        
        return services;
    }
}