using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIHU.Domain.Interfaces;

namespace APIHU.Infrastructure.AI;

/// <summary>
/// Configuración del proveedor Google Gemini
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// API Key de Google AI Studio (prefijo AIza...). Se puede obtener gratis en https://aistudio.google.com/apikey
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// URL base de la API de Gemini
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>
    /// Modelo a utilizar. Modelos con free tier activo (abril 2026):
    ///   gemini-2.5-flash       (recomendado - con thinking interno)
    ///   gemini-2.5-flash-lite  (más rápido y ligero)
    /// Los modelos gemini-1.5-* y gemini-2.0-flash ya NO tienen free tier.
    /// </summary>
    public string Modelo { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// Temperatura (0.0 - 2.0 en Gemini, aunque se recomienda 0.0 - 1.0)
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
    /// Solo aplica a modelos Gemini 2.5+. Cantidad de tokens que el modelo puede usar
    /// para razonamiento interno ("thinking") antes de generar la respuesta visible.
    ///   0  = thinking desactivado (más rápido, más tokens disponibles para la respuesta)
    ///   -1 = dinámico (Google decide, default del modelo)
    ///   N  = hasta N tokens de thinking
    /// Para generar JSON estructurado (HUs) lo dejamos en 0 por defecto.
    /// </summary>
    public int ThinkingBudget { get; set; } = 0;
}

