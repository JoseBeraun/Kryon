using Kryon.Contracts.Usuarios;
using Kryon.Core.Usuarios;

namespace Kryon.Api.Usuarios;

/// <summary>
/// Traduce <see cref="ErrorUsuarios"/> a la respuesta de error del contrato (ProblemDetails, RFC 9457):
/// <c>type</c>, <c>title</c>, <c>status</c> y <c>codigo</c>; <c>errors</c> solo en <c>validacion</c> y <c>actual</c>
/// solo en <c>usuario-modificado</c>.
/// </summary>
/// <remarks>
/// La respuesta se construye únicamente con el código, los errores por campo y los datos vigentes recibidos: nunca
/// incluye el mensaje de la excepción, trazas, nombres de tablas ni datos de otra empresa (FR-041). Los textos que ve
/// el administrador los decide la interfaz a partir de <c>codigo</c> (research §R9).
/// </remarks>
public static class MapeoErrores
{
    public const string TipoProblemaJson = "application/problem+json";

    private const string BaseTipo = "https://kryon.app/errores/";

    /// <summary>Respuesta HTTP para el error.</summary>
    /// <param name="error">Error funcional o de dominio.</param>
    /// <param name="actual">
    /// Datos vigentes del usuario. Obligatorio con <c>usuario-modificado</c> y no admitido con ningún otro código.
    /// </param>
    public static IResult ARespuesta(this ErrorUsuarios error, DetalleUsuario? actual = null)
    {
        var problema = AProblema(error, actual);
        return TypedResults.Json(problema, options: null, contentType: TipoProblemaJson, statusCode: problema.Status);
    }

    /// <summary>Cuerpo de la respuesta de error para el error.</summary>
    /// <param name="error">Error funcional o de dominio.</param>
    /// <param name="actual">
    /// Datos vigentes del usuario. Obligatorio con <c>usuario-modificado</c> y no admitido con ningún otro código.
    /// </param>
    public static Problema AProblema(this ErrorUsuarios error, DetalleUsuario? actual = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var esModificado = error.Codigo == CodigoErrorUsuarios.UsuarioModificado;
        if (esModificado != actual is not null)
        {
            throw new ArgumentException(
                "Los datos vigentes son obligatorios en 'usuario-modificado' y no se admiten con ningún otro código.",
                nameof(actual));
        }

        var (status, title) = Definicion(error.Codigo);
        return new Problema
        {
            Type = BaseTipo + error.Codigo,
            Title = title,
            Status = status,
            Codigo = error.Codigo,
            Errors = error.Codigo == CodigoErrorUsuarios.Validacion
                ? error.Errores.ToDictionary(campo => campo.Key, campo => campo.Value.ToArray())
                : null,
            Actual = actual,
        };
    }

    /// <summary>Estado HTTP (contracts/usuarios-api.openapi.yaml) y título de cada código.</summary>
    private static (int Status, string Title) Definicion(string codigo) => codigo switch
    {
        CodigoErrorUsuarios.NoAutenticado => (StatusCodes.Status401Unauthorized, "No autenticado"),
        CodigoErrorUsuarios.SinPermiso => (StatusCodes.Status403Forbidden, "Sin permiso para esta acción"),
        CodigoErrorUsuarios.UsuarioNoEncontrado => (StatusCodes.Status404NotFound, "Usuario no encontrado"),
        CodigoErrorUsuarios.Validacion => (StatusCodes.Status400BadRequest, "Los datos enviados no son válidos"),
        CodigoErrorUsuarios.RolNoValido => (StatusCodes.Status409Conflict, "El rol no es válido"),
        CodigoErrorUsuarios.IdentificadorEnUso => (StatusCodes.Status409Conflict, "El identificador de acceso no está disponible"),
        CodigoErrorUsuarios.IdentificadorNoEditable => (StatusCodes.Status409Conflict, "El identificador de acceso no se puede modificar"),
        CodigoErrorUsuarios.LimiteUsuariosAlcanzado => (StatusCodes.Status409Conflict, "Se alcanzó el límite de usuarios"),
        CodigoErrorUsuarios.NoPuedeDesactivarseASiMismo => (StatusCodes.Status409Conflict, "No puedes desactivar tu propia cuenta"),
        CodigoErrorUsuarios.NoPuedeCambiarSuPropioRol => (StatusCodes.Status409Conflict, "No puedes cambiar tu propio rol"),
        CodigoErrorUsuarios.UltimoAdministrador => (StatusCodes.Status409Conflict, "La empresa debe conservar al menos un administrador activo"),
        CodigoErrorUsuarios.UsuarioModificado => (StatusCodes.Status412PreconditionFailed, "Otra persona modificó este usuario"),
        CodigoErrorUsuarios.FaltaVersion => (StatusCodes.Status428PreconditionRequired, "Falta la versión del usuario"),

        // ErrorUsuarios solo admite los 13 códigos anteriores: llegar aquí es un error de programación, no una
        // respuesta para el cliente.
        _ => throw new InvalidOperationException($"Código de error sin mapeo HTTP: '{codigo}'."),
    };
}
