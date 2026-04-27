using Microsoft.AspNetCore.Mvc;
using APIHU.Application.DTOs;
using APIHU.Application.Interfaces;
using APIHU.Domain.Interfaces;

namespace APIHU.API.Controllers;

/// <summary>
/// Controller para la gestión de Historias de Usuario
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HUController : ControllerBase
{
    private readonly IGeneracionHUService _generacionService;
    private readonly IAIProviderService _aiProvider;
    private readonly ILogger<HUController> _logger;

    public HUController(
        IGeneracionHUService generacionService,
        IAIProviderService aiProvider,
        ILogger<HUController> logger)
    {
        _generacionService = generacionService;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    /// <summary>
    /// Genera Historias de Usuario a partir de texto (formato JSON)
    /// </summary>
    /// <remarks>
    /// Recibe un texto (transcripción de reunión, requerimientos) en JSON y genera Historias de Usuario.
    ///
    /// Pipeline de 3 etapas:
    /// 1. **Limpieza**: elimina ruido, fillers y normaliza
    /// 2. **Estructuración**: identifica requerimientos
    /// 3. **Generación**: crea HUs con criterios y tareas técnicas
    ///
    /// **Importante:** los saltos de línea dentro de "texto" deben escaparse como `\n`.
    /// Si tu texto tiene muchos saltos de línea (transcripción de Teams, etc.) usa
    /// el endpoint `POST /api/hu/generate-from-text` que acepta texto plano.
    /// </remarks>
    /// <param name="request">Request con el texto a procesar</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <response code="200">Historias de usuario generadas exitosamente</response>
    /// <response code="400">Error de validación o de generación</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpPost("generate")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(GenerarHUResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GenerarHUResponse>> GenerarHU(
        [FromBody] GenerarHURequest request,
        CancellationToken cancellationToken)
    {
        return await EjecutarGeneracionAsync(request, cancellationToken);
    }

    /// <summary>
    /// Genera Historias de Usuario aceptando el texto crudo en el body (text/plain)
    /// </summary>
    /// <remarks>
    /// Versión más cómoda para pegar transcripciones largas de Teams, Meet, etc.
    ///
    /// El cuerpo de la request es el texto plano sin escapar. Los demás parámetros
    /// se pasan como query string.
    ///
    /// **Ejemplo:**
    /// ```
    /// POST /api/hu/generate-from-text?proyecto=OneDrive&amp;maximoHUs=2&amp;idioma=es
    /// Content-Type: text/plain
    ///
    /// Hola Nicole, ¿cómo vas?
    /// NICOLL: Está muy bien, gracias.
    /// REYVING: ...
    /// ```
    ///
    /// El texto se valida igual que el endpoint JSON: mínimo 20 caracteres, máximo 10.000.
    /// </remarks>
    /// <param name="proyecto">Nombre del proyecto (opcional)</param>
    /// <param name="maximoHUs">Número MÁXIMO de HUs a generar (1-20). Default: 5. El modelo puede generar menos si juzga que basta.</param>
    /// <param name="idioma">Código de idioma. Default: "es"</param>
    /// <param name="versionPrompt">Versión de prompts a usar (v1=clásicos, v2=mejorados con detección de roles). Default: "v2"</param>
    /// <param name="contexto">Contexto extra del proyecto/equipo/roles. Mejora mucho la precisión cuando la transcripción no aclara quién es el actor (ej: "Nicoll es coordinadora de RRHH y gestiona 12 personas")</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <response code="200">Historias de usuario generadas exitosamente</response>
    /// <response code="400">Error de validación o de generación</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpPost("generate-from-text")]
    [Consumes("text/plain")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(GenerarHUResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GenerarHUResponse>> GenerarHUFromText(
        [FromBody] string texto,
        [FromQuery] string? proyecto = null,
        [FromQuery] int? maximoHUs = null,
        [FromQuery] string? idioma = "es",
        [FromQuery] string? versionPrompt = "v2",
        [FromQuery] string? contexto = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return BadRequest(new ErrorResponse
            {
                Tipo = "ValidationError",
                Mensaje = "El cuerpo de la request está vacío. Envía el texto a procesar como text/plain."
            });
        }

        // Validaciones manuales (DataAnnotations no aplican a string del body en text/plain)
        if (texto.Length < 20)
        {
            return BadRequest(new ErrorResponse
            {
                Tipo = "ValidationError",
                Mensaje = $"El texto debe tener al menos 20 caracteres (actual: {texto.Length})."
            });
        }
        if (texto.Length > 10_000)
        {
            return BadRequest(new ErrorResponse
            {
                Tipo = "ValidationError",
                Mensaje = $"El texto excede el máximo de 10.000 caracteres (actual: {texto.Length})."
            });
        }
        if (maximoHUs.HasValue && (maximoHUs < 1 || maximoHUs > 20))
        {
            return BadRequest(new ErrorResponse
            {
                Tipo = "ValidationError",
                Mensaje = "maximoHUs debe estar entre 1 y 20."
            });
        }

        var request = new GenerarHURequest
        {
            Texto = texto,
            Proyecto = proyecto,
            MaximoHUs = maximoHUs,
            Idioma = idioma,
            VersionPrompt = versionPrompt,
            Contexto = contexto
        };

        return await EjecutarGeneracionAsync(request, cancellationToken);
    }

    /// <summary>
    /// Health check del servicio
    /// </summary>
    /// <returns>Estado del servicio y proveedor de IA activo</returns>
    [HttpGet("health")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "API-HU Generator v2.0",
            Pipeline = "3 etapas (Limpieza → Estructuración → HU)",
            ProveedorIA = _aiProvider.NombreProveedor,
            Modelo = _aiProvider.ModeloActual
        });
    }

    // ============================================
    // Lógica común reutilizada por los dos endpoints de generación
    // ============================================
    private async Task<ActionResult<GenerarHUResponse>> EjecutarGeneracionAsync(
        GenerarHURequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Generación HUs - Proyecto: {Proyecto}, Caracteres: {Caracteres}",
                request.Proyecto ?? "Sin proyecto",
                request.Texto?.Length ?? 0);

            if (!ModelState.IsValid)
            {
                var errores = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new ErrorResponse
                {
                    Tipo = "ValidationError",
                    Mensaje = "Error de validación en el request",
                    Detalle = string.Join("; ", errores)
                });
            }

            var resultado = await _generacionService.GenerarHUsAsync(request, cancellationToken);

            if (!resultado.Exitoso)
            {
                return BadRequest(new ErrorResponse
                {
                    Tipo = "GenerationError",
                    Mensaje = resultado.Mensaje
                });
            }

            return Ok(resultado);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("La operación fue cancelada");
            return StatusCode(499, new ErrorResponse
            {
                Tipo = "Cancelled",
                Mensaje = "La operación fue cancelada por el cliente"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno al generar HUs");
            return StatusCode(500, new ErrorResponse
            {
                Tipo = "InternalError",
                Mensaje = "Error interno del servidor",
                Detalle = ex.Message
            });
        }
    }
}
