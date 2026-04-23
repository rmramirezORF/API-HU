using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIHU.Domain.Entities;

/// <summary>
/// Estados posibles de una generación de HUs
/// </summary>
public enum EstadoGeneracion
{
    Procesando = 0,
    Completado = 1,
    Error = 2
}

/// <summary>
/// Registro de una generación de Historias de Usuario
/// </summary>
[Table("GeneracionesHU")]
public class GeneracionHU : BaseEntity
{
    [Required]
    [MaxLength(10000)]
    public string TextoEntrada { get; set; } = string.Empty;

    [MaxLength(15000)]
    public string? TextoProcesado { get; set; }

    [MaxLength(100)]
    public string? Proyecto { get; set; }

    [MaxLength(10)]
    public string Idioma { get; set; } = "es";

    public int TotalHUs { get; set; }

    public bool Exitoso { get; set; }

    [MaxLength(500)]
    public string? MensajeError { get; set; }

    [MaxLength(50)]
    public string? PromptVersion { get; set; }

    // ============================================
    // Campos adicionales para producción v2.0
    // ============================================

    /// <summary>
    /// Estado actual del procesamiento
    /// </summary>
    public EstadoGeneracion Estado { get; set; } = EstadoGeneracion.Procesando;

    /// <summary>
    /// Duración del procesamiento en milisegundos
    /// </summary>
    public int DuracionMs { get; set; }

    /// <summary>
    /// Modelo de IA utilizado
    /// </summary>
    [MaxLength(50)]
    public string? ModeloIA { get; set; }

    /// <summary>
    /// Tokens consumidos en la generación (opcional)
    /// </summary>
    public int? TokensConsumidos { get; set; }

    /// <summary>
    /// Correlation ID para trazabilidad
    /// </summary>
    [MaxLength(50)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// IP del cliente que hizo la request
    /// </summary>
    [MaxLength(50)]
    public string? ClientIP { get; set; }

    /// <summary>
    /// User Agent del cliente
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    // Colección de HUs generadas
    public virtual ICollection<HistoriaUsuario> HistoriasUsuario { get; set; } = new List<HistoriaUsuario>();
}