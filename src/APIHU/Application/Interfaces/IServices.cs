using APIHU.Application.DTOs;

namespace APIHU.Application.Interfaces;

/// <summary>
/// Interfaz para el servicio de generación de HUs
/// </summary>
public interface IGeneracionHUService
{
    /// <summary>
    /// Genera HUs a partir de texto usando el pipeline completo
    /// </summary>
    Task<GenerarHUResponse> GenerarHUsAsync(GenerarHURequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera y guarda HUs en la base de datos
    /// </summary>
    Task<GenerarHUResponse> GenerarYGuardarHUsAsync(GenerarHURequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interfaz para el servicio de pipeline de procesamiento
/// </summary>
public interface IPipelineProcesamientoService
{
    /// <summary>
    /// Ejecuta el pipeline completo de procesamiento
    /// </summary>
    Task<ResultadoPipeline> EjecutarPipelineAsync(GenerarHURequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interfaz para el servicio de gestión de prompts
/// </summary>
public interface IPromptService
{
    /// <summary>
    /// Obtiene el prompt de limpieza
    /// </summary>
    string ObtenerPromptLimpieza(string version = "v1");

    /// <summary>
    /// Obtiene el prompt de estructuración
    /// </summary>
    string ObtenerPromptEstructuracion(string version = "v1");

    /// <summary>
    /// Obtiene el prompt de generación de HUs
    /// </summary>
    string ObtenerPromptHU(string version = "v1");

    /// <summary>
    /// Obtiene las versiones disponibles de prompts
    /// </summary>
    List<string> ObtenerVersionesDisponibles();
}