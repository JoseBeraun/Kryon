namespace Kryon.Core.Usuarios;

/// <summary>
/// Estado de un usuario. Los estados adicionales (por ejemplo, "Pendiente") y el estado inicial al registrar
/// los decidirá la futura spec de autenticación (DEP-2); este enum se amplía solo si esa spec lo define.
/// </summary>
public enum EstadoUsuario : byte
{
    Activo = 1,
    Inactivo = 2,
}

/// <summary>
/// Usuario de una empresa. No existe ninguna operación de borrado (FR-033).
/// </summary>
public sealed class Usuario
{
    /// <summary>
    /// Crea un usuario de la empresa del contexto verificado.
    /// </summary>
    /// <param name="empresaId">Se toma siempre de <c>IContextoSolicitud</c>; es inmutable.</param>
    /// <param name="nombreCompleto">Se recortan los espacios y no puede quedar vacío.</param>
    /// <param name="identificadorAcceso">
    /// Obligatorio. Su formato y su unicidad no se validan aquí porque dependen de DEP-3.
    /// </param>
    /// <param name="rolId">Rol asignado.</param>
    /// <param name="estadoInicial">Lo decide <c>IPoliticaAltaUsuario</c> (DEP-2); esta clase no asume ninguno.</param>
    /// <param name="ahora">Instante de creación en UTC.</param>
    /// <remarks>
    /// Las longitudes máximas son provisionales y configurables: las valida <c>ValidadorUsuario</c>
    /// con <c>OpcionesUsuarios</c>, no esta clase.
    /// </remarks>
    public Usuario(
        Guid empresaId,
        string nombreCompleto,
        string identificadorAcceso,
        Guid rolId,
        EstadoUsuario estadoInicial,
        DateTimeOffset ahora)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(empresaId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(rolId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(identificadorAcceso);
        if (!Enum.IsDefined(estadoInicial))
        {
            throw new ArgumentOutOfRangeException(nameof(estadoInicial), estadoInicial, "Estado de usuario no definido.");
        }

        ExigirUtc(ahora);

        Id = Guid.NewGuid();
        EmpresaId = empresaId;
        NombreCompleto = NormalizarNombreCompleto(nombreCompleto);
        IdentificadorAcceso = identificadorAcceso;
        RolId = rolId;
        Estado = estadoInicial;
        CreadoEn = ahora;
        ModificadoEn = ahora;
    }

    /// <summary>Lo genera el servidor.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa propietaria. Es inmutable.</summary>
    public Guid EmpresaId { get; private init; }

    public string NombreCompleto { get; private set; }

    public string IdentificadorAcceso { get; private set; }

    public Guid RolId { get; private set; }

    public EstadoUsuario Estado { get; private set; }

    /// <summary>Token de concurrencia (<c>rowversion</c>), expuesto como ETag. Lo asigna la base de datos.</summary>
    public byte[] Version { get; private set; } = [];

    /// <summary>UTC.</summary>
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>UTC.</summary>
    public DateTimeOffset ModificadoEn { get; private set; }

    private static string NormalizarNombreCompleto(string nombreCompleto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreCompleto);
        return nombreCompleto.Trim();
    }

    private static void ExigirUtc(DateTimeOffset instante)
    {
        if (instante.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("El instante debe estar en UTC.", nameof(instante));
        }
    }
}
