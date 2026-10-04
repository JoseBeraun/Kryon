using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;
using Kryon.Infrastructure.Persistencia;

using Microsoft.EntityFrameworkCore;

namespace Kryon.Api.Tests.Infraestructura;

/// <summary>Rol de los datos semilla (tabla <c>Roles</c>, de solo lectura para la feature).</summary>
public sealed record RolSemilla(Guid Id, Guid EmpresaId, string Nombre, IReadOnlyList<string> Capacidades);

/// <summary>Usuario de los datos semilla con todo lo que se persiste; <see cref="Identidad"/> sirve para <c>ClienteComo</c>.</summary>
public sealed record UsuarioSembrado(Guid Id, string NombreCompleto, string IdentificadorAcceso, RolSemilla Rol, EstadoUsuario Estado)
{
    /// <summary>Identidad de prueba: su id, la empresa de su rol y las capacidades de su rol.</summary>
    public UsuarioSemilla Identidad => new(Id, Rol.EmpresaId, Rol.Capacidades);
}

/// <summary>
/// Datos semilla de quickstart.md (prerrequisitos) con identificadores fijos. <c>KryonApiFactory</c> los carga
/// después de las migraciones. Sin credenciales, tokens ni historial de auditoría.
/// </summary>
/// <remarks>
/// Lo que quickstart.md no fija (los Guid, los nombres de los roles, el identificador de "Ana Beta" y el rol sin
/// capacidades de gestión de carla, dario y "Ana Beta") son valores deterministas del fixture. Estos valores son
/// datos técnicos del fixture de pruebas y no definen el catálogo ni reglas de negocio de roles de Kryon.
/// </remarks>
public static class SemillaDatos
{
    // Nombres de permiso provisionales (research §R4), los mismos que asocia Seguridad:Permisos en appsettings.json.
    public const string Administrar = "usuarios.administrar";
    public const string Registrar = "usuarios.registrar";
    public const string Editar = "usuarios.editar";
    public const string Activar = "usuarios.activar";
    public const string Desactivar = "usuarios.desactivar";

    public static readonly IReadOnlyList<string> TodasLasCapacidades = [Administrar, Registrar, Editar, Activar, Desactivar];

    public static readonly Guid EmpresaA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    public static readonly Guid EmpresaB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");

    /// <summary>Rol con todas las capacidades de gestión de usuarios (ana).</summary>
    public static readonly RolSemilla RolAdministracionA =
        new(Guid.Parse("a1000000-0000-0000-0000-000000000001"), EmpresaA, "Administración", TodasLasCapacidades);

    /// <summary>
    /// Rol de A sin la capacidad de desactivar (beto), para las pruebas de acciones visibles (quickstart V6). Conserva
    /// <c>usuarios.administrar</c>, así que beto sigue siendo administrador: ana y beto son los únicos administradores
    /// activos de A (quickstart V7).
    /// </summary>
    public static readonly RolSemilla RolSinDesactivarA =
        new(Guid.Parse("a1000000-0000-0000-0000-000000000002"), EmpresaA, "Sin desactivar", [Administrar, Registrar, Editar, Activar]);

    /// <summary>Rol de A sin capacidades de gestión de usuarios (carla, dario).</summary>
    public static readonly RolSemilla RolSinGestionA =
        new(Guid.Parse("a1000000-0000-0000-0000-000000000003"), EmpresaA, "Sin gestión", []);

    /// <summary>Rol de B con todas las capacidades de gestión de usuarios (berta).</summary>
    public static readonly RolSemilla RolAdministracionB =
        new(Guid.Parse("b1000000-0000-0000-0000-000000000001"), EmpresaB, "Administración", TodasLasCapacidades);

    /// <summary>Rol de B sin capacidades de gestión de usuarios ("Ana Beta").</summary>
    public static readonly RolSemilla RolSinGestionB =
        new(Guid.Parse("b1000000-0000-0000-0000-000000000002"), EmpresaB, "Sin gestión", []);

