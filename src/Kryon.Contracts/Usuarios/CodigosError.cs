namespace Kryon.Contracts.Usuarios;

/// <summary>Los 14 valores de <c>Problema.codigo</c> del contrato HTTP, incluido <c>error-interno</c>.</summary>
public static class CodigosError
{
    public const string NoAutenticado = "no-autenticado";
    public const string SinPermiso = "sin-permiso";
    public const string UsuarioNoEncontrado = "usuario-no-encontrado";
    public const string Validacion = "validacion";
    public const string RolNoValido = "rol-no-valido";
    public const string IdentificadorEnUso = "identificador-en-uso";
    public const string IdentificadorNoEditable = "identificador-no-editable";
    public const string LimiteUsuariosAlcanzado = "limite-usuarios-alcanzado";
    public const string NoPuedeDesactivarseASiMismo = "no-puede-desactivarse-a-si-mismo";
    public const string NoPuedeCambiarSuPropioRol = "no-puede-cambiar-su-propio-rol";
    public const string UltimoAdministrador = "ultimo-administrador";
    public const string UsuarioModificado = "usuario-modificado";
    public const string FaltaVersion = "falta-version";
    public const string ErrorInterno = "error-interno";

    public static IReadOnlyList<string> Todos { get; } = Array.AsReadOnly<string>(
    [
        NoAutenticado,
        SinPermiso,
        UsuarioNoEncontrado,
        Validacion,
        RolNoValido,
        IdentificadorEnUso,
        IdentificadorNoEditable,
        LimiteUsuariosAlcanzado,
        NoPuedeDesactivarseASiMismo,
        NoPuedeCambiarSuPropioRol,
        UltimoAdministrador,
        UsuarioModificado,
        FaltaVersion,
        ErrorInterno,
    ]);
}
