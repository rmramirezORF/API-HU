namespace APIHU.Domain.Entities;

/// <summary>
/// Entidad base con propiedades comunes
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaModificacion { get; set; }
}