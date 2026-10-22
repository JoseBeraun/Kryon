using Kryon.Core.Usuarios;
using Kryon.Infrastructure.Persistencia;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kryon.Infrastructure.Usuarios;

/// <summary>
/// Lecturas de la Gestión de Usuarios sobre <see cref="KryonDbContext"/> (FR-014 a FR-018). Todas pasan por los filtros
/// globales por empresa del contexto, el <c>SESSION_CONTEXT</c> y la RLS: ningún método recibe una empresa y nunca se
/// usa <c>IgnoreQueryFilters()</c>, así que un usuario o un rol de otra empresa se comporta como inexistente.
/// </summary>
/// <remarks>
/// La búsqueda es un <c>LIKE</c> parametrizado con escape explícito: <c>%</c>, <c>_</c> y <c>[</c> escritos por el
/// usuario se buscan como texto. Las mayúsculas y los acentos siguen la intercalación de la base de datos. Las
/// consultas son de solo lectura (<c>AsNoTracking</c>).
/// </remarks>
public sealed class ConsultaUsuarios(KryonDbContext contexto, IOptions<OpcionesUsuarios> opciones) : IConsultaUsuarios
{
    private const char Escape = '\\';

    public async Task<PaginaConsultaUsuarios> BuscarAsync(CriteriosConsultaUsuarios criterios, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        // Precondiciones: la validación con mensajes por campo la hace el caso de uso; aquí solo se falla cerrado.
        var paginacion = opciones.Value.Paginacion;
        var tamanoPagina = criterios.TamanoPagina ?? paginacion.TamanoPorDefecto;
        ArgumentOutOfRangeException.ThrowIfLessThan(criterios.Pagina, 1, nameof(criterios));
        ArgumentOutOfRangeException.ThrowIfLessThan(tamanoPagina, 1, nameof(criterios));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tamanoPagina, paginacion.TamanoMaximo, nameof(criterios));
        var desplazamiento = (long)(criterios.Pagina - 1) * tamanoPagina;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(desplazamiento, int.MaxValue, nameof(criterios));

        var usuarios = contexto.Usuarios.AsNoTracking();

        if (!string.IsNullOrEmpty(criterios.Texto))
        {
            var patron = $"%{EscaparLike(criterios.Texto)}%";
            usuarios = usuarios.Where(u =>
                EF.Functions.Like(u.NombreCompleto, patron, Escape.ToString())
                || EF.Functions.Like(u.IdentificadorAcceso, patron, Escape.ToString()));
        }

        if (criterios.RolId is { } rolId)
        {
            usuarios = usuarios.Where(u => u.RolId == rolId);
        }

        if (criterios.Estado is { } estado)
        {
            usuarios = usuarios.Where(u => u.Estado == estado);
        }

        // El total sale de la misma consulta filtrada, antes de paginar.
        var total = await usuarios.CountAsync(cancellationToken);

        var pagina = await ConRol(usuarios.OrderBy(u => u.NombreCompleto).ThenBy(u => u.Id))
            .Skip((int)desplazamiento)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);

        return new PaginaConsultaUsuarios(
            [.. pagina.Select(fila => new UsuarioConRol(fila.Usuario, fila.Rol))],
            total,
            criterios.Pagina,
            tamanoPagina);
    }

    public async Task<UsuarioConRol?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var fila = await ConRol(contexto.Usuarios.AsNoTracking().Where(u => u.Id == usuarioId))
            .SingleOrDefaultAsync(cancellationToken);

        return fila is null ? null : new UsuarioConRol(fila.Usuario, fila.Rol);
    }

    public async Task<IReadOnlyList<RolReferencia>> ListarRolesAsignablesAsync(CancellationToken cancellationToken) =>
        await contexto.Set<RolReferencia>().AsNoTracking().ToListAsync(cancellationToken);

    public Task<RolReferencia?> ObtenerRolAsync(Guid rolId, CancellationToken cancellationToken) =>
        contexto.Set<RolReferencia>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == rolId, cancellationToken);

    /// <summary>
    /// Une cada usuario con su rol mediante <c>LEFT JOIN</c>: si el rol no existe o no es visible para la empresa
    /// actual, el usuario se conserva con rol null (EC-10). Respeta el orden de <paramref name="usuarios"/>.
    /// </summary>
    private IQueryable<FilaUsuario> ConRol(IQueryable<Usuario> usuarios) =>
        from usuario in usuarios
        join rol in contexto.Set<RolReferencia>().AsNoTracking() on usuario.RolId equals rol.Id into roles
        from rol in roles.DefaultIfEmpty()
        select new FilaUsuario(usuario, rol);

    /// <summary>Escapa el carácter de escape y los metacaracteres de <c>LIKE</c> de SQL Server (<c>%</c>, <c>_</c>, <c>[</c>).</summary>
    private static string EscaparLike(string texto) => texto
        .Replace(Escape.ToString(), $"{Escape}{Escape}", StringComparison.Ordinal)
        .Replace("%", $"{Escape}%", StringComparison.Ordinal)
        .Replace("_", $"{Escape}_", StringComparison.Ordinal)
        .Replace("[", $"{Escape}[", StringComparison.Ordinal);

    private sealed record FilaUsuario(Usuario Usuario, RolReferencia? Rol);
}
