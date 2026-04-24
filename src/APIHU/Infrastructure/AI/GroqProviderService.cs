using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using APIHU.Domain.Interfaces;

namespace APIHU.Infrastructure.AI;

/// <summary>
/// Configuración del proveedor Groq (hosting ultrarrápido de modelos open-source)
/// </summary>
public class GroqOptions
{
    public const string SectionName = "Groq";

    /// <summary>
    /// API Key de Groq (prefijo gsk_...). Obtener gratis en https://console.groq.com/keys
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// URL base (compatible OpenAI)
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    /// <summary>
    /// Modelo a utilizar. Recomendados free:
    ///   llama-3.3-70b-versatile  (mejor calidad, 30 req/min free)
    ///   llama-3.1-8b-instant     (mucho más rápido, 30 req/min free)
    ///   mixtral-8x7b-32768
    ///   gemma2-9b-it
    /// </summary>
    public string Modelo { get; set; } = "llama-3.3-70b-versatile";

    public double Temperatura { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;
    public int MaximoReintentos { get; set; } = 3;
    public int BackoffBaseMs { get; set; } = 1000;
    public int TimeoutSegundos { get; set; } = 120;
}

/// <summary>
/// Proveedor de IA usando Groq Cloud (free tier estable, sin tarjeta).
/// Infraestructura propia de Groq (LPU) → respuestas muy rápidas.
/// </summary>
public class GroqProviderService : IAIProviderService
{
    private readonly HttpClient _httpClient;
    private readonly GroqOptions _options;
    private readonly ILogger<GroqProviderService> _logger;

    public string NombreProveedor => "Groq";
    public string ModeloActual => _options.Modelo;
    public UsoTokens UltimoUso { get; private set; } = UsoTokens.Vacio;

    public GroqProviderService(
        HttpClient httpClient,
        GroqOptions options,
        ILogger<GroqProviderService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("GroqOptions.ApiKey está vacía. Configúrala via GROQ_API_KEY o Groq:ApiKey.");
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSegundos);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
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
                    "Enviando prompt a Groq ({Modelo}) - intento {Intento}/{Max}",
                    _options.Modelo, intento, _options.MaximoReintentos);

                var respuesta = await EjecutarRequestAsync(prompt, cancellationToken);

                _logger.LogInformation(
                    "Respuesta de Groq OK. Tokens: in={In}, out={Out}, total={Total}",
                    UltimoUso.InputTokens, UltimoUso.OutputTokens, UltimoUso.Total);

                return respuesta;
            }
            catch (GroqTransientException ex) when (intento < _options.MaximoReintentos)
            {
                ultimaExcepcion = ex;
                var espera = CalcularBackoff(intento, ex.RetryAfter);
                _logger.LogWarning(
                    "Error transitorio de Groq (intento {Intento}): {Mensaje}. Reintentando en {Espera}ms",
                    intento, ex.Message, espera);
                await Task.Delay(espera, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && intento < _options.MaximoReintentos)
            {
                ultimaExcepcion = ex;
                var espera = CalcularBackoff(intento, null);
                _logger.LogWarning(
                    "Timeout en Groq (intento {Intento}). Reintentando en {Espera}ms",
                    intento, espera);
                await Task.Delay(espera, cancellationToken);
            }
        }

        _logger.LogError(ultimaExcepcion, "Fallaron todos los {Max} intentos contra Groq", _options.MaximoReintentos);
        throw new InvalidOperationException(
            $"No se pudo obtener respuesta de Groq después de {_options.MaximoReintentos} intentos",
            ultimaExcepcion);
    }

    private async Task<string> EjecutarRequestAsync(string prompt, CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.BaseUrl}/chat/completions";

        var requestBody = new
        {
            model = _options.Modelo,
            temperature = _options.Temperatura,
            max_tokens = _options.MaxTokens,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LanzarExcepcionSegunStatus(response.StatusCode, responseBody, response.Headers);
        }

        GroqResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GroqResponse>(responseBody, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new GroqTransientException(
                $"Respuesta de Groq no parseable: {ex.Message}", null, ex);
        }

        if (parsed?.Choices == null || parsed.Choices.Length == 0)
        {
            throw new InvalidOperationException("Respuesta de Groq sin choices");
        }

        UltimoUso = new UsoTokens(
            parsed.Usage?.PromptTokens ?? 0,
            parsed.Usage?.CompletionTokens ?? 0);

        var texto = parsed.Choices[0].Message?.Content;
        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new InvalidOperationException("Respuesta de Groq sin contenido en el mensaje");
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

        _logger.LogError("Error de Groq {Status}: {Body}", (int)status, Truncar(body, 500));

        switch ((int)status)
        {
            case 429:
            case >= 500 and < 600:
                throw new GroqTransientException(
                    $"Error transitorio {(int)status} de Groq", retryAfter);

            case 401:
                throw new InvalidOperationException(
                    "API key de Groq inválida. Revisa GROQ_API_KEY en https://console.groq.com/keys");
            case 403:
                throw new InvalidOperationException(
                    "Permisos insuficientes en Groq (403).");
            case 400:
                throw new InvalidOperationException(
                    $"Request inválido a Groq (400): {Truncar(body, 300)}");
            case 404:
                throw new InvalidOperationException(
                    $"Modelo no encontrado en Groq (404). Modelo actual: '{_options.Modelo}'.");
            default:
                throw new InvalidOperationException(
                    $"Error no manejado de Groq ({(int)status}): {Truncar(body, 300)}");
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
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private class GroqResponse
    {
        public GroqChoice[]? Choices { get; set; }
        public GroqUsage? Usage { get; set; }
    }

    private class GroqChoice
    {
        public GroqMessage? Message { get; set; }
    }

    private class GroqMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }

    private class GroqUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }
}

internal class GroqTransientException : Exception
{
    public TimeSpan? RetryAfter { get; }

    public GroqTransientException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
        : base(message, inner)
    {
        RetryAfter = retryAfter;
    }
}
