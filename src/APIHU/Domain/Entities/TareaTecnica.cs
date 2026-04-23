using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIHU.Domain.Entities;

/// <summary>
/// Tarea técnica de una Historia de Usuario
/// </summary>
[Table("TareasTecnicas")]
public class TareaTecnica
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int HistoriaUsuarioId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Tipo { get; set; }

    public int Orden { get; set; }

    public bool EstaCompletada { get; set; } = false;

    [ForeignKey("HistoriaUsuarioId")]
    public virtual HistoriaUsuario? HistoriaUsuario { get; set; }
}