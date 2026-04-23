using APIHU.Domain.Entities;

namespace APIHU.Domain.Interfaces;

/// <summary>
/// Interfaz para el repositorio de Historias de Usuario
/// </summary>
public interface IHistoriaUsuarioRepository
{
    Task<HistoriaUsuario> AgregarAsync(HistoriaUsuario entidad, CancellationToken cancellationToken = default);
    Task<HistoriaUsuario?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<HistoriaUsuario>> ObtenerPorGeneracionIdAsync(int generacionId, CancellationToken cancellationToken = default);
    Task<List<HistoriaUsuario>> ObtenerTodosAsync(int pagina = 1, int tamanoPagina = 20, CancellationToken cancellationToken = default);
    Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interfaz para el repositorio de Generaciones HU
/// </summary>
public interface IGeneracionRepository
{
    Task<GeneracionHU> AgregarAsync(GeneracionHU entidad, CancellationToken cancellationToken = default);
    Task<GeneracionHU?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<GeneracionHU>> ObtenerTodosAsync(int pagina = 1, int tamanoPagina = 20, CancellationToken cancellationToken = default);
    Task<bool> ActualizarAsync(GeneracionHU entidad, CancellationToken cancellationToken = default);
}