using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIHU.Domain.Interfaces;

namespace APIHU.Infrastructure.AI;

/// <summary>
/// Configuración del proveedor Anthropic (Claude)
/// </summary>
public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>
    /// API Key de Anthropic (sk-ant-...). Se lee preferentemente de la variable de entorno ANTHROPIC_API_KEY
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// URL base de la API de Anthropic
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1/messages";

    /// <summary>
    /// Versión de la API de Anthropic
    /// </summary>
    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>
    /// Modelo de Claude a utilizar (ej: claude-sonnet-4-6, claude-opus-4-6, claude-haiku-4-5-20251001)
    /// </summary>
    public string Modelo { get; set; } = "claude-sonnet-4-6";

    /// <summary>
    /// Temperatura (0.0 - 1.0). Más baja = más determinista
    /// </summary>
    public double Temperatura { get; set; } = 0.3;

    /// <summary>
    /// Tokens máximos de respuesta
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Número máximo de reintentos ante errores transitorios
    /// </summary>
    public int MaximoReintentos { get; set; } = 3;

    /// <summary>
    /// Tiempo base de espera entre reintentos (ms). Se multiplica exponencialmente.
    /// </summary>
    public int BackoffBaseMs { get; set; } = 1000;

    /// <summary>
    /// Timeout de cada request individual en segundos
    /// </summary>
    public int TimeoutSegundos { get; set; } = 120;
}

/// <summary>
/// Proveedor de IA usando la API de Anthropic (Claude).
/// Implementa reintentos con exponential backoff, manejo específico de códigos HTTP
/// (429 rate limit, 529 overloaded, 5xx) y tracking de tokens consumidos.
/// </summary>
public class AnthropicProviderService : IAIProviderService
{
    private readonly HttpClient _httpClient;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicProviderService> _logger;

    public string NombreProveedor => "Anthropic";
    public string ModeloActual => _options.Modelo;
    public UsoTokens UltimoUso { get; private set; } = UsoTokens.Vacio;

