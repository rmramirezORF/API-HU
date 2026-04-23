using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIHU.Domain.Entities;

/// <summary>
/// Criterio de aceptación de una Historia de Usuario
/// </summary>
[Table("CriteriosAceptacion")]
public class CriterioAceptacion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int HistoriaUsuarioId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    public int Orden { get; set; }

    public bool EsObligatorio { get; set; } = true;

    [ForeignKey("HistoriaUsuarioId")]
    public virtual HistoriaUsuario? HistoriaUsuario { get; set; }
}