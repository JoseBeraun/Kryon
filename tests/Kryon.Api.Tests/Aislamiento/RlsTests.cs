using System.Data;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Core.Usuarios;

using Microsoft.Data.SqlClient;

namespace Kryon.Api.Tests.Aislamiento;

/// <summary>
/// Prueba directa de Row-Level Security (quickstart V2): SQL contra el SQL Server del contenedor, sin EF Core ni la
/// API, para demostrar que el aislamiento no depende del filtro global de EF Core.
/// </summary>
/// <remarks>
/// Cada escenario abre su propia conexión <b>sin pooling</b> y fija <c>SESSION_CONTEXT('EmpresaId')</c> una sola vez,
/// igual que <c>ContextoEmpresaInterceptor</c> (<c>@read_only = 1</c>); así ninguna conexión hereda la empresa de otra.
/// </remarks>
public sealed class RlsTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    /// <summary>Usuario de base de datos solo de esta prueba (sin login), miembro del rol de la aplicación (T026).</summary>
    private const string UsuarioRolAplicacion = "prueba_kryon_aplicacion";

    private const int ErrorPredicadoBloqueo = 33504;
    private const int ErrorPermisoDenegado = 229;

    [Fact]
    public async Task ConSessionContextDeA_SinFiltroDeEf_SoloDevuelveFilasDeA()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConEmpresaAsync(SemillaDatos.EmpresaA, ct);

        var filas = await LeerUsuariosAsync(conexion, ct);

        var idsDeA = SemillaDatos.Usuarios.Where(u => u.Rol.EmpresaId == SemillaDatos.EmpresaA).Select(u => u.Id);
        Assert.All(filas, fila => Assert.Equal(SemillaDatos.EmpresaA, fila.EmpresaId));
        Assert.Equal(idsDeA.Order(), filas.Select(fila => fila.Id).Order());
    }

    [Fact]
    public async Task ConSessionContextDeA_InsertarConEmpresaB_EsBloqueado()
    {
        var ct = TestContext.Current.CancellationToken;
        var idNuevo = Guid.Parse("c0000000-0000-0000-0000-000000000035");

        await using (var conexionA = await AbrirConEmpresaAsync(SemillaDatos.EmpresaA, ct))
        {
            await using var insertar = conexionA.CreateCommand();
            insertar.CommandText = """
                INSERT INTO dbo.Usuarios (Id, EmpresaId, NombreCompleto, IdentificadorAcceso, RolId, Estado, CreadoEn, ModificadoEn)
                VALUES (@Id, @EmpresaId, @NombreCompleto, @IdentificadorAcceso, @RolId, @Estado, @Momento, @Momento);
                """;
            insertar.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = idNuevo;
            insertar.Parameters.Add("@EmpresaId", SqlDbType.UniqueIdentifier).Value = SemillaDatos.EmpresaB;
            insertar.Parameters.Add("@NombreCompleto", SqlDbType.NVarChar, 200).Value = "Intruso";
            insertar.Parameters.Add("@IdentificadorAcceso", SqlDbType.NVarChar, 256).Value = "intruso";
            insertar.Parameters.Add("@RolId", SqlDbType.UniqueIdentifier).Value = SemillaDatos.RolSinGestionB.Id;
            insertar.Parameters.Add("@Estado", SqlDbType.TinyInt).Value = (byte)EstadoUsuario.Activo;
            insertar.Parameters.Add("@Momento", SqlDbType.DateTimeOffset).Value = SemillaDatos.Momento;

            var error = await Assert.ThrowsAsync<SqlException>(() => insertar.ExecuteNonQueryAsync(ct));
            Assert.Equal(ErrorPredicadoBloqueo, error.Number);
        }

        // La fila no existe ni siquiera para B, y los usuarios sembrados de B siguen intactos.
        await using var conexionB = await AbrirConEmpresaAsync(SemillaDatos.EmpresaB, ct);
        var filasDeB = await LeerUsuariosAsync(conexionB, ct);
        Assert.DoesNotContain(filasDeB, fila => fila.Id == idNuevo);
        Assert.Equal(
            SemillaDatos.Usuarios.Where(u => u.Rol.EmpresaId == SemillaDatos.EmpresaB).Select(u => u.Id).Order(),
            filasDeB.Select(fila => fila.Id).Order());
    }

    [Fact]
    public async Task RolDeLaAplicacion_NoPuedeModificarNiBorrarAuditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConEmpresaAsync(SemillaDatos.EmpresaA, ct);

        long idRegistro;
        await using (var insertar = conexion.CreateCommand())
        {
            insertar.CommandText = """
                INSERT INTO dbo.AuditoriaUsuarios (EmpresaId, ActorUsuarioId, OcurridoEn, TipoOperacion, UsuarioAfectadoId)
                OUTPUT INSERTED.Id
                VALUES (@EmpresaId, @Actor, @Momento, @Tipo, @Afectado);
                """;
            insertar.Parameters.Add("@EmpresaId", SqlDbType.UniqueIdentifier).Value = SemillaDatos.EmpresaA;
            insertar.Parameters.Add("@Actor", SqlDbType.UniqueIdentifier).Value = SemillaDatos.Ana.Id;
            insertar.Parameters.Add("@Momento", SqlDbType.DateTimeOffset).Value = SemillaDatos.Momento;
            insertar.Parameters.Add("@Tipo", SqlDbType.TinyInt).Value = (byte)TipoOperacion.Activacion;
            insertar.Parameters.Add("@Afectado", SqlDbType.UniqueIdentifier).Value = SemillaDatos.Dario.Id;
            idRegistro = (long)(await insertar.ExecuteScalarAsync(ct))!;
        }

        await EjecutarAsync(conexion, $"""
            IF USER_ID(N'{UsuarioRolAplicacion}') IS NULL CREATE USER [{UsuarioRolAplicacion}] WITHOUT LOGIN;
            IF IS_ROLEMEMBER(N'kryon_aplicacion', N'{UsuarioRolAplicacion}') = 0 ALTER ROLE [kryon_aplicacion] ADD MEMBER [{UsuarioRolAplicacion}];
            """, ct);

        await EjecutarAsync(conexion, $"EXECUTE AS USER = N'{UsuarioRolAplicacion}';", ct);
        try
        {
            await using var modificar = conexion.CreateCommand();
            modificar.CommandText = "UPDATE dbo.AuditoriaUsuarios SET TipoOperacion = @Tipo WHERE Id = @Id;";
            modificar.Parameters.Add("@Tipo", SqlDbType.TinyInt).Value = (byte)TipoOperacion.Desactivacion;
            modificar.Parameters.Add("@Id", SqlDbType.BigInt).Value = idRegistro;
            Assert.Equal(ErrorPermisoDenegado, (await Assert.ThrowsAsync<SqlException>(() => modificar.ExecuteNonQueryAsync(ct))).Number);

            await using var borrar = conexion.CreateCommand();
            borrar.CommandText = "DELETE FROM dbo.AuditoriaUsuarios WHERE Id = @Id;";
            borrar.Parameters.Add("@Id", SqlDbType.BigInt).Value = idRegistro;
            Assert.Equal(ErrorPermisoDenegado, (await Assert.ThrowsAsync<SqlException>(() => borrar.ExecuteNonQueryAsync(ct))).Number);
        }
        finally
        {
            await EjecutarAsync(conexion, "REVERT;", ct);
        }

        // El registro sigue igual.
        await using var leer = conexion.CreateCommand();
        leer.CommandText = "SELECT TipoOperacion FROM dbo.AuditoriaUsuarios WHERE Id = @Id;";
        leer.Parameters.Add("@Id", SqlDbType.BigInt).Value = idRegistro;
        Assert.Equal((byte)TipoOperacion.Activacion, (byte)(await leer.ExecuteScalarAsync(ct))!);
    }

    private async Task<SqlConnection> AbrirConEmpresaAsync(Guid empresaId, CancellationToken ct)
    {
        var cadena = new SqlConnectionStringBuilder(fabrica.CadenaConexion) { Pooling = false }.ConnectionString;
        var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(ct);

        await using var fijar = conexion.CreateCommand();
        fijar.CommandText = "EXEC sys.sp_set_session_context @key = N'EmpresaId', @value = @EmpresaId, @read_only = 1;";
        fijar.Parameters.Add("@EmpresaId", SqlDbType.UniqueIdentifier).Value = empresaId;
        await fijar.ExecuteNonQueryAsync(ct);

        return conexion;
    }

    private static async Task<List<(Guid Id, Guid EmpresaId)>> LeerUsuariosAsync(SqlConnection conexion, CancellationToken ct)
    {
        await using var consulta = conexion.CreateCommand();
        consulta.CommandText = "SELECT Id, EmpresaId FROM dbo.Usuarios;";
        await using var lector = await consulta.ExecuteReaderAsync(ct);

        var filas = new List<(Guid Id, Guid EmpresaId)>();
        while (await lector.ReadAsync(ct))
        {
            filas.Add((lector.GetGuid(0), lector.GetGuid(1)));
        }

        return filas;
    }

    private static async Task EjecutarAsync(SqlConnection conexion, string sql, CancellationToken ct)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync(ct);
    }
}
