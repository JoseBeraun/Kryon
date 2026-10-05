namespace Kryon.Core.Usuarios;

/// <summary>
/// Bloqueo exclusivo, por empresa, de las operaciones que pueden reducir el número de administradores activos
/// (desactivar o cambiar un rol). Evita que dos operaciones concurrentes dejen a la empresa sin administradores
/// (FR-036, EC-6).
/// </summary>
/// <remarks>
/// La empresa es siempre la del contexto verificado: no se recibe como parámetro. El bloqueo solo afecta a esa
/// empresa y dura hasta que termina la transacción de <see cref="IUnidadDeTrabajo"/>.
/// </remarks>
public interface IBloqueoAdministradoresEmpresa
{
    /// <summary>
    /// Adquiere el bloqueo para la empresa actual, esperando si otra operación lo tiene. Debe llamarse con una
    /// transacción iniciada y antes de contar los administradores activos.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task AdquirirAsync(CancellationToken cancellationToken);
}
