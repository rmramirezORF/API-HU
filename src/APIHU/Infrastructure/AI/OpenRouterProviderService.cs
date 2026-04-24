using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIHU.Domain.Interfaces;

namespace APIHU.Infrastructure.AI;

/// <summary>
/// Configuración del proveedor OpenRouter (aggregator OpenAI-compatible)
/// </summary>
public class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    /// <summary>
    /// API Key de OpenRouter (prefijo sk-or-...). Obtener en https://openrouter.ai/keys
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// URL base de la API (compatible OpenAI)
    /// </summary>
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    /// <summary>
    /// Modelo principal a utilizar. Ejemplos de modelos free (inestables, rotan):
    ///   google/gemma-3-27b-it:free
    ///   qwen/qwen3-next-80b-a3b-instruct:free
    ///   openai/gpt-oss-120b:free
    ///   inclusionai/ling-2.6-flash:free
    /// Modelos de pago también soportados (sin sufijo :free).
    /// </summary>
    public string Modelo { get; set; } = "google/gemma-3-27b-it:free";

    /// <summary>
    /// Modelos alternativos si el principal falla con 429/503/out_tokens=0.
    /// Se prueban en orden. Útil para tier :free donde los modelos saturan.
    /// </summary>
    public string[] ModelosFallback { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Temperatura (0.0 - 2.0)
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

    /// <summary>
    /// Identificador de tu app (opcional, mejora tu ranking en OpenRouter). Se envía en HTTP-Referer.
    /// </summary>
    public string? SiteUrl { get; set; }

    /// <summary>
    /// Nombre de tu app (opcional). Se envía en X-Title.
    /// </summary>
    public string? AppName { get; set; } = "API-HU";
}

