namespace Kryon.Core.Usuarios;

/// <summary>Tipo de operación auditada.</summary>
public enum TipoOperacion : byte
{
    Alta = 1,
    Modificacion = 2,
    Activacion = 3,
    Desactivacion = 4,

    /// <summary>
    /// Una operación sobre un <c>{id}</c> terminó en <c>usuario-no-encontrado</c> (FR-046). No afirma que el
    /// recurso pertenezca a otra empresa: un usuario de otra empresa y uno inexistente son indistinguibles.
    /// </summary>
    UsuarioNoAccesible = 5,
}

/// <summary>Operación que se solicitó cuando el registro es <see cref="TipoOperacion.UsuarioNoAccesible"/>.</summary>
public enum OperacionSolicitada : byte
{
    Consulta = 1,
    Edicion = 2,
    Activacion = 3,
    Desactivacion = 4,
}

/// <summary>
/// Registro de auditoría de la Gestión de Usuarios. Es un hecho inmutable: no tiene métodos de modificación.
/// Los datos de auditoría no deben contener credenciales, tokens ni secretos (FR-045).
/// </summary>
public sealed class AuditoriaUsuario
{
    /// <param name="empresaId">La empresa del actor.</param>
    /// <param name="actorUsuarioId">Quién realizó la acción.</param>
    /// <param name="ocurridoEn">UTC, asignado por el servidor.</param>
    /// <param name="tipoOperacion">Tipo de operación auditada.</param>
    /// <param name="usuarioAfectadoId">
    /// El usuario sobre el que se actuó. En <see cref="TipoOperacion.UsuarioNoAccesible"/> es el identificador
    /// solicitado, sin ningún dato del recurso.
    /// </param>
    /// <param name="operacionSolicitada">Solo en <see cref="TipoOperacion.UsuarioNoAccesible"/>, donde es obligatoria.</param>
    /// <param name="cambiosJson">
    /// Solo en <see cref="TipoOperacion.Modificacion"/>: únicamente los campos que cambiaron (FR-044).
    /// Esta clase no lo construye ni valida su contenido.
    /// </param>
    public AuditoriaUsuario(
        Guid empresaId,
        Guid actorUsuarioId,
        DateTimeOffset ocurridoEn,
        TipoOperacion tipoOperacion,
        Guid usuarioAfectadoId,
        OperacionSolicitada? operacionSolicitada,
        string? cambiosJson)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(empresaId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(actorUsuarioId, Guid.Empty);
        if (ocurridoEn.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("El instante debe estar en UTC.", nameof(ocurridoEn));
        }

        if (!Enum.IsDefined(tipoOperacion))
        {
            throw new ArgumentOutOfRangeException(nameof(tipoOperacion), tipoOperacion, "Tipo de operación no definido.");
        }

        var esNoAccesible = tipoOperacion == TipoOperacion.UsuarioNoAccesible;

        // El identificador solicitado se registra tal cual, aunque no identifique a ningún usuario.
        if (!esNoAccesible)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(usuarioAfectadoId, Guid.Empty);
        }

        if (esNoAccesible != operacionSolicitada.HasValue
            || (operacionSolicitada.HasValue && !Enum.IsDefined(operacionSolicitada.Value)))
        {
            throw new ArgumentException(
                "La operación solicitada es obligatoria en UsuarioNoAccesible y no aplica a ningún otro tipo.",
                nameof(operacionSolicitada));
        }

        if (cambiosJson is not null && tipoOperacion != TipoOperacion.Modificacion)
        {
            throw new ArgumentException("Los cambios solo aplican a Modificacion.", nameof(cambiosJson));
        }

        EmpresaId = empresaId;
        ActorUsuarioId = actorUsuarioId;
        OcurridoEn = ocurridoEn;
        TipoOperacion = tipoOperacion;
        UsuarioAfectadoId = usuarioAfectadoId;
        OperacionSolicitada = operacionSolicitada;
        CambiosJson = cambiosJson;
    }

    /// <summary>bigint identity; lo asigna la base de datos.</summary>
    public long Id { get; private set; }

    /// <summary>La empresa del actor.</summary>
    public Guid EmpresaId { get; }

    public Guid ActorUsuarioId { get; }

    /// <summary>UTC.</summary>
    public DateTimeOffset OcurridoEn { get; }

    public TipoOperacion TipoOperacion { get; }

    /// <summary>En <see cref="TipoOperacion.UsuarioNoAccesible"/>, el identificador solicitado.</summary>
    public Guid UsuarioAfectadoId { get; }

    public OperacionSolicitada? OperacionSolicitada { get; }

    public string? CambiosJson { get; }
}
