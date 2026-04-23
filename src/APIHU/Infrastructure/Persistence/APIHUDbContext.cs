using Microsoft.EntityFrameworkCore;
using APIHU.Domain.Entities;

namespace APIHU.Infrastructure.Persistence;

/// <summary>
/// DbContext para la base de datos de Historias de Usuario
/// </summary>
public class APIHUDbContext : DbContext
{
    public APIHUDbContext(DbContextOptions<APIHUDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<HistoriaUsuario> HistoriasUsuario { get; set; }
    public DbSet<CriterioAceptacion> CriteriosAceptacion { get; set; }
    public DbSet<TareaTecnica> TareasTecnicas { get; set; }
    public DbSet<GeneracionHU> GeneracionesHU { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ============================================
        // HistoriaUsuario
        // ============================================
        modelBuilder.Entity<HistoriaUsuario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Titulo).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Como).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Quiero).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Para).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Descripcion).HasMaxLength(2000);
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("GETUTCDATE()");

            // Relación con GeneracionHU
            entity.HasOne(e => e.GeneracionHU)
                .WithMany(g => g.HistoriasUsuario)
                .HasForeignKey(e => e.GeneracionHUId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================
        // CriterioAceptacion
        // ============================================
        modelBuilder.Entity<CriterioAceptacion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Descripcion).IsRequired().HasMaxLength(500);
            entity.Property(e => e.EsObligatorio).HasDefaultValue(true);

            entity.HasOne(e => e.HistoriaUsuario)
                .WithMany(h => h.CriteriosAceptacion)
                .HasForeignKey(e => e.HistoriaUsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================
        // TareaTecnica
        // ============================================
        modelBuilder.Entity<TareaTecnica>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Descripcion).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Tipo).HasMaxLength(50);
            entity.Property(e => e.EstaCompletada).HasDefaultValue(false);

            entity.HasOne(e => e.HistoriaUsuario)
                .WithMany(h => h.TareasTecnicas)
                .HasForeignKey(e => e.HistoriaUsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================
        // GeneracionHU
        // ============================================
        modelBuilder.Entity<GeneracionHU>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TextoEntrada).IsRequired().HasMaxLength(10000);
            entity.Property(e => e.TextoProcesado).HasMaxLength(15000);
            entity.Property(e => e.Proyecto).HasMaxLength(100);
            entity.Property(e => e.Idioma).HasMaxLength(10).HasDefaultValue("es");
            entity.Property(e => e.TotalHUs).HasDefaultValue(0);
            entity.Property(e => e.Exitoso).HasDefaultValue(false);
            entity.Property(e => e.MensajeError).HasMaxLength(500);
            entity.Property(e => e.PromptVersion).HasMaxLength(20);
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}