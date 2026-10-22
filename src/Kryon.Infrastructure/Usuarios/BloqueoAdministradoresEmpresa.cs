using System.Data;

using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;
using Kryon.Infrastructure.Persistencia;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Kryon.Infrastructure.Usuarios;

/// <summary>
/// Bloqueo exclusivo por empresa para las operaciones que pueden reducir los administradores activos (FR-036, EC-6,
/// research §R5): <c>sp_getapplock</c> sobre el recurso <c>usuarios-admin:{EmpresaId}</c>, con propietario la
/// transacción, en la misma conexión y transacción de <see cref="KryonDbContext"/>. Se libera al confirmar o revertir.
/// </summary>
/// <remarks>
/// La empresa sale solo del <see cref="IContextoSolicitud"/> verificado, así que el bloqueo nunca afecta a otra empresa.
/// Falla cerrado: sin transacción iniciada o si SQL Server no concede el bloqueo, lanza una excepción y la operación no
/// continúa.
/// </remarks>
public sealed class BloqueoAdministradoresEmpresa(KryonDbContext contexto, IContextoSolicitud contextoSolicitud)
    : IBloqueoAdministradoresEmpresa
{
    public async Task AdquirirAsync(CancellationToken cancellationToken)
    {
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("El bloqueo de administradores exige una transacción iniciada.");
        }

        var recurso = new SqlParameter("@Recurso", SqlDbType.NVarChar, 255) { Value = $"usuarios-admin:{contextoSolicitud.EmpresaId:D}" };
        var resultado = new SqlParameter("@Resultado", SqlDbType.Int) { Direction = ParameterDirection.Output };

        await contexto.Database.ExecuteSqlRawAsync(
            "EXEC @Resultado = sys.sp_getapplock @Resource = @Recurso, @LockMode = N'Exclusive', @LockOwner = N'Transaction';",
            [recurso, resultado],
            cancellationToken);

        // 0: concedido; 1: concedido tras esperar. Negativo: tiempo agotado, cancelado, interbloqueo o error.
        if (resultado.Value is not int codigo || codigo < 0)
        {
            throw new InvalidOperationException($"No se pudo adquirir el bloqueo de administradores de la empresa (sp_getapplock: {resultado.Value}).");
        }
    }
}
