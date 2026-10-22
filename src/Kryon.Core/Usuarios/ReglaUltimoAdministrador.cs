namespace Kryon.Core.Usuarios;

/// <summary>
/// Regla del último administrador (FR-036, EC-6): ninguna operación puede dejar a la empresa sin administradores
/// activos. Un administrador activo es un usuario Activo cuyo rol incluye la capacidad de acceso a la gestión de
/// usuarios (nombre provisional <c>usuarios.administrar</c>, research §R4).
/// </summary>
/// <remarks>
/// El nombre de esa capacidad es configurable (<c>Seguridad:Permisos</c> de la API): esta regla no lo fija, lo recibe.
/// El recuento se hace con <see cref="IBloqueoAdministradoresEmpresa"/> adquirido para que dos operaciones
/// concurrentes no lo vean a la vez (research §R5).
/// </remarks>
public static class ReglaUltimoAdministrador
{
    /// <summary>Indica si <paramref name="usuario"/> es un administrador activo.</summary>
    /// <param name="usuario">Usuario de la empresa actual.</param>
    /// <param name="rol">Su rol; null si no tiene un rol reconocido (EC-10), en cuyo caso no es administrador.</param>
    /// <param name="capacidadAdministrar">Nombre configurado de la capacidad de acceso a la gestión de usuarios.</param>
    public static bool EsAdministradorActivo(Usuario usuario, RolReferencia? rol, string capacidadAdministrar)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentException.ThrowIfNullOrWhiteSpace(capacidadAdministrar);

        return usuario.Estado == EstadoUsuario.Activo
            && rol is not null
            && rol.Id == usuario.RolId
            && rol.Capacidades.Contains(capacidadAdministrar, StringComparer.Ordinal);
    }

    /// <summary>Rechaza el cambio si dejaría a la empresa sin administradores activos.</summary>
    /// <param name="administradoresActivosRestantes">Administradores activos que quedarían después del cambio.</param>
    /// <exception cref="ErrorUsuarios"><see cref="CodigoErrorUsuarios.UltimoAdministrador"/> si quedarían cero.</exception>
    public static void Verificar(int administradoresActivosRestantes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(administradoresActivosRestantes);

        if (administradoresActivosRestantes == 0)
        {
            throw new ErrorUsuarios(
                CodigoErrorUsuarios.UltimoAdministrador,
                "La empresa debe conservar al menos un administrador activo.");
        }
    }
}
