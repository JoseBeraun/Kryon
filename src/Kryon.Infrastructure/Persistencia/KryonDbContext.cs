using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;

using Microsoft.EntityFrameworkCore;

namespace Kryon.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core de Kryon. El proveedor y la cadena de conexión llegan por <see cref="DbContextOptions"/>
/// desde la composición de la aplicación; este contexto no fija ninguno.
/// </summary>
/// <remarks>
/// Primera capa de aislamiento multiempresa (research §R3): cada entidad de empresa tiene un filtro de consulta
/// global por <see cref="EmpresaActual"/>, que sale solo del <see cref="IContextoSolicitud"/> verificado.
/// </remarks>
public sealed class KryonDbContext(DbContextOptions<KryonDbContext> options, IContextoSolicitud contextoSolicitud)
    : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    /// <summary>
    /// Empresa de la solicitud. Los filtros la referencian como miembro de este contexto, así que EF Core la
    /// parametriza y la lee de cada instancia en cada consulta aunque el modelo se cree una sola vez.
    /// </summary>
    private Guid EmpresaActual => contextoSolicitud.EmpresaId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KryonDbContext).Assembly);

        modelBuilder.Entity<Usuario>().HasQueryFilter(u => u.EmpresaId == EmpresaActual);
        modelBuilder.Entity<RolReferencia>().HasQueryFilter(r => r.EmpresaId == EmpresaActual);
        modelBuilder.Entity<AuditoriaUsuario>().HasQueryFilter(a => a.EmpresaId == EmpresaActual);
    }
}
