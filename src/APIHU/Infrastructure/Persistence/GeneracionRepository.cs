using Microsoft.EntityFrameworkCore;
using APIHU.Domain.Entities;
using APIHU.Domain.Interfaces;

namespace APIHU.Infrastructure.Persistence;

/// <summary>
/// Implementación del repositorio de Generaciones HU
/// </summary>
public class GeneracionRepository : IGeneracionRepository
{
    private readonly APIHUDbContext _context;
    private readonly ILogger<GeneracionRepository> _logger;

    public GeneracionRepository(APIHUDbContext context, ILogger<GeneracionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GeneracionHU> AgregarAsync(GeneracionHU entidad, CancellationToken cancellationToken = default)
    {
        _context.GeneracionesHU.Add(entidad);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Generación agregada con ID {Id}", entidad.Id);
        return entidad;
    }

    public async Task<GeneracionHU?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.GeneracionesHU
            .Include(g => g.HistoriasUsuario)
                .ThenInclude(h => h.CriteriosAceptacion)
            .Include(g => g.HistoriasUsuario)
                .ThenInclude(h => h.TareasTecnicas)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<List<GeneracionHU>> ObtenerTodosAsync(int pagina = 1, int tamanoPagina = 20, CancellationToken cancellationToken = default)
    {
        return await _context.GeneracionesHU
            .OrderByDescending(g => g.FechaCreacion)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ActualizarAsync(GeneracionHU entidad, CancellationToken cancellationToken = default)
    {
        var existente = await _context.GeneracionesHU.FindAsync(new object[] { entidad.Id }, cancellationToken);
        if (existente == null)
        {
            _logger.LogWarning("No se encontró generación con ID {Id} para actualizar", entidad.Id);
            return false;
        }

        _context.Entry(existente).CurrentValues.SetValues(entidad);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Generación actualizada con ID {Id}", entidad.Id);
        return true;
    }
}