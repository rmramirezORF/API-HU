using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIHU.Domain.Entities;

/// <summary>
/// Entidad que representa una Historia de Usuario
/// </summary>
[Table("HistoriasUsuario")]
public class HistoriaUsuario : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Como { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Quiero { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Para { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    // Relación con Generación
    public int? GeneracionHUId { get; set; }
    
    [ForeignKey("GeneracionHUId")]
    public GeneracionHU? GeneracionHU { get; set; }

    // Colecciones
    public virtual ICollection<CriterioAceptacion> CriteriosAceptacion { get; set; } = new List<CriterioAceptacion>();
    public virtual ICollection<TareaTecnica> TareasTecnicas { get; set; } = new List<TareaTecnica>();
}