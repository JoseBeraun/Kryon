namespace Kryon.Core.Usuarios;

/// <summary>
/// Validación de los datos de un usuario para registrarlo o editarlo (FR-024 a FR-026). Los topes de longitud son
/// provisionales y salen de <see cref="OpcionesUsuarios"/>; no son requisitos de negocio.
/// </summary>
/// <remarks>
/// No valida el formato ni la unicidad del identificador de acceso: los define la futura spec de autenticación (DEP-3).
/// El rol solo se busca entre los de la empresa actual (<see cref="IConsultaUsuarios"/>): un rol de otra empresa se
/// comporta como inexistente y no se revela nada de ella.
/// </remarks>
public sealed class ValidadorUsuario(OpcionesUsuarios opciones, IConsultaUsuarios consulta)
{
    /// <summary>Nombres de campo de los errores, los mismos que en el contrato HTTP.</summary>
    public const string CampoNombreCompleto = "nombreCompleto";

    /// <inheritdoc cref="CampoNombreCompleto"/>
    public const string CampoIdentificadorAcceso = "identificadorAcceso";

    /// <inheritdoc cref="CampoNombreCompleto"/>
    public const string CampoRolId = "rolId";

    /// <summary>Valida los datos y devuelve el nombre ya recortado y el rol de la empresa actual.</summary>
    /// <param name="nombreCompleto">Nombre completo tal como llega.</param>
    /// <param name="identificadorAcceso">
    /// Identificador de acceso, obligatorio si <paramref name="incluyeIdentificador"/>; si no, se ignora (la edición no lo
    /// acepta mientras DEP-4 siga abierta).
    /// </param>
    /// <param name="incluyeIdentificador">La operación recibe el identificador (registro).</param>
    /// <param name="rolId">Rol elegido.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <exception cref="ErrorUsuarios">
    /// <see cref="CodigoErrorUsuarios.Validacion"/> con los errores por campo, o
    /// <see cref="CodigoErrorUsuarios.RolNoValido"/> si el rol no pertenece a la empresa actual (FR-026).
    /// </exception>
    public async Task<DatosUsuarioValidados> ValidarAsync(
        string? nombreCompleto,
        string? identificadorAcceso,
        bool incluyeIdentificador,
        Guid rolId,
        CancellationToken cancellationToken)
    {
        var errores = new Dictionary<string, IReadOnlyList<string>>();
        var limites = opciones.Validacion;

        var nombre = nombreCompleto?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            errores[CampoNombreCompleto] = ["El nombre completo es obligatorio."];
        }
        else if (nombre.Length > limites.LongitudMaximaNombre)
        {
            errores[CampoNombreCompleto] = [$"El nombre completo no puede superar {limites.LongitudMaximaNombre} caracteres."];
        }

        if (incluyeIdentificador)
        {
            if (string.IsNullOrWhiteSpace(identificadorAcceso))
            {
                errores[CampoIdentificadorAcceso] = ["El identificador de acceso es obligatorio."];
            }
            else if (identificadorAcceso.Length > limites.LongitudMaximaIdentificador)
            {
                errores[CampoIdentificadorAcceso] = [$"El identificador de acceso no puede superar {limites.LongitudMaximaIdentificador} caracteres."];
            }
        }

        if (rolId == Guid.Empty)
        {
            errores[CampoRolId] = ["El rol es obligatorio."];
        }

        if (errores.Count > 0)
        {
            throw new ErrorUsuarios(CodigoErrorUsuarios.Validacion, "Los datos del usuario no son válidos.", errores);
        }

        var rol = await consulta.ObtenerRolAsync(rolId, cancellationToken)
            ?? throw new ErrorUsuarios(CodigoErrorUsuarios.RolNoValido, "El rol no pertenece a la empresa actual.");

        return new DatosUsuarioValidados(nombre, incluyeIdentificador ? identificadorAcceso : null, rol);
    }
}

/// <summary>Datos ya validados: el nombre recortado, el identificador (si la operación lo recibe) y el rol.</summary>
/// <param name="NombreCompleto">Nombre completo sin espacios al principio ni al final.</param>
/// <param name="IdentificadorAcceso">Identificador tal como llegó, sin normalizar (DEP-3); null si no aplica.</param>
/// <param name="Rol">Rol de la empresa actual.</param>
public sealed record DatosUsuarioValidados(string NombreCompleto, string? IdentificadorAcceso, RolReferencia Rol);
