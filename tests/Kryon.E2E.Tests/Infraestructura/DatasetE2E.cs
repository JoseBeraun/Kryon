using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Kryon.E2E.Tests.Infraestructura;

/// <summary>
/// Dataset de las pruebas E2E, equivalente al dataset semilla de quickstart.md y definido aquí (caja negra: no usa el
/// código de <c>SemillaDatos</c> de <c>Kryon.Api.Tests</c>).
/// </summary>
/// <remarks>
/// <para>
/// Empresa A: <see cref="Ana"/> (administradora, todas las capacidades de gestión), <see cref="Beto"/> (administrador
/// con un rol sin la capacidad de desactivar), <see cref="Carla"/> (sin capacidades de gestión) y <see cref="Dario"/>
/// (Inactivo). Empresa B: <see cref="Berta"/> (administradora) y <see cref="AnaBeta"/>, que coincide con la búsqueda
/// "Ana" desde la empresa A.
/// </para>
/// <para>
/// Los nombres de permiso son provisionales (research §R4) y no se repiten aquí: se leen de la sección
/// <c>Seguridad:Permisos</c> del <c>appsettings.json</c> compilado de la API, la misma configuración que usan sus
/// políticas.
/// </para>
/// </remarks>
public static class DatasetE2E
{
    public static readonly Guid EmpresaA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    public static readonly Guid EmpresaB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");

    private static readonly Lazy<PermisosE2E> PermisosConfigurados = new(LeerPermisos);

    public static PermisosE2E Permisos => PermisosConfigurados.Value;

    public static RolE2E RolAdministracionA => new(Guid.Parse("a1000000-0000-0000-0000-000000000001"), EmpresaA, "Administración", Permisos.Todos);

    public static RolE2E RolSinDesactivarA => new(
        Guid.Parse("a1000000-0000-0000-0000-000000000002"),
        EmpresaA,
        "Sin desactivar",
        [Permisos.Acceder, Permisos.Registrar, Permisos.Editar, Permisos.Activar]);

    public static RolE2E RolSinGestionA => new(Guid.Parse("a1000000-0000-0000-0000-000000000003"), EmpresaA, "Sin gestión", []);

    public static RolE2E RolAdministracionB => new(Guid.Parse("b1000000-0000-0000-0000-000000000001"), EmpresaB, "Administración", Permisos.Todos);

    public static RolE2E RolSinGestionB => new(Guid.Parse("b1000000-0000-0000-0000-000000000002"), EmpresaB, "Sin gestión", []);

    public static UsuarioE2E Ana => new(Guid.Parse("a2000000-0000-0000-0000-000000000001"), "Ana", "ana", RolAdministracionA, Activo: true);

    public static UsuarioE2E Beto => new(Guid.Parse("a2000000-0000-0000-0000-000000000002"), "Beto", "beto", RolSinDesactivarA, Activo: true);

    public static UsuarioE2E Carla => new(Guid.Parse("a2000000-0000-0000-0000-000000000003"), "Carla", "carla", RolSinGestionA, Activo: true);

    public static UsuarioE2E Dario => new(Guid.Parse("a2000000-0000-0000-0000-000000000004"), "Dario", "dario", RolSinGestionA, Activo: false);

    public static UsuarioE2E Berta => new(Guid.Parse("b2000000-0000-0000-0000-000000000001"), "Berta", "berta", RolAdministracionB, Activo: true);

    public static UsuarioE2E AnaBeta => new(Guid.Parse("b2000000-0000-0000-0000-000000000002"), "Ana Beta", "ana.beta", RolSinGestionB, Activo: true);

    public static IReadOnlyList<RolE2E> Roles => [RolAdministracionA, RolSinDesactivarA, RolSinGestionA, RolAdministracionB, RolSinGestionB];

    public static IReadOnlyList<UsuarioE2E> Usuarios => [Ana, Beto, Carla, Dario, Berta, AnaBeta];

