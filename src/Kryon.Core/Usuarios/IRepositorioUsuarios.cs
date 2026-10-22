namespace Kryon.Core.Usuarios;

/// <summary>
/// Persistencia de usuarios para las operaciones de negocio, limitada a la empresa del contexto verificado.
/// No existe ninguna operación de borrado (FR-033).
/// </summary>
public interface IRepositorioUsuarios
{
    /// <summary>Obtiene un usuario de la empresa actual para modificarlo.</summary>
    /// <param name="usuarioId">Identificador solicitado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>null si no existe o no es accesible; ambos casos son indistinguibles (FR-003).</returns>
    Task<Usuario?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>
    /// Cuenta los administradores activos de la empresa actual: usuarios Activos cuyo rol incluye la capacidad de
    /// acceso a la gestión de usuarios (FR-036). Se usa con <see cref="IBloqueoAdministradoresEmpresa"/> adquirido.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken);

    /// <summary>Guarda un usuario nuevo de la empresa actual.</summary>
    /// <param name="usuario">Usuario creado con la empresa del contexto.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda los cambios de un usuario usando su <see cref="Usuario.Version"/> como token de concurrencia. Si otra
    /// operación lo modificó desde que se leyó, lanza <see cref="ErrorUsuarios"/> con
    /// <see cref="CodigoErrorUsuarios.UsuarioModificado"/> y no guarda nada (FR-038).
    /// </summary>
    /// <param name="usuario">Usuario obtenido con <see cref="ObtenerAsync"/>.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken);
}
