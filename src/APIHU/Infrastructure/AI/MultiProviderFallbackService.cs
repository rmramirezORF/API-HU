using APIHU.Domain.Interfaces;

namespace APIHU.Infrastructure.AI;

/// <summary>
/// Envuelve múltiples proveedores de IA y los prueba en orden.
/// Si el primero falla, cae al siguiente y así sucesivamente.
/// Expone como ModeloActual/UltimoUso los del proveedor que efectivamente respondió.
/// </summary>
public class MultiProviderFallbackService : IAIProviderService
{
    private readonly IReadOnlyList<IAIProviderService> _proveedores;
    private readonly ILogger<MultiProviderFallbackService> _logger;

    /// <summary>
    /// Último proveedor que respondió con éxito (o null si aún no se ha llamado)
    /// </summary>
    private IAIProviderService? _ultimoExitoso;

    public string NombreProveedor => _ultimoExitoso?.NombreProveedor
        ?? $"MultiProvider[{string.Join(",", _proveedores.Select(p => p.NombreProveedor))}]";

    public string ModeloActual => _ultimoExitoso?.ModeloActual
        ?? (_proveedores.Count > 0 ? _proveedores[0].ModeloActual : "unknown");

    public UsoTokens UltimoUso => _ultimoExitoso?.UltimoUso ?? UsoTokens.Vacio;

    public MultiProviderFallbackService(
        IReadOnlyList<IAIProviderService> proveedores,
        ILogger<MultiProviderFallbackService> logger)
    {
        _proveedores = proveedores;
        _logger = logger;

        if (_proveedores.Count == 0)
        {
            throw new InvalidOperationException(
                "MultiProviderFallbackService requiere al menos un proveedor");
        }

        _logger.LogInformation(
            "MultiProviderFallbackService iniciado con {Count} proveedor(es): {Nombres}",
            _proveedores.Count,
            string.Join(" → ", _proveedores.Select(p => $"{p.NombreProveedor} ({p.ModeloActual})")));
    }

    public async Task<string> EnviarPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var excepciones = new List<(string proveedor, Exception ex)>();

        for (var i = 0; i < _proveedores.Count; i++)
        {
            var p = _proveedores[i];
            var total = _proveedores.Count;
            var esFallback = i > 0;

            try
            {
                if (esFallback)
                {
                    _logger.LogWarning(
                        "⟲ Fallback a proveedor {N}/{Total}: {Nombre} ({Modelo})",
                        i + 1, total, p.NombreProveedor, p.ModeloActual);
                }
                else
                {
                    _logger.LogInformation(
                        "▶ Proveedor principal {N}/{Total}: {Nombre} ({Modelo})",
                        i + 1, total, p.NombreProveedor, p.ModeloActual);
                }

                var respuesta = await p.EnviarPromptAsync(prompt, cancellationToken);

                _ultimoExitoso = p;

                if (esFallback)
                {
                    _logger.LogWarning(
                        "✓ Recuperado con fallback: {Nombre} ({Modelo}) tras {FallidosCount} fallo(s)",
                        p.NombreProveedor, p.ModeloActual, i);
                }

                return respuesta;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                excepciones.Add((p.NombreProveedor, ex));
                _logger.LogError(
                    "✖ Proveedor {Nombre} falló: {Mensaje}. {Restantes} proveedor(es) restante(s).",
                    p.NombreProveedor, ex.Message, total - i - 1);
            }
        }

        // Todos fallaron
        var resumen = string.Join(" | ", excepciones.Select(e => $"{e.proveedor}: {e.ex.Message}"));
        _logger.LogError(
            "✖✖ TODOS los {Count} proveedores fallaron. Resumen: {Resumen}",
            _proveedores.Count, resumen);

        throw new InvalidOperationException(
            $"Todos los proveedores configurados fallaron ({_proveedores.Count}). Detalle: {resumen}",
            excepciones.LastOrDefault().ex);
    }
}
