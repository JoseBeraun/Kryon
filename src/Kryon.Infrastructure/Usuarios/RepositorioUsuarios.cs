using Kryon.Core.Usuarios;
using Kryon.Infrastructure.Persistencia;

using Microsoft.EntityFrameworkCore;

namespace Kryon.Infrastructure.Usuarios;

/// <summary>
/// Persistencia de usuarios para las operaciones de negocio sobre <see cref="KryonDbContext"/>: todas pasan por los
/// filtros globales por empresa, el <c>SESSION_CONTEXT</c> y la RLS, y nunca usan <c>IgnoreQueryFilters()</c>.
/// <see cref="Usuario.Version"/> es el token de concurrencia: si otra operación cambió el usuario desde que se leyó,
/// <see cref="ActualizarAsync"/> lanza <c>usuario-modificado</c> (FR-038).
/// </summary>
/// <param name="contexto">Contexto de la solicitud; comparte conexión y transacción con el resto del caso de uso.</param>
/// <param name="capacidadAdministrar">
/// Nombre configurado de la capacidad de acceso a la gestión de usuarios (provisionalmente <c>usuarios.administrar</c>):
/// el mismo que usa la política <c>Usuarios.Acceder</c>. Define quién es administrador (FR-036).
/// </param>
public sealed class RepositorioUsuarios(KryonDbContext contexto, string? capacidadAdministrar) : IRepositorioUsuarios
{
    public Task<Usuario?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        contexto.Usuarios.SingleOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

    public async Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(capacidadAdministrar))
        {
            // Sin la capacidad configurada no se puede saber quién es administrador: falla cerrado.
            throw new InvalidOperationException("No está configurada la capacidad de acceso a la gestión de usuarios.");
        }

        // Las capacidades se guardan como JSON (no se pueden filtrar en SQL): se leen los roles de la empresa, que son
        // pocos, y se cuentan en SQL los usuarios Activos con un rol de administrador.
        var roles = await contexto.Set<RolReferencia>().AsNoTracking().ToListAsync(cancellationToken);
        var rolesAdministrador = roles
            .Where(r => r.Capacidades.Contains(capacidadAdministrar, StringComparer.Ordinal))
            .Select(r => r.Id)
            .ToArray();

        return await contexto.Usuarios.CountAsync(
            u => u.Estado == EstadoUsuario.Activo && rolesAdministrador.Contains(u.RolId),
            cancellationToken);
    }

    public async Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync(cancellationToken);
    }

    public async Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        try
        {
            await contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ErrorUsuarios(CodigoErrorUsuarios.UsuarioModificado, "Otra operación modificó el usuario desde que se leyó.");
        }
    }
}
