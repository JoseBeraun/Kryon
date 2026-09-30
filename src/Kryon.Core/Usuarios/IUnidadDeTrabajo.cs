namespace Kryon.Core.Usuarios;

/// <summary>
/// Límite transaccional de los casos de uso: la operación, el bloqueo por empresa y la auditoría se confirman o se
/// revierten juntos.
/// </summary>
public interface IUnidadDeTrabajo
{
    /// <summary>Inicia la transacción.</summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task IniciarAsync(CancellationToken cancellationToken);

    /// <summary>Confirma la transacción iniciada.</summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task ConfirmarAsync(CancellationToken cancellationToken);

    /// <summary>Revierte la transacción iniciada; no queda ningún cambio de la operación.</summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task RevertirAsync(CancellationToken cancellationToken);
}
