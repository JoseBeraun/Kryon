using System.Data;

using Microsoft.Data.SqlClient;

namespace Kryon.Api.Tests.Infraestructura;

/// <summary>
/// Marca de agua de la auditoría para las pruebas que comparten el SQL Server de su <see cref="KryonApiFactory"/>: el
/// último <c>Id</c> (columna <c>IDENTITY</c>, creciente) visible para una empresa justo antes de la operación que se
/// prueba. Los registros que produce esa operación son exactamente los de <c>Id</c> mayor que la marca, sin depender de
/// relojes ni de ventanas de tiempo que puedan incluir registros de otras pruebas de la misma clase.
/// </summary>
/// <remarks>
/// Solo lee: la auditoría es de solo inserción y nunca se modifica ni se borra. La lectura fija el
/// <c>SESSION_CONTEXT</c> de la empresa, así que la RLS se aplica igual que en la aplicación.
/// </remarks>
public static class MarcaAuditoria
{
    /// <summary>Último <c>Id</c> de auditoría visible para <paramref name="empresaId"/>; 0 si todavía no hay ninguno.</summary>
    public static async Task<long> UltimoIdAsync(string cadenaConexion, Guid empresaId, CancellationToken cancellationToken)
    {
        await using var conexion = new SqlConnection(new SqlConnectionStringBuilder(cadenaConexion) { Pooling = false }.ConnectionString);
        await conexion.OpenAsync(cancellationToken);

        await using (var fijar = conexion.CreateCommand())
        {
            fijar.CommandText = "EXEC sys.sp_set_session_context @key = N'EmpresaId', @value = @EmpresaId, @read_only = 1;";
            fijar.Parameters.Add("@EmpresaId", SqlDbType.UniqueIdentifier).Value = empresaId;
            await fijar.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var consulta = conexion.CreateCommand();
        consulta.CommandText = "SELECT ISNULL(MAX(Id), 0) FROM dbo.AuditoriaUsuarios;";
        return (long)(await consulta.ExecuteScalarAsync(cancellationToken))!;
    }
}
