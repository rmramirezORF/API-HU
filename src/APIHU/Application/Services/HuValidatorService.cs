using APIHU.Application.DTOs;

namespace APIHU.Application.Services;

/// <summary>
/// Validador de Historias de Usuario
/// Valida estructura, criterios, duplicados y coherencia
/// </summary>
public class HuValidatorService : IHuValidatorService
{
    private readonly ILogger<HuValidatorService> _logger;

    public HuValidatorService(ILogger<HuValidatorService> logger)
    {
        _logger = logger;
    }

    public async Task<ValidacionResultado> ValidarHistoriasAsync(
        List<HistoriaUsuarioResponse> historias,
        CancellationToken cancellationToken = default)
    {
        var resultado = new ValidacionResultado();
        
        if (historias == null || historias.Count == 0)
        {
            resultado.EsValido = false;
            resultado.Advertencias.Add("No hay historias de usuario para validar");
            return resultado;
        }

        _logger.LogInformation("Iniciando validación de {Cantidad} historias de usuario", historias.Count);

        // Validar cada HU
        foreach (var hu in historias)
        {
            var validacionHU = await ValidarHistoriaIndividualAsync(hu, cancellationToken);
            
            if (validacionHU.EsValida)
            {
                resultado.HistoriasValidas.Add(hu);
            }
            else
            {
                resultado.HistoriasInvalidas.Add(hu);
                resultado.Advertencias.AddRange(validacionHU.Errores);
            }
        }

        // Verificar duplicados
        var duplicados = DetectarDuplicados(historias);
        if (duplicados.Any())
        {
            resultado.Advertencias.Add($"Se detectaron {duplicados.Count} historias duplicadas");
            // Remover duplicados, mantener el primero
            resultado.HistoriasValidas = resultado.HistoriasValidas
                .Where((hu, index) => !duplicados.Contains(index))
                .ToList();
        }

        resultado.EsValido = resultado.HistoriasValidas.Count > 0;

        _logger.LogInformation(
            "Validación completada. Válidas: {Validas}, Inválidas: {Invalidas}, Advertencias: {Advertencias}",
            resultado.HistoriasValidas.Count,
            resultado.HistoriasInvalidas.Count,
            resultado.Advertencias.Count);

        return resultado;
    }

    public async Task<ValidacionHU> ValidarHistoriaIndividualAsync(
        HistoriaUsuarioResponse hu,
        CancellationToken cancellationToken = default)
    {
        var validacion = new ValidacionHU { EsValida = true };

        // 1. Validar estructura - Aceptar formato completo O solo descripción
        // (Para modelos pequeños como tinyllama que no generan formato estructurado)
        bool tieneFormatoEstructurado = !string.IsNullOrWhiteSpace(hu.Como) 
            && !string.IsNullOrWhiteSpace(hu.Quiero) 
            && !string.IsNullOrWhiteSpace(hu.Para);
        
        bool tieneDescripcion = !string.IsNullOrWhiteSpace(hu.Descripcion) 
            || !string.IsNullOrWhiteSpace(hu.Titulo);

        if (!tieneFormatoEstructurado && !tieneDescripcion)
        {
            validacion.EsValida = false;
            validacion.Errores.Add("Falta información de la historia (necesita Como/Quiero/Para o Descripción/Título)");
        }

        // 2. Validar que tenga criterios de aceptación O descripción
        if ((hu.CriteriosAceptacion == null || hu.CriteriosAceptacion.Count == 0) 
            && string.IsNullOrWhiteSpace(hu.Descripcion))
        {
            validacion.EsValida = false;
            validacion.Errores.Add("La historia no tiene criterios de aceptación ni descripción");
        }
        else if (hu.CriteriosAceptacion != null && hu.CriteriosAceptacion.Count > 0)
        {
            // Verificar que los criterios no estén vacíos
            var criteriosVacios = hu.CriteriosAceptacion
                .Where(c => string.IsNullOrWhiteSpace(c.Descripcion))
                .ToList();
            
            if (criteriosVacios.Any())
            {
                validacion.Advertencias.Add($"Hay {criteriosVacios.Count} criterios de aceptación vacíos");
            }
        }

        // 3. Validar coherencia básica
        if (!string.IsNullOrWhiteSpace(hu.Titulo))
        {
            // El título no debe ser muy corto
            if (hu.Titulo.Length < 5)
            {
                validacion.Advertencias.Add("El título es muy corto");
            }

            // El título no debe ser muy largo
            if (hu.Titulo.Length > 200)
            {
                validacion.Advertencias.Add("El título es muy largo");
            }
        }

        // 4. Validar que el "Como" tenga sentido (debe empezar con "Como" o similar)
        if (!string.IsNullOrWhiteSpace(hu.Como) && !hu.Como.StartsWith("Como", StringComparison.OrdinalIgnoreCase))
        {
            validacion.Advertencias.Add("El campo 'Como' debería empezar con 'Como'");
        }

        // 5. Validar que el "Quiero" tenga sentido (debe empezar con "Quiero" o similar)
        if (!string.IsNullOrWhiteSpace(hu.Quiero) && !hu.Quiero.StartsWith("Quiero", StringComparison.OrdinalIgnoreCase))
        {
            validacion.Advertencias.Add("El campo 'Quiero' debería empezar con 'Quiero'");
        }

        // 6. Validar que el "Para" tenga sentido (debe empezar con "Para" o similar)
        if (!string.IsNullOrWhiteSpace(hu.Para) && !hu.Para.StartsWith("Para", StringComparison.OrdinalIgnoreCase))
        {
            validacion.Advertencias.Add("El campo 'Para' debería empezar con 'Para'");
        }

        return validacion;
    }

    private List<int> DetectarDuplicados(List<HistoriaUsuarioResponse> historias)
    {
        var duplicados = new List<int>();
        var seen = new HashSet<string>();

        for (int i = 0; i < historias.Count; i++)
        {
            var hu = historias[i];
            // Crear una clave única basada en los campos principales
            var key = $"{hu.Como?.ToLowerInvariant()}|{hu.Quiero?.ToLowerInvariant()}|{hu.Para?.ToLowerInvariant()}";
            
            if (seen.Contains(key))
            {
                duplicados.Add(i);
            }
            else
            {
                seen.Add(key);
            }
        }

        return duplicados;
    }
}

/// <summary>
/// Interfaz para el validador de HUs
/// </summary>
public interface IHuValidatorService
{
    /// <summary>
    /// Valida una lista de historias de usuario
    /// </summary>
    Task<ValidacionResultado> ValidarHistoriasAsync(
        List<HistoriaUsuarioResponse> historias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida una historia de usuario individual
    /// </summary>
    Task<ValidacionHU> ValidarHistoriaIndividualAsync(
        HistoriaUsuarioResponse hu,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de validación de una HU individual
/// </summary>
public class ValidacionHU
{
    public bool EsValida { get; set; }
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}

/// <summary>
/// Resultado de validación de múltiples HUs
/// </summary>
public class ValidacionResultado
{
    public bool EsValido { get; set; }
    public List<HistoriaUsuarioResponse> HistoriasValidas { get; set; } = new();
    public List<HistoriaUsuarioResponse> HistoriasInvalidas { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}