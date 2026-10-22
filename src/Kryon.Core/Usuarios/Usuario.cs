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

    /// <summary>
    /// Solo para que la persistencia reconstruya usuarios ya guardados; los usuarios nuevos se crean con el
    /// constructor público.
    /// </summary>
    private Usuario()
    {
        NombreCompleto = string.Empty;
        IdentificadorAcceso = string.Empty;
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

    /// <summary>
    /// Pasa el usuario de Activo a Inactivo (data-model.md → <i>Transiciones de estado</i>). Aplica solo las
    /// precondiciones que no dependen de la base de datos; la del último administrador, la versión y el permiso los
    /// comprueba el caso de uso.
    /// </summary>
    /// <param name="actorUsuarioId">Usuario que realiza la acción, tomado del contexto verificado.</param>
    /// <exception cref="ErrorUsuarios">
    /// <see cref="CodigoErrorUsuarios.NoPuedeDesactivarseASiMismo"/> si es la cuenta del actor (FR-034);
    /// <see cref="CodigoErrorUsuarios.UsuarioModificado"/> si ya no está Activo (EC-2).
    /// </exception>
    public void Desactivar(Guid actorUsuarioId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorUsuarioId, Guid.Empty);

        if (actorUsuarioId == Id)
        {
            throw new ErrorUsuarios(CodigoErrorUsuarios.NoPuedeDesactivarseASiMismo, "No se puede desactivar la propia cuenta.");
        }

        ExigirEstado(EstadoUsuario.Activo);
        Estado = EstadoUsuario.Inactivo;
    }

    /// <summary>Pasa el usuario de Inactivo a Activo (data-model.md → <i>Transiciones de estado</i>).</summary>
    /// <exception cref="ErrorUsuarios">
    /// <see cref="CodigoErrorUsuarios.UsuarioModificado"/> si ya no está Inactivo (EC-2).
    /// </exception>
    public void Activar()
    {
        ExigirEstado(EstadoUsuario.Inactivo);
        Estado = EstadoUsuario.Activo;
    }

    /// <summary>
    /// Cambia el nombre completo (US4). Las longitudes y la obligatoriedad con mensajes por campo las valida
    /// <c>ValidadorUsuario</c>; aquí solo se recorta y se rechaza un valor vacío.
    /// </summary>
    public void CambiarNombreCompleto(string nombreCompleto) =>
        NombreCompleto = NormalizarNombreCompleto(nombreCompleto);

    /// <summary>
    /// Cambia el rol (US4). Que el rol sea de la empresa actual, la regla del propio rol y la del último administrador
    /// las comprueba el caso de uso.
    /// </summary>
    public void CambiarRol(Guid rolId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(rolId, Guid.Empty);
        RolId = rolId;
    }

    /// <summary>
    /// El usuario debe estar en el estado de origen de la transición. Si no lo está, otra persona lo cambió desde que se
    /// cargó la pantalla: se informa como <c>usuario-modificado</c> con el estado actual (data-model.md, precondición 5).
    /// </summary>
    private void ExigirEstado(EstadoUsuario origen)
    {
        if (Estado != origen)
        {
            throw new ErrorUsuarios(CodigoErrorUsuarios.UsuarioModificado, "El estado del usuario cambió desde que se consultó.");
        }
    }

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
