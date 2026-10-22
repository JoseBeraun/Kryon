using Microsoft.AspNetCore.Authorization;

namespace Kryon.Api.Seguridad;

/// <summary>
/// Políticas de autorización de la Gestión de Usuarios (research §R4).
/// Cada política se asocia por configuración (<see cref="SeccionPermisos"/>) al nombre del permiso real.
/// Los nombres de permiso de appsettings.json son provisionales, no un catálogo definitivo.
/// </summary>
public static class PoliticasUsuarios
{
    /// <summary>Sección de configuración que asocia cada política con el nombre de su permiso.</summary>
    public const string SeccionPermisos = "Seguridad:Permisos";

    public const string Acceder = "Usuarios.Acceder";
    public const string Registrar = "Usuarios.Registrar";
    public const string Editar = "Usuarios.Editar";
    public const string Activar = "Usuarios.Activar";
    public const string Desactivar = "Usuarios.Desactivar";

    /// <summary>Las cinco políticas de la Gestión de Usuarios.</summary>
    public static IReadOnlyList<string> Todas { get; } = [Acceder, Registrar, Editar, Activar, Desactivar];

    /// <summary>
    /// Define las cinco políticas y registra su manejador. El registro en la aplicación lo hace T028.
    /// </summary>
    public static IServiceCollection AgregarPoliticasUsuarios(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, ManejadorRequisitoPermisoUsuarios>();
        services.AddAuthorizationBuilder()
            .AddPolicy(Acceder, politica => politica.AddRequirements(new RequisitoPermisoUsuarios(Acceder)))
            .AddPolicy(Registrar, politica => politica.AddRequirements(new RequisitoPermisoUsuarios(Registrar)))
            .AddPolicy(Editar, politica => politica.AddRequirements(new RequisitoPermisoUsuarios(Editar)))
            .AddPolicy(Activar, politica => politica.AddRequirements(new RequisitoPermisoUsuarios(Activar)))
            .AddPolicy(Desactivar, politica => politica.AddRequirements(new RequisitoPermisoUsuarios(Desactivar)));
        return services;
    }
}

/// <summary>Requisito: el actor debe tener el permiso asociado a <see cref="Politica"/> en la configuración.</summary>
public sealed class RequisitoPermisoUsuarios(string politica) : IAuthorizationRequirement
{
    public string Politica { get; } = politica;
}

/// <summary>
/// Evalúa <see cref="RequisitoPermisoUsuarios"/> con las capacidades del contexto de solicitud (T007).
/// Falla de forma cerrada: si la política no tiene un permiso configurado, si el contexto no puede
/// construirse o si el actor no tiene ese permiso, el requisito no se cumple y la política deniega.
/// </summary>
public sealed class ManejadorRequisitoPermisoUsuarios(IConfiguration configuracion)
    : AuthorizationHandler<RequisitoPermisoUsuarios>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RequisitoPermisoUsuarios requirement)
    {
        var permiso = configuracion.GetSection(PoliticasUsuarios.SeccionPermisos)[requirement.Politica];
        if (string.IsNullOrWhiteSpace(permiso))
        {
            return Task.CompletedTask;
        }

        if (!ContextoSolicitudDesdeClaims.TryCrear(context.User, out var contexto))
        {
            return Task.CompletedTask;
        }

        if (contexto.Capacidades.Contains(permiso, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
