using System.Collections.ObjectModel;

namespace Kryon.Core.Usuarios;

/// <summary>
/// Los 13 códigos funcionales y de dominio de la Gestión de Usuarios, con los valores exactos del contrato.
/// <c>error-interno</c> no pertenece a esta lista: lo genera exclusivamente la capa API (T030).
/// </summary>
public static class CodigoErrorUsuarios
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
    ]);

    public static bool EsValido(string? codigo) => codigo is not null && Todos.Contains(codigo, StringComparer.Ordinal);
}

/// <summary>
/// Error funcional o de dominio de la Gestión de Usuarios. La capa API lo traduce a su respuesta (T029).
/// </summary>
public sealed class ErrorUsuarios : Exception
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SinErrores =
        ReadOnlyDictionary<string, IReadOnlyList<string>>.Empty;

    /// <param name="codigo">Uno de los valores de <see cref="CodigoErrorUsuarios"/>.</param>
    /// <param name="mensaje">Descripción del error.</param>
    /// <param name="errores">
    /// Errores por campo. Obligatorios con <see cref="CodigoErrorUsuarios.Validacion"/> y solo con ese código.
    /// </param>
    public ErrorUsuarios(string codigo, string mensaje, IReadOnlyDictionary<string, IReadOnlyList<string>>? errores = null)
        : base(mensaje)
    {
        if (!CodigoErrorUsuarios.EsValido(codigo))
        {
            throw new ArgumentOutOfRangeException(nameof(codigo), codigo, "Código de error de usuarios no definido.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(mensaje);

        var esValidacion = codigo == CodigoErrorUsuarios.Validacion;
        if (esValidacion != (errores is { Count: > 0 }))
        {
            throw new ArgumentException(
                "Los errores por campo son obligatorios en 'validacion' y no aplican a ningún otro código.",
                nameof(errores));
        }

        Codigo = codigo;
        Errores = errores is null
            ? SinErrores
            : errores.ToDictionary(par => par.Key, par => (IReadOnlyList<string>)par.Value.ToArray().AsReadOnly()).AsReadOnly();
    }

    /// <summary>Uno de los valores de <see cref="CodigoErrorUsuarios"/>.</summary>
    public string Codigo { get; }

    /// <summary>Errores por campo; vacío salvo en <see cref="CodigoErrorUsuarios.Validacion"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errores { get; }
}
