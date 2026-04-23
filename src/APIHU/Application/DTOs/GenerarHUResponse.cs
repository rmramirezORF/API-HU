namespace APIHU.Application.DTOs;

/// <summary>
/// Response de una Historia de Usuario
/// </summary>
public class HistoriaUsuarioResponse
{
    public string? Titulo { get; set; }
    public string? Como { get; set; }
    public string? Quiero { get; set; }
    public string? Para { get; set; }
    public string? Descripcion { get; set; }
    public List<CriterioAceptacionResponse>? CriteriosAceptacion { get; set; }
    public List<TareaTecnicaResponse>? TareasTecnicas { get; set; }
}

public class CriterioAceptacionResponse
{
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool EsObligatorio { get; set; } = true;
}

public class TareaTecnicaResponse
{
    public string? Descripcion { get; set; }
    public string? Tipo { get; set; }
    public int Orden { get; set; }
}

/// <summary>
/// Response del endpoint de generación
/// </summary>
public class GenerarHUResponse
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    
    /// <summary>
    /// Texto limpio resultado de la etapa de procesamiento
    /// </summary>
    public string? TextoLimpio { get; set; }
    
    public List<HistoriaUsuarioResponse> HistoriasUsuario { get; set; } = new();
    public GenerarHUMetadata? Metadata { get; set; }
    public int? GeneracionId { get; set; }
    
    /// <summary>
    /// Correlation ID para trazabilidad
    /// </summary>
    public string? CorrelationId { get; set; }
}

public class GenerarHUMetadata
{
    public DateTime FechaGeneracion { get; set; } = DateTime.UtcNow;
    public string? Proyecto { get; set; }
    public int TotalHUs { get; set; }
    public string? Idioma { get; set; }
    public string? VersionPrompt { get; set; }
    
    /// <summary>
    /// Duración del procesamiento en milisegundos
    /// </summary>
    public int DuracionMs { get; set; }
}

/// <summary>
/// Response de error estándar
/// </summary>
public class ErrorResponse
{
    public string Tipo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Correlation ID para trazabilidad
    /// </summary>
    public string? CorrelationId { get; set; }
}