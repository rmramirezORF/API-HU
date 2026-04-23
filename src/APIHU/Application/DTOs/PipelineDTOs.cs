namespace APIHU.Application.DTOs;

/// <summary>
/// Resultado de la etapa de limpieza de texto
/// </summary>
public class ResultadoLimpieza
{
    public string TextoLimpio { get; set; } = string.Empty;
    public List<string> ElementosEliminados { get; set; } = new();
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Resultado de la etapa de estructuración
/// </summary>
public class ResultadoEstructuracion
{
    public List<Requerimiento> Requerimientos { get; set; } = new();
    public string Resumen { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Requerimiento identificado durante el análisis
/// </summary>
public class Requerimiento
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public List<string> PalabrasClave { get; set; } = new();
}

/// <summary>
/// Resultado completo del pipeline (usado por HuProcessingOrchestrator)
/// </summary>
public class ResultadoPipeline
{
    /// <summary>
    /// Correlation ID para trazabilidad
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Resultado de la etapa de limpieza
    /// </summary>
    public ResultadoLimpieza? Limpieza { get; set; }

    /// <summary>
    /// Duración de la etapa de limpieza en milisegundos
    /// </summary>
    public long LimpiezaDuracionMs { get; set; }

    /// <summary>
    /// Resultado de la etapa de estructuración
    /// </summary>
    public ResultadoEstructuracion? Estructuracion { get; set; }

    /// <summary>
    /// Duración de la etapa de estructuración en milisegundos
    /// </summary>
    public long EstructuracionDuracionMs { get; set; }

    /// <summary>
    /// Historias de usuario generadas
    /// </summary>
    public List<HistoriaUsuarioResponse>? HistoriasUsuario { get; set; }

    /// <summary>
    /// Duración de la etapa de generación en milisegundos
    /// </summary>
    public long GeneracionDuracionMs { get; set; }

    /// <summary>
    /// Indica si el pipeline se ejecutó exitosamente
    /// </summary>
    public bool Exitoso { get; set; }

    /// <summary>
    /// Mensaje de error si falló el pipeline
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Tiempo total de ejecución del pipeline
    /// </summary>
    public TimeSpan? TiempoTotal { get; set; }

    /// <summary>
    /// Tokens de entrada acumulados a través de las 3 etapas
    /// </summary>
    public int TokensInput { get; set; }

    /// <summary>
    /// Tokens de salida acumulados a través de las 3 etapas
    /// </summary>
    public int TokensOutput { get; set; }

    /// <summary>
    /// Suma total de tokens (input + output)
    /// </summary>
    public int TokensTotales => TokensInput + TokensOutput;
}