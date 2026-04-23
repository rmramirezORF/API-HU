using Microsoft.EntityFrameworkCore;
using APIHU.Domain.Entities;
using APIHU.Domain.Interfaces;

namespace APIHU.Infrastructure.Persistence;

/// <summary>
/// Implementación del repositorio de Historias de Usuario
/// </summary>
public class HistoriaUsuarioRepository : IHistoriaUsuarioRepository
{
    private readonly APIHUDbContext _context;
    private readonly ILogger<HistoriaUsuarioRepository> _logger;

    public HistoriaUsuarioRepository(APIHUDbContext context, ILogger<HistoriaUsuarioRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HistoriaUsuario> AgregarAsync(HistoriaUsuario entidad, CancellationToken cancellationToken = default)
    {
        _context.HistoriasUsuario.Add(entidad);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("HU agregada con ID {Id}", entidad.Id);
        return entidad;
    }

    public async Task<HistoriaUsuario?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.HistoriasUsuario
            .Include(h => h.CriteriosAceptacion)
            .Include(h => h.TareasTecnicas)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<List<HistoriaUsuario>> ObtenerPorGeneracionIdAsync(int generacionId, CancellationToken cancellationToken = default)
    {
        return await _context.HistoriasUsuario
            .Where(h => h.GeneracionHUId == generacionId)
            .Include(h => h.CriteriosAceptacion)
            .Include(h => h.TareasTecnicas)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<HistoriaUsuario>> ObtenerTodosAsync(int pagina = 1, int tamanoPagina = 20, CancellationToken cancellationToken = default)
    {
        return await _context.HistoriasUsuario
            .Include(h => h.CriteriosAceptacion)
            .Include(h => h.TareasTecnicas)
            .OrderByDescending(h => h.FechaCreacion)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default)
    {
        var entidad = await _context.HistoriasUsuario.FindAsync(new object[] { id }, cancellationToken);
        if (entidad == null)
        {
            _logger.LogWarning("No se encontró HU con ID {Id} para eliminar", id);
            return false;
        }

        _context.HistoriasUsuario.Remove(entidad);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("HU eliminada con ID {Id}", id);
        return true;
    }
}