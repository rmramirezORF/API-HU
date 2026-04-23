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
[Produces("application/json")]
public class HUController : ControllerBase
{
    private readonly IGeneracionHUService _generacionService;
    private readonly IPromptService _promptService;
    private readonly IAIProviderService _aiProvider;
    private readonly ILogger<HUController> _logger;

    public HUController(
        IGeneracionHUService generacionService,
        IPromptService promptService,
        IAIProviderService aiProvider,
        ILogger<HUController> logger)
    {
        _generacionService = generacionService;
        _promptService = promptService;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    /// <summary>
    /// Genera Historias de Usuario a partir de texto
    /// </summary>
    /// <remarks>
    /// Este endpoint recibe un texto (transcripción de reunión o requerimientos) y utiliza inteligencia artificial para generar Historias de Usuario estructuradas.
    /// 
    /// El procesamiento se realiza en un pipeline de 3 etapas:
    /// 1. **Limpieza**: Elimina ruido y normaliza el texto
    /// 2. **Estructuración**: Identifica requerimientos y funcionalidades
    /// 3. **Generación**: Crea las HUs con criterios y tareas
    /// 
    /// **Ejemplo de Request:**
    /// ```json
    /// {
    ///   "texto": "Reunión con el cliente: Necesitamos un sistema de gestión de inventario que permita registrar productos, controlar stock mínimo, generar alertas cuando el inventario baje del umbral y generar reportes de movimientos.",
    ///   "proyecto": "Sistema de Inventario v1.0",
    ///   "maximoHUs": 5,
    ///   "idioma": "es",
    ///   "versionPrompt": "v1"
    /// }
    /// ```
    /// </remarks>
    /// <param name="request">Request con el texto a procesar</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Lista de Historias de Usuario generadas</returns>
    /// <response code="200">Historias de usuario generadas exitosamente</response>
    /// <response code="400">Error en la validación del request</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerarHUResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GenerarHUResponse>> GenerarHU(
        [FromBody] GenerarHURequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Recibida solicitud de generación de HUs para proyecto: {Proyecto}", 
                request.Proyecto ?? "Sin proyecto");

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

    /// <summary>
    /// Genera y guarda Historias de Usuario en la base de datos
    /// </summary>
    /// <param name="request">Request con el texto a procesar</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Lista de IDs de las HUs guardadas</returns>
    [HttpPost("generate-and-save")]
    [ProducesResponseType(typeof(GenerarHUResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GenerarHUResponse>> GenerarYGuardarHU(
        [FromBody] GenerarHURequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Tipo = "ValidationError",
                    Mensaje = "Error de validación en el request"
                });
            }

            var resultado = await _generacionService.GenerarYGuardarHUsAsync(request, cancellationToken);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar y guardar HUs");
            return StatusCode(500, new ErrorResponse
            {
                Tipo = "InternalError",
                Mensaje = "Error interno del servidor",
                Detalle = ex.Message
            });
        }
    }

    /// <summary>
    /// Obtiene las versiones de prompts disponibles
    /// </summary>
    /// <returns>Lista de versiones disponibles</returns>
    [HttpGet("prompts/versions")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public ActionResult<List<string>> ObtenerVersionesPrompt()
    {
        var versiones = _promptService.ObtenerVersionesDisponibles();
        return Ok(versiones);
    }

    /// <summary>
    /// Health check del servicio
    /// </summary>
    /// <returns>Estado del servicio</returns>
    [HttpGet("health")]
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
}