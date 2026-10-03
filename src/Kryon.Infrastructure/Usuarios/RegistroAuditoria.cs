using Kryon.Core.Usuarios;
using Kryon.Infrastructure.Persistencia;

namespace Kryon.Infrastructure.Usuarios;

/// <summary>
/// Persiste registros de auditoría (solo inserción, FR-043 a FR-046). Guarda el registro tal como lo construyó el
/// caso de uso, con la empresa del actor y, en <see cref="TipoOperacion.Modificacion"/>, los cambios ya calculados.
/// </summary>
/// <remarks>
/// No abre, confirma ni revierte transacciones: si la operación auditada inició una con <see cref="IUnidadDeTrabajo"/>,
/// el registro se escribe dentro de ella sobre el mismo <see cref="KryonDbContext"/> y se confirma o revierte junto con
/// la operación. Los permisos de base de datos (DENY UPDATE, DELETE) y la RLS por empresa protegen además la tabla.
/// </remarks>
public sealed class RegistroAuditoria(KryonDbContext contexto) : IRegistroAuditoria
{
    public async Task RegistrarAsync(AuditoriaUsuario registro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registro);

        contexto.Set<AuditoriaUsuario>().Add(registro);
        await contexto.SaveChangesAsync(cancellationToken);
    }
}
