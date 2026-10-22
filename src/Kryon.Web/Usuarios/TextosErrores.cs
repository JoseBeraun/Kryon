using Kryon.Contracts.Usuarios;

namespace Kryon.Web.Usuarios;

/// <summary>
/// Textos de error de la interfaz, copiados literalmente de la tabla <i>Mensajes</i> de
/// <c>contracts/ui-usuarios.md</c>: uno por cada código de <see cref="CodigosError"/> y otro para el caso sin respuesta
/// o error de red. Nunca se muestra el <c>title</c> ni ningún otro texto que venga del servidor.
/// </summary>
public static class TextosErrores
{
    /// <summary>Sin respuesta o error de red: no se sabe si el cambio se aplicó (FR-042, EC-8).</summary>
    public const string SinRespuesta = "No se pudo confirmar el resultado. Revisa el listado y vuelve a intentarlo.";

    /// <summary>Texto de <c>error-interno</c>; también el de cualquier código desconocido.</summary>
    public const string ErrorInterno = "No se pudo completar la acción por un error interno. Vuelve a intentarlo.";

    /// <summary>
    /// Texto del código de error de la API. Un código desconocido (o null) usa el texto de <c>error-interno</c>.
    /// Para <c>validacion</c> es solo el mensaje general: los errores de cada campo se muestran aparte.
    /// </summary>
    public static string ParaCodigo(string? codigo) => codigo switch
    {
        CodigosError.NoAutenticado => "No se pudo verificar tu sesión. Vuelve a iniciar sesión.",
        CodigosError.SinPermiso => "No tienes permiso para realizar esta acción.",
        CodigosError.UsuarioNoEncontrado => "Usuario no encontrado. Vuelve al listado.",
        CodigosError.Validacion => "Revisa los campos indicados y corrige los errores antes de continuar.",
        CodigosError.RolNoValido => "Selecciona un rol de la lista.",
        CodigosError.IdentificadorEnUso => "Este identificador de acceso no está disponible. Usa otro.",
        CodigosError.IdentificadorNoEditable =>
            "No se cambió el identificador de acceso: este identificador no se puede modificar.",
        CodigosError.LimiteUsuariosAlcanzado => "No se registró el usuario: la empresa alcanzó su límite de usuarios.",
        CodigosError.NoPuedeDesactivarseASiMismo =>
            "No se desactivó la cuenta: no puedes desactivar tu propia cuenta. Pide a otro administrador que lo haga.",
        CodigosError.NoPuedeCambiarSuPropioRol =>
            "No se cambió el rol: no puedes cambiar tu propio rol. Pide a otro administrador que lo haga.",
        CodigosError.UltimoAdministrador =>
            "No se aplicó el cambio: la empresa debe tener al menos un administrador activo. Asigna el rol de administrador a otra persona primero.",
        CodigosError.UsuarioModificado =>
            "No se aplicó el cambio: otra persona modificó este usuario. Se muestran los datos actuales; revisa y vuelve a intentarlo.",
        CodigosError.FaltaVersion =>
            "No se aplicó el cambio porque faltaba la versión del usuario. Recarga los datos y vuelve a intentarlo.",
        CodigosError.ErrorInterno => ErrorInterno,
        _ => ErrorInterno,
    };
}
