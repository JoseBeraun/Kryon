using Kryon.Core.Usuarios;

using Microsoft.EntityFrameworkCore.Storage;

namespace Kryon.Infrastructure.Persistencia;

/// <summary>Transacción de <see cref="KryonDbContext"/> para un caso de uso.</summary>
public sealed class UnidadDeTrabajo(KryonDbContext contexto) : IUnidadDeTrabajo
{
    private IDbContextTransaction? transaccion;

    public async Task IniciarAsync(CancellationToken cancellationToken)
    {
        if (transaccion is not null)
        {
            throw new InvalidOperationException("La transacción ya está iniciada.");
        }

        transaccion = await contexto.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task ConfirmarAsync(CancellationToken cancellationToken)
    {
        var actual = transaccion ?? throw new InvalidOperationException("No hay una transacción iniciada.");
        transaccion = null;
        await using (actual)
        {
            await actual.CommitAsync(cancellationToken);
        }
    }

    public async Task RevertirAsync(CancellationToken cancellationToken)
    {
        var actual = transaccion ?? throw new InvalidOperationException("No hay una transacción iniciada.");
        transaccion = null;
        await using (actual)
        {
            await actual.RollbackAsync(cancellationToken);
        }
    }
}
