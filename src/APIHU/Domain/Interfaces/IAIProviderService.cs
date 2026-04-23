namespace APIHU.Domain.Interfaces;

/// <summary>
/// Uso de tokens reportado por el proveedor tras la última llamada
/// </summary>
public record UsoTokens(int InputTokens, int OutputTokens)
{
    public int Total => InputTokens + OutputTokens;
    public static UsoTokens Vacio => new(0, 0);
}

/// <summary>
/// Interfaz para servicios de IA generativa
/// </summary>
public interface IAIProviderService
{
    /// <summary>
    /// Envía un prompt y obtiene la respuesta como texto
    /// </summary>
    Task<string> EnviarPromptAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nombre del proveedor de IA
    /// </summary>
    string NombreProveedor { get; }

    /// <summary>
    /// Modelo actualmente configurado
    /// </summary>
    string ModeloActual { get; }

    /// <summary>
    /// Uso de tokens de la última llamada realizada en este scope
    /// </summary>
    UsoTokens UltimoUso { get; }
}