    public AnthropicProviderService(
        HttpClient httpClient,
        AnthropicOptions options,
        ILogger<AnthropicProviderService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("AnthropicOptions.ApiKey está vacía. Configúrala via ANTHROPIC_API_KEY o Anthropic:ApiKey.");
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSegundos);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", _options.ApiVersion);
    }

    public async Task<string> EnviarPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("El prompt no puede estar vacío", nameof(prompt));
        }

        Exception? ultimaExcepcion = null;

        for (var intento = 1; intento <= _options.MaximoReintentos; intento++)
        {
            try
            {
                _logger.LogInformation(
                    "Enviando prompt a Anthropic ({Modelo}) - intento {Intento}/{Max}",
                    _options.Modelo, intento, _options.MaximoReintentos);

                var respuesta = await EjecutarRequestAsync(prompt, cancellationToken);

                _logger.LogInformation(
                    "Respuesta de Anthropic OK. Tokens: in={In}, out={Out}, total={Total}",
                    UltimoUso.InputTokens, UltimoUso.OutputTokens, UltimoUso.Total);

                return respuesta;
            }
            catch (AnthropicTransientException ex) when (intento < _options.MaximoReintentos)
            {
                ultimaExcepcion = ex;
                var espera = CalcularBackoff(intento, ex.RetryAfter);
                _logger.LogWarning(
                    "Error transitorio de Anthropic (intento {Intento}): {Mensaje}. Reintentando en {Espera}ms",
                    intento, ex.Message, espera);
                await Task.Delay(espera, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && intento < _options.MaximoReintentos)
            {
                ultimaExcepcion = ex;
                var espera = CalcularBackoff(intento, null);
                _logger.LogWarning(
                    "Timeout en Anthropic (intento {Intento}). Reintentando en {Espera}ms",
                    intento, espera);
                await Task.Delay(espera, cancellationToken);
            }
        }

        _logger.LogError(ultimaExcepcion, "Fallaron todos los {Max} intentos contra Anthropic", _options.MaximoReintentos);
        throw new InvalidOperationException(
            $"No se pudo obtener respuesta de Anthropic después de {_options.MaximoReintentos} intentos",
            ultimaExcepcion);
    }

    private async Task<string> EjecutarRequestAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new AnthropicRequest
        {
            Model = _options.Modelo,
            MaxTokens = _options.MaxTokens,
            Temperature = _options.Temperatura,
            Messages = new[]
            {
                new AnthropicMessage { Role = "user", Content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(_options.BaseUrl, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LanzarExcepcionSegunStatus(response.StatusCode, responseBody, response.Headers);
        }

        AnthropicResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AnthropicResponse>(responseBody, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new AnthropicTransientException(
                $"Respuesta de Anthropic no parseable: {ex.Message}", null, ex);
        }

        if (parsed?.Content == null || parsed.Content.Length == 0)
        {
            throw new InvalidOperationException("Respuesta de Anthropic sin contenido");
        }

        UltimoUso = new UsoTokens(
            parsed.Usage?.InputTokens ?? 0,
            parsed.Usage?.OutputTokens ?? 0);

        var texto = string.Concat(parsed.Content
            .Where(c => c.Type == "text" && c.Text != null)
            .Select(c => c.Text));

        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new InvalidOperationException("Respuesta de Anthropic sin bloques de texto");
        }

        return texto;
    }

    private void LanzarExcepcionSegunStatus(HttpStatusCode status, string body, System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        TimeSpan? retryAfter = null;
        if (headers.RetryAfter?.Delta != null)
        {
            retryAfter = headers.RetryAfter.Delta;
        }
        else if (headers.RetryAfter?.Date != null)
        {
            retryAfter = headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
        }

        _logger.LogError("Error de Anthropic {Status}: {Body}", (int)status, Truncar(body, 500));

        switch ((int)status)
        {
            case 429: // rate limit
            case 529: // overloaded
            case >= 500 and < 600: // server errors
                throw new AnthropicTransientException(
                    $"Error transitorio {(int)status} de Anthropic", retryAfter);

            case 401:
                throw new InvalidOperationException(
                    "API key de Anthropic inválida o ausente (401). Revisa ANTHROPIC_API_KEY.");
            case 403:
                throw new InvalidOperationException(
                    "Permisos insuficientes en Anthropic (403).");
            case 400:
                throw new InvalidOperationException(
                    $"Request inválido a Anthropic (400): {Truncar(body, 300)}");
            case 404:
                throw new InvalidOperationException(
                    $"Modelo no encontrado en Anthropic (404). Modelo actual: '{_options.Modelo}'.");
            default:
                throw new InvalidOperationException(
                    $"Error no manejado de Anthropic ({(int)status}): {Truncar(body, 300)}");
        }
    }

    private int CalcularBackoff(int intento, TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue && retryAfter.Value.TotalMilliseconds > 0)
        {
            return (int)Math.Min(retryAfter.Value.TotalMilliseconds, 30_000);
        }

        // Exponential backoff con jitter suave
        var baseMs = _options.BackoffBaseMs * Math.Pow(2, intento - 1);
        var jitter = Random.Shared.Next(0, 250);
        return (int)Math.Min(baseMs + jitter, 30_000);
    }

    private static string Truncar(string texto, int max) =>
        texto.Length <= max ? texto : texto.Substring(0, max) + "...";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ============================================
    // DTOs de la API Anthropic
    // ============================================
    private class AnthropicRequest
    {
        public string Model { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public AnthropicMessage[] Messages { get; set; } = Array.Empty<AnthropicMessage>();
    }

    private class AnthropicMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    private class AnthropicResponse
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public string? StopReason { get; set; }
        public AnthropicContentBlock[]? Content { get; set; }
        public AnthropicUsage? Usage { get; set; }
    }

    private class AnthropicContentBlock
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }

    private class AnthropicUsage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }
}

/// <summary>
/// Excepción interna que marca un error como transitorio (elegible para retry)
/// </summary>
internal class AnthropicTransientException : Exception
{
    public TimeSpan? RetryAfter { get; }

    public AnthropicTransientException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
        : base(message, inner)
    {
        RetryAfter = retryAfter;
    }
}