    /// <summary>
    /// Un script por empresa. Cada uno fija el <c>SESSION_CONTEXT</c> de su empresa (de solo lectura) antes de insertar,
    /// así la RLS acepta las filas sin desactivarla ni saltársela, igual que en la aplicación.
    /// </summary>
    internal static IEnumerable<(string Empresa, string Script)> ScriptsPorEmpresa(string baseDatos)
    {
        foreach (var (nombre, empresa) in new[] { ("A", EmpresaA), ("B", EmpresaB) })
        {
            var script = new StringBuilder();
            script.AppendLine(CultureInfo.InvariantCulture, $"USE [{baseDatos}];");
            script.AppendLine("SET NOCOUNT ON;");
            script.AppendLine(CultureInfo.InvariantCulture, $"EXEC sys.sp_set_session_context @key = N'EmpresaId', @value = '{empresa}', @read_only = 1;");

            foreach (var rol in Roles.Where(r => r.EmpresaId == empresa))
            {
                script.AppendLine(CultureInfo.InvariantCulture,
                    $"INSERT INTO dbo.Roles (Id, EmpresaId, Nombre, Capacidades) VALUES ('{rol.Id}', '{empresa}', {Texto(rol.Nombre)}, {Texto(JsonSerializer.Serialize(rol.Capacidades))});");
            }

            foreach (var usuario in Usuarios.Where(u => u.Rol.EmpresaId == empresa))
            {
                // Estado: 1 = Activo, 2 = Inactivo (columna tinyint del modelo).
                script.AppendLine(CultureInfo.InvariantCulture,
                    $"INSERT INTO dbo.Usuarios (Id, EmpresaId, NombreCompleto, IdentificadorAcceso, RolId, Estado, CreadoEn, ModificadoEn) " +
                    $"VALUES ('{usuario.Id}', '{empresa}', {Texto(usuario.NombreCompleto)}, {Texto(usuario.IdentificadorAcceso)}, '{usuario.Rol.Id}', " +
                    $"{(usuario.Activo ? 1 : 2)}, '2026-01-01T00:00:00+00:00', '2026-01-01T00:00:00+00:00');");
            }

            yield return (nombre, script.ToString());
        }
    }

    private static string Texto(string valor) => $"N'{valor.Replace("'", "''", StringComparison.Ordinal)}'";

    private static PermisosE2E LeerPermisos()
    {
        using var configuracion = JsonDocument.Parse(File.ReadAllText(Rutas.SalidaCompilada("src/Kryon.Api", "appsettings.json")));
        var permisos = configuracion.RootElement.GetProperty("Seguridad").GetProperty("Permisos");
        string Permiso(string politica) => permisos.GetProperty(politica).GetString()
            ?? throw new InvalidOperationException($"La API no configura el permiso de la política '{politica}'.");

        return new PermisosE2E(
            Permiso("Usuarios.Acceder"),
            Permiso("Usuarios.Registrar"),
            Permiso("Usuarios.Editar"),
            Permiso("Usuarios.Activar"),
            Permiso("Usuarios.Desactivar"));
    }
}

/// <summary>Nombres de permiso configurados en la API para cada política de la feature.</summary>
public sealed record PermisosE2E(string Acceder, string Registrar, string Editar, string Activar, string Desactivar)
{
    public IReadOnlyList<string> Todos => [Acceder, Registrar, Editar, Activar, Desactivar];
}

public sealed record RolE2E(Guid Id, Guid EmpresaId, string Nombre, IReadOnlyList<string> Capacidades);

public sealed record UsuarioE2E(Guid Id, string NombreCompleto, string IdentificadorAcceso, RolE2E Rol, bool Activo)
{
    /// <summary>
    /// Encabezados del esquema <c>IdentidadPrueba</c> de la API para actuar como este usuario, con las capacidades de su
    /// rol. Sus nombres son los de T009.
    /// </summary>
    public IReadOnlyDictionary<string, string> EncabezadosIdentidad => new Dictionary<string, string>
    {
        ["X-Kryon-Prueba-UsuarioId"] = Id.ToString(),
        ["X-Kryon-Prueba-EmpresaId"] = Rol.EmpresaId.ToString(),
        ["X-Kryon-Prueba-Capacidades"] = string.Join(',', Rol.Capacidades),
    };
}
