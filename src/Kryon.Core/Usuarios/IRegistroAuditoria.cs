namespace Kryon.Core.Usuarios;

/// <summary>
/// Registro de auditoría de la Gestión de Usuarios (solo inserción). Se escribe en la misma transacción que la
/// operación auditada, así que una operación rechazada o revertida no deja registro de ese tipo (FR-032, EC-2, EC-3).
/// </summary>
public interface IRegistroAuditoria
{
    /// <summary>Añade un registro de auditoría.</summary>
    /// <param name="registro">
    /// Registro con la empresa del actor. Los datos de auditoría no deben contener credenciales, tokens ni secretos
    /// (FR-045).
    /// </param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task RegistrarAsync(AuditoriaUsuario registro, CancellationToken cancellationToken);
}
