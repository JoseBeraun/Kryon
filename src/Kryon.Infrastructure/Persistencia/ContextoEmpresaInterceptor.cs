using System.Data;
using System.Data.Common;

using Kryon.Core.Seguridad;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kryon.Infrastructure.Persistencia;

/// <summary>
/// Segunda capa de aislamiento multiempresa (research §R3): al abrir cada conexión fija
/// <c>SESSION_CONTEXT('EmpresaId')</c> con la empresa del <see cref="IContextoSolicitud"/> verificado, para que la
/// Row-Level Security de SQL Server filtre y bloquee por empresa.
/// </summary>
/// <remarks>
/// Se ejecuta en cada apertura lógica, también cuando la conexión física sale del pool. Al reutilizar una conexión,
/// SqlClient restablece la sesión (<c>sp_reset_connection</c>) antes del primer comando, lo que borra el contexto
/// de sesión anterior; por eso <c>@read_only = 1</c> no impide fijar la empresa de la nueva solicitud y sí impide
/// que cualquier SQL posterior la cambie dentro de la misma conexión lógica.
/// </remarks>
public sealed class ContextoEmpresaInterceptor(IContextoSolicitud contextoSolicitud) : DbConnectionInterceptor
{
    internal const string Sql =
        "EXEC sys.sp_set_session_context @key = N'EmpresaId', @value = @EmpresaId, @read_only = 1;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var comando = CrearComando(connection);
        comando.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var comando = CrearComando(connection);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CrearComando(DbConnection connection)
    {
        var comando = connection.CreateCommand();
        comando.CommandText = Sql;

        var empresa = comando.CreateParameter();
        empresa.ParameterName = "@EmpresaId";
        empresa.DbType = DbType.Guid;
        empresa.Value = contextoSolicitud.EmpresaId;
        comando.Parameters.Add(empresa);

        return comando;
    }
}
