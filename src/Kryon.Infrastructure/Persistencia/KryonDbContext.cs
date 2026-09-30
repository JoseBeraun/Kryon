using Kryon.Core.Usuarios;

using Microsoft.EntityFrameworkCore;

namespace Kryon.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core de Kryon. El proveedor y la cadena de conexión llegan por <see cref="DbContextOptions"/>
/// desde la composición de la aplicación; este contexto no fija ninguno.
/// </summary>
public sealed class KryonDbContext(DbContextOptions<KryonDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KryonDbContext).Assembly);
    }
}