/// <summary>
/// Proveedor de IA usando OpenRouter. API compatible con OpenAI.
/// Permite acceder a muchos modelos (incluyendo free tier) con una sola key.
/// </summary>
public class OpenRouterProviderService : IAIProviderService
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly ILogger<OpenRouterProviderService> _logger;

    public string NombreProveedor => "OpenRouter";
    public string ModeloActual => _modeloEnUso;
    public UsoTokens UltimoUso { get; private set; } = UsoTokens.Vacio;

    /// <summary>
    /// Modelo efectivamente usado en la última llamada (puede diferir de Options.Modelo si cayó a fallback)
    /// </summary>
    private string _modeloEnUso;

    public OpenRouterProviderService(
        HttpClient httpClient,
        OpenRouterOptions options,
        ILogger<OpenRouterProviderService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _modeloEnUso = options.Modelo;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("OpenRouterOptions.ApiKey está vacía. Configúrala via OPENROUTER_API_KEY o OpenRouter:ApiKey.");
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSegundos);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        if (!string.IsNullOrWhiteSpace(_options.SiteUrl))
        {
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _options.SiteUrl);
        }
        if (!string.IsNullOrWhiteSpace(_options.AppName))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Title", _options.AppName);
        }
    }

    public async Task<string> EnviarPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("El prompt no puede estar vacío", nameof(prompt));
        }

        // Ordena: primero el modelo principal, luego cada fallback
        var modelosAProbar = new List<string> { _options.Modelo };
        modelosAProbar.AddRange(_options.ModelosFallback ?? Array.Empty<string>());

        Exception? ultimaExcepcion = null;

        for (var idx = 0; idx < modelosAProbar.Count; idx++)
        {
            var modelo = modelosAProbar[idx];
            var esFallback = idx > 0;

            if (esFallback)
            {
                _logger.LogWarning(
                    "Modelo principal agotado. Intentando fallback #{Idx}: {Modelo}",
                    idx, modelo);
            }

            for (var intento = 1; intento <= _options.MaximoReintentos; intento++)
            {
                try
                {
                    _logger.LogInformation(
                        "Enviando prompt a OpenRouter ({Modelo}) - intento {Intento}/{Max}",
                        modelo, intento, _options.MaximoReintentos);

                    var respuesta = await EjecutarRequestAsync(prompt, modelo, cancellationToken);

                    _modeloEnUso = modelo;

                    _logger.LogInformation(
                        "Respuesta de OpenRouter OK [{Modelo}]. Tokens: in={In}, out={Out}, total={Total}",
                        modelo, UltimoUso.InputTokens, UltimoUso.OutputTokens, UltimoUso.Total);

                    return respuesta;
                }
                catch (OpenRouterTransientException ex) when (intento < _options.MaximoReintentos)
                {
                    ultimaExcepcion = ex;
                    var espera = CalcularBackoff(intento, ex.RetryAfter);
                    _logger.LogWarning(
                        "Error transitorio en {Modelo} (intento {Intento}): {Mensaje}. Reintentando en {Espera}ms",
                        modelo, intento, ex.Message, espera);
                    await Task.Delay(espera, cancellationToken);
                }
                catch (OpenRouterTransientException ex)
                {
                    // Agotamos retries de este modelo -> caer al siguiente fallback
                    ultimaExcepcion = ex;
                    _logger.LogWarning(
                        "Modelo {Modelo} agotó {Max} reintentos. {Restantes} fallback(s) restante(s).",
                        modelo, _options.MaximoReintentos, modelosAProbar.Count - idx - 1);
                    break;
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && intento < _options.MaximoReintentos)
                {
                    ultimaExcepcion = ex;
                    var espera = CalcularBackoff(intento, null);
                    _logger.LogWarning(
                        "Timeout en {Modelo} (intento {Intento}). Reintentando en {Espera}ms",
                        modelo, intento, espera);
                    await Task.Delay(espera, cancellationToken);
                }
            }
        }

        _logger.LogError(ultimaExcepcion,
            "Fallaron todos los modelos ({Count}) contra OpenRouter",
            modelosAProbar.Count);
        throw new InvalidOperationException(
            $"No se pudo obtener respuesta de OpenRouter después de probar {modelosAProbar.Count} modelo(s) con {_options.MaximoReintentos} reintento(s) cada uno",
            ultimaExcepcion);
    }

    private async Task<string> EjecutarRequestAsync(string prompt, string modelo, CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.BaseUrl}/chat/completions";

        var requestBody = new OpenRouterRequest
        {
            Model = modelo,
            Temperature = _options.Temperatura,
            MaxTokens = _options.MaxTokens,
            Messages = new[]
            {
                new OpenRouterMessage { Role = "user", Content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LanzarExcepcionSegunStatus(response.StatusCode, responseBody, response.Headers);
        }

        OpenRouterResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new OpenRouterTransientException(
                $"Respuesta de OpenRouter no parseable: {ex.Message}", null, ex);
        }

        if (parsed?.Choices == null || parsed.Choices.Length == 0)
        {
            throw new InvalidOperationException("Respuesta de OpenRouter sin choices");
        }

        UltimoUso = new UsoTokens(
            parsed.Usage?.PromptTokens ?? 0,
            parsed.Usage?.CompletionTokens ?? 0);

        var texto = parsed.Choices[0].Message?.Content;

        // Modelo respondió vacío → tratar como transitorio para permitir fallback a otro modelo
        if (string.IsNullOrWhiteSpace(texto) || UltimoUso.OutputTokens == 0)
        {
            throw new OpenRouterTransientException(
                "Respuesta vacía de OpenRouter (out_tokens=0). El modelo puede estar degradado.");
        }

        return texto;
    }

    private void LanzarExcepcionSegunStatus(HttpStatusCode status, string body, HttpResponseHeaders headers)
    {
        TimeSpan? retryAfter = null;
        if (headers.RetryAfter?.Delta != null)
        {
            retryAfter = headers.RetryAfter.Delta;
        }

        _logger.LogError("Error de OpenRouter {Status}: {Body}", (int)status, Truncar(body, 500));

        switch ((int)status)
        {
            case 429: // rate limit
            case 502: // modelo free saturado
            case >= 500 and < 600:
                throw new OpenRouterTransientException(
                    $"Error transitorio {(int)status} de OpenRouter", retryAfter);

            case 401:
                throw new InvalidOperationException(
                    "API key de OpenRouter inválida. Revisa OPENROUTER_API_KEY en https://openrouter.ai/keys");
            case 402:
                throw new InvalidOperationException(
                    "OpenRouter requiere crédito (402). Revisa tu balance o usa un modelo con sufijo ':free'.");
            case 403:
                throw new InvalidOperationException(
                    "Permisos insuficientes en OpenRouter (403).");
            case 400:
                throw new InvalidOperationException(
                    $"Request inválido a OpenRouter (400): {Truncar(body, 300)}");
            case 404:
                // Modelo retirado / no disponible → tratar como transitorio para caer a fallback
                throw new OpenRouterTransientException(
                    $"Modelo no encontrado en OpenRouter (404). Probando fallback si existe.");
            default:
                throw new InvalidOperationException(
                    $"Error no manejado de OpenRouter ({(int)status}): {Truncar(body, 300)}");
        }
    }

    private int CalcularBackoff(int intento, TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue && retryAfter.Value.TotalMilliseconds > 0)
        {
            return (int)Math.Min(retryAfter.Value.TotalMilliseconds, 30_000);
        }

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
    // DTOs (formato OpenAI)
    // ============================================
    private class OpenRouterRequest
    {
        public string Model { get; set; } = string.Empty;
        public OpenRouterMessage[] Messages { get; set; } = Array.Empty<OpenRouterMessage>();
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
    }

    private class OpenRouterMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    private class OpenRouterResponse
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public OpenRouterChoice[]? Choices { get; set; }
        public OpenRouterUsage? Usage { get; set; }
    }

    private class OpenRouterChoice
    {
        public int Index { get; set; }
        public OpenRouterMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private class OpenRouterUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}

internal class OpenRouterTransientException : Exception
{
    public TimeSpan? RetryAfter { get; }

    public OpenRouterTransientException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
        : base(message, inner)
    {
        RetryAfter = retryAfter;
    }
}