    public static readonly UsuarioSembrado Ana =
        new(Guid.Parse("a2000000-0000-0000-0000-000000000001"), "Ana", "ana", RolAdministracionA, EstadoUsuario.Activo);

    public static readonly UsuarioSembrado Beto =
        new(Guid.Parse("a2000000-0000-0000-0000-000000000002"), "Beto", "beto", RolSinDesactivarA, EstadoUsuario.Activo);

    public static readonly UsuarioSembrado Carla =
        new(Guid.Parse("a2000000-0000-0000-0000-000000000003"), "Carla", "carla", RolSinGestionA, EstadoUsuario.Activo);

    public static readonly UsuarioSembrado Dario =
        new(Guid.Parse("a2000000-0000-0000-0000-000000000004"), "Dario", "dario", RolSinGestionA, EstadoUsuario.Inactivo);

    public static readonly UsuarioSembrado Berta =
        new(Guid.Parse("b2000000-0000-0000-0000-000000000001"), "Berta", "berta", RolAdministracionB, EstadoUsuario.Activo);

    /// <summary>Usuario de B que coincide con la búsqueda "Ana" desde la empresa A.</summary>
    public static readonly UsuarioSembrado AnaBeta =
        new(Guid.Parse("b2000000-0000-0000-0000-000000000002"), "Ana Beta", "ana.beta", RolSinGestionB, EstadoUsuario.Activo);

    public static readonly IReadOnlyList<RolSemilla> Roles =
        [RolAdministracionA, RolSinDesactivarA, RolSinGestionA, RolAdministracionB, RolSinGestionB];

    public static readonly IReadOnlyList<UsuarioSembrado> Usuarios = [Ana, Beto, Carla, Dario, Berta, AnaBeta];

    /// <summary>Momento fijo de creación de los usuarios sembrados (UTC).</summary>
    public static readonly DateTimeOffset Momento = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Inserta los datos semilla. Cada empresa se siembra con su propio <see cref="KryonDbContext"/> y su propio
    /// <see cref="ContextoEmpresaInterceptor"/>, así que cada conexión fija el <c>SESSION_CONTEXT</c> de esa empresa
    /// y la RLS (T025) acepta las inserciones sin desactivarla ni saltársela.
    /// </summary>
    public static async Task SembrarAsync(string cadenaConexion, CancellationToken cancellationToken = default)
    {
        foreach (var empresa in new[] { EmpresaA, EmpresaB })
        {
            var contextoEmpresa = new ContextoSemilla(empresa);
            var opciones = new DbContextOptionsBuilder<KryonDbContext>()
                .UseSqlServer(cadenaConexion)
                .AddInterceptors(new ContextoEmpresaInterceptor(contextoEmpresa))
                .Options;

            await using var contexto = new KryonDbContext(opciones, contextoEmpresa);

            foreach (var rol in Roles.Where(r => r.EmpresaId == empresa))
            {
                contexto.Add(new RolReferencia(rol.Id, rol.EmpresaId, rol.Nombre, rol.Capacidades));
            }

            foreach (var semilla in Usuarios.Where(u => u.Rol.EmpresaId == empresa))
            {
                // El constructor de dominio aplica todas sus reglas; solo la clave se fija con la API de EF Core para que
                // las pruebas puedan referirse siempre al mismo usuario.
                var usuario = new Usuario(empresa, semilla.NombreCompleto, semilla.IdentificadorAcceso, semilla.Rol.Id, semilla.Estado, Momento);
                contexto.Entry(usuario).Property(u => u.Id).CurrentValue = semilla.Id;
                contexto.Add(usuario);
            }

            await contexto.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Contexto de empresa de la siembra: no representa ninguna solicitud ni usuario.</summary>
    private sealed class ContextoSemilla(Guid empresaId) : IContextoSolicitud
    {
        public Guid UsuarioId => Guid.Empty;

        public Guid EmpresaId { get; } = empresaId;

        public IReadOnlyCollection<string> Capacidades => [];
    }
}
