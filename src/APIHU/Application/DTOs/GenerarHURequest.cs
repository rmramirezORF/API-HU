using System.ComponentModel.DataAnnotations;

namespace APIHU.Application.DTOs;

/// <summary>
/// Request para generar Historias de Usuario
/// </summary>
public class GenerarHURequest
{
    [Required(ErrorMessage = "El texto de entrada es requerido")]
    [MinLength(20, ErrorMessage = "El texto debe tener al menos 20 caracteres")]
    [MaxLength(10000, ErrorMessage = "El texto no puede exceder 10000 caracteres")]
    public string Texto { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Proyecto { get; set; }

    [Range(1, 20, ErrorMessage = "El número de HUs debe estar entre 1 y 20")]
    public int? MaximoHUs { get; set; }

    [MaxLength(10)]
    public string? Idioma { get; set; } = "es";

    [MaxLength(20)]
    public string? VersionPrompt { get; set; }
}