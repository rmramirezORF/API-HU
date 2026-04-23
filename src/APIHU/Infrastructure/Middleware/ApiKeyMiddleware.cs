using System.Runtime.CompilerServices;

namespace APIHU.Infrastructure.Middleware;

/// <summary>
/// Middleware de validación de API Key para seguridad
/// Valida el header X-API-Key contra las claves configuradas
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private readonly ApiKeyOptions _options;

    public const string ApiKeyHeader = "X-API-Key";

    public ApiKeyMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyMiddleware> logger,
        ApiKeyOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation if API Key is disabled (development mode)
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        // Skip validation for certain paths (health check, swagger, etc.)
        if (ShouldSkipValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Obtener API Key del header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var apiKey) ||
            string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "Request sin API Key. Path: {Path}, IP: {IP}",
                context.Request.Path, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = 401; // Unauthorized
            context.Response.ContentType = "application/json";
            
            await context.Response.WriteAsJsonAsync(new
            {
                tipo = "Unauthorized",
                mensaje = "Se requiere API Key en el header X-API-Key",
                headerRequerido = ApiKeyHeader
            });

            return;
        }

        // Validar API Key
        if (!IsValidApiKey(apiKey!))
        {
            _logger.LogWarning(
                "API Key inválida. Path: {Path}, IP: {IP}, Key: {Key}",
                context.Request.Path, context.Connection.RemoteIpAddress, MaskApiKey(apiKey!));

            context.Response.StatusCode = 403; // Forbidden
            context.Response.ContentType = "application/json";
            
            await context.Response.WriteAsJsonAsync(new
            {
                tipo = "Forbidden",
                mensaje = "API Key inválida",
                codigo = "INVALID_API_KEY"
            });

            return;
        }

        // API Key válida, continuar
        _logger.LogDebug(
            "Request autenticado correctamente. Path: {Path}, IP: {IP}",
            context.Request.Path, context.Connection.RemoteIpAddress);

        await _next(context);
    }

    private bool IsValidApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        // Comparar con las API Keys configuradas
        return _options.ValidKeys.Contains(apiKey, StringComparer.OrdinalIgnoreCase);
    }

    private bool ShouldSkipValidation(PathString path)
    {
        // No aplicar validación a estas rutas
        var excludedPaths = new[]
        {
            "/api/hu/health",
            "/swagger",
            "/swagger/index.html",
            "/swagger/v1/swagger.json"
        };

        return excludedPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }

    private string MaskApiKey(string apiKey)
    {
        // Mostrar solo los primeros 4 caracteres
        if (apiKey.Length <= 4)
            return "****";
        
        return $"{apiKey[..4]}****{apiKey[^4..]}";
    }
}

/// <summary>
/// Opciones de configuración de API Key
/// </summary>
public class ApiKeyOptions
{
    public bool Enabled { get; set; } = false; // Por defecto desactivado para desarrollo
    public List<string> ValidKeys { get; set; } = new();
    public string HeaderName { get; set; } = "X-API-Key";
}

/// <summary>
/// Extensiones para registrar el middleware de API Key
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKey(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }

    public static IServiceCollection AddApiKey(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new ApiKeyOptions();
        configuration.GetSection("ApiKey").Bind(options);
        services.AddSingleton(options);
        
        return services;
    }
}