/// <summary>
/// Proveedor de IA usando la API de Google Gemini.
/// Ideal para uso free: 1500 requests/día y 1M tokens/día en gemini-2.0-flash.
/// </summary>
public class GeminiProviderService : IAIProviderService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiProviderService> _logger;

    public string NombreProveedor => "Gemini";
    public string ModeloActual => _options.Modelo;
    public UsoTokens UltimoUso { get; private set; } = UsoTokens.Vacio;

    public GeminiProviderService(
        HttpClient httpClient,
        GeminiOptions options,
        ILogger<GeminiProviderService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("GeminiOptions.ApiKey está vacía. Configúrala via GEMINI_API_KEY o Gemini:ApiKey.");
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSegundos);
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
                    "Enviando prompt a Gemini ({Modelo}) - intento {Intento}/{Max}",
                    _options.Modelo, intento, _options.MaximoReintentos);

                var respuesta = await EjecutarRequestAsync(prompt, cancellationToken);

                _logger.LogInformation(
                    "Respuesta de Gemini OK. Tokens: in={In}, out={Out}, total={Total}",
                    UltimoUso.InputTokens, UltimoUso.OutputTokens, UltimoUso.Total);

                return respuesta;
            }
            catch (GeminiTransientException ex) when (intento < _options.MaximoReintentos)
            {
                ultimaExcepcion = ex;
                var espera = CalcularBackoff(intento, ex.RetryAfter);
                _logger.LogWarning(
                    "Error transitorio de Gemini (intento {Intento}): {Mensaje}. Reintentando en {Espera}ms",
                    intento, ex.Message, espera);
                await Task.Delay(espera, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && intento < _options.MaximoReintentos)
            {
                ultimaExcepcion = ex;
                var espera = CalcularBackoff(intento, null);
                _logger.LogWarning(
                    "Timeout en Gemini (intento {Intento}). Reintentando en {Espera}ms",
                    intento, espera);
                await Task.Delay(espera, cancellationToken);
            }
        }

        _logger.LogError(ultimaExcepcion, "Fallaron todos los {Max} intentos contra Gemini", _options.MaximoReintentos);
        throw new InvalidOperationException(
            $"No se pudo obtener respuesta de Gemini después de {_options.MaximoReintentos} intentos",
            ultimaExcepcion);
    }

    private async Task<string> EjecutarRequestAsync(string prompt, CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.BaseUrl}/models/{_options.Modelo}:generateContent";

        var generationConfig = new GeminiGenerationConfig
        {
            Temperature = _options.Temperatura,
            MaxOutputTokens = _options.MaxTokens
        };

        // Gemini 2.5+ soporta thinkingConfig. Para nuestro caso (JSON estructurado)
        // es mejor desactivarlo para no gastar tokens en razonamiento oculto.
        if (_options.Modelo.Contains("2.5", StringComparison.OrdinalIgnoreCase))
        {
            generationConfig.ThinkingConfig = new GeminiThinkingConfig
            {
                ThinkingBudget = _options.ThinkingBudget
            };
        }

        var requestBody = new GeminiRequest
        {
            Contents = new[]
            {
                new GeminiContent
                {
                    Parts = new[] { new GeminiPart { Text = prompt } }
                }
            },
            GenerationConfig = generationConfig
        };

        var json = JsonSerializer.Serialize(requestBody, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LanzarExcepcionSegunStatus(response.StatusCode, responseBody, response.Headers);
        }

        GeminiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeminiResponse>(responseBody, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new GeminiTransientException(
                $"Respuesta de Gemini no parseable: {ex.Message}", null, ex);
        }

        if (parsed?.Candidates == null || parsed.Candidates.Length == 0)
        {
            throw new InvalidOperationException("Respuesta de Gemini sin candidatos");
        }

        UltimoUso = new UsoTokens(
            parsed.UsageMetadata?.PromptTokenCount ?? 0,
            parsed.UsageMetadata?.CandidatesTokenCount ?? 0);

        var partes = parsed.Candidates[0].Content?.Parts;
        if (partes == null || partes.Length == 0)
        {
            throw new InvalidOperationException("Respuesta de Gemini sin contenido en el candidato");
        }

        var texto = string.Concat(partes.Where(p => p.Text != null).Select(p => p.Text));

        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new InvalidOperationException("Respuesta de Gemini sin texto");
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

        _logger.LogError("Error de Gemini {Status}: {Body}", (int)status, Truncar(body, 500));

        switch ((int)status)
        {
            case 429: // rate limit
            case >= 500 and < 600: // server errors
                throw new GeminiTransientException(
                    $"Error transitorio {(int)status} de Gemini", retryAfter);

            case 401:
            case 403:
                throw new InvalidOperationException(
                    "API key de Gemini inválida o sin permisos. Revisa GEMINI_API_KEY en https://aistudio.google.com/apikey");
            case 400:
                throw new InvalidOperationException(
                    $"Request inválido a Gemini (400): {Truncar(body, 300)}");
            case 404:
                throw new InvalidOperationException(
                    $"Modelo no encontrado en Gemini (404). Modelo actual: '{_options.Modelo}'.");
            default:
                throw new InvalidOperationException(
                    $"Error no manejado de Gemini ({(int)status}): {Truncar(body, 300)}");
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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ============================================
    // DTOs de la API Gemini
    // ============================================
    private class GeminiRequest
    {
        public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private class GeminiContent
    {
        public string? Role { get; set; }
        public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
    }

    private class GeminiPart
    {
        public string? Text { get; set; }
    }

    private class GeminiGenerationConfig
    {
        public double Temperature { get; set; }
        public int MaxOutputTokens { get; set; }
        public GeminiThinkingConfig? ThinkingConfig { get; set; }
    }

    private class GeminiThinkingConfig
    {
        public int ThinkingBudget { get; set; }
    }

    private class GeminiResponse
    {
        public GeminiCandidate[]? Candidates { get; set; }
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
        public string? FinishReason { get; set; }
    }

    private class GeminiUsageMetadata
    {
        public int PromptTokenCount { get; set; }
        public int CandidatesTokenCount { get; set; }
        public int TotalTokenCount { get; set; }
    }
}

internal class GeminiTransientException : Exception
{
    public TimeSpan? RetryAfter { get; }

    public GeminiTransientException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
        : base(message, inner)
    {
        RetryAfter = retryAfter;
    }
}
