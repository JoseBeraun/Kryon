using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Contracts.Usuarios;
using Kryon.Core.Usuarios;

using Microsoft.Data.SqlClient;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// Flujo de desactivación y activación (US2-2, US2-4, US2-6, SC-002, EC-2, FR-038): <c>POST
/// /api/usuarios/{id}/desactivacion</c> y <c>/activacion</c> con <c>If-Match</c>, sobre la API real, SQL Server real y
/// los datos semilla. Cada prueba deja a <c>carla</c> como la encontró (Activa), también si falla.
/// </summary>
/// <remarks>
/// Las pruebas de la clase comparten el SQL Server de <see cref="KryonApiFactory"/>. La auditoría de cada prueba se mide
/// desde una marca (<see cref="MarcaAuditoria"/>) tomada justo antes de su operación, no con ventanas de tiempo, para no
/// contar los registros de otras pruebas de la clase.
/// </remarks>
public sealed class CambiarEstadoTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Desactivar_Y_Activar_Responden200ConNuevoETag_YAuditanLos5Datos()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var carla = SemillaDatos.Carla;
        try
        {
            var versionInicial = await VersionAsync(ana, carla.Id, ct);
            var antes = DateTimeOffset.UtcNow.AddSeconds(-5);
            var marca = await MarcaAsync(ct);

            var desactivacion = await CambiarAsync(ana, carla.Id, "desactivacion", versionInicial, ct);

            Assert.Equal(HttpStatusCode.OK, desactivacion.StatusCode);
            var detalle = await LeerAsync<DetalleUsuario>(desactivacion, ct);
            Assert.Equal("inactivo", detalle.Estado);
            var etagDesactivado = desactivacion.Headers.ETag?.ToString();
            Assert.NotNull(etagDesactivado);
            Assert.NotEqual(versionInicial, etagDesactivado);
            Assert.Equal(etagDesactivado, detalle.Version);

            var activacion = await CambiarAsync(ana, carla.Id, "activacion", etagDesactivado, ct);

            Assert.Equal(HttpStatusCode.OK, activacion.StatusCode);
            Assert.Equal("activo", (await LeerAsync<DetalleUsuario>(activacion, ct)).Estado);
            Assert.NotEqual(etagDesactivado, activacion.Headers.ETag?.ToString());

            var registros = await AuditoriaAsync(carla.Id, marca, ct);
            Assert.Collection(
                registros,
                r => AssertRegistro(r, TipoOperacion.Desactivacion, carla.Id, antes),
                r => AssertRegistro(r, TipoOperacion.Activacion, carla.Id, antes));
        }
        finally
        {
            await RestaurarCarlaActivaAsync(ana);
        }
    }

    [Theory]
    [InlineData("desactivacion")]
    [InlineData("activacion")]
    public async Task SinIfMatch_Responde428FaltaVersion(string operacion)
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        try
        {
            var marca = await MarcaAsync(ct);

            var respuesta = await ana.PostAsync($"/api/usuarios/{SemillaDatos.Carla.Id}/{operacion}", content: null, ct);

            Assert.Equal(HttpStatusCode.PreconditionRequired, respuesta.StatusCode);
            var problema = await LeerProblemaAsync(respuesta, ct);
            Assert.Equal(428, problema.Status);
            Assert.Equal(CodigosError.FaltaVersion, problema.Codigo);
            Assert.Empty(await AuditoriaAsync(SemillaDatos.Carla.Id, marca, ct));
        }
        finally
        {
            await RestaurarCarlaActivaAsync(ana);
        }
    }

    [Fact]
    public async Task IfMatchObsoleto_Responde412UsuarioModificadoConActual_YNoAudita()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var carla = SemillaDatos.Carla;
        try
        {
            var versionObsoleta = await VersionAsync(ana, carla.Id, ct);

            // Otra operación cambia a carla: su versión deja de ser la que tenía el cliente.
            var desactivacion = await CambiarAsync(ana, carla.Id, "desactivacion", versionObsoleta, ct);
            Assert.Equal(HttpStatusCode.OK, desactivacion.StatusCode);
            var versionVigente = desactivacion.Headers.ETag!.ToString();

            // La marca se toma después de esa desactivación: solo cuenta lo que haga la petición obsoleta.
            var marca = await MarcaAsync(ct);

            var obsoleta = await CambiarAsync(ana, carla.Id, "activacion", versionObsoleta, ct);

            Assert.Equal(HttpStatusCode.PreconditionFailed, obsoleta.StatusCode);
            var problema = await LeerProblemaAsync(obsoleta, ct);
            Assert.Equal(412, problema.Status);
            Assert.Equal(CodigosError.UsuarioModificado, problema.Codigo);
            Assert.NotNull(problema.Actual);
            Assert.Equal(carla.Id, problema.Actual.Id);
            Assert.Equal("inactivo", problema.Actual.Estado);
            Assert.Equal(versionVigente, problema.Actual.Version);
            Assert.Empty(await AuditoriaAsync(carla.Id, marca, ct));
        }
        finally
        {
            await RestaurarCarlaActivaAsync(ana);
        }
    }

    /// <summary>Última marca de auditoría de la empresa A, antes de la operación que se prueba.</summary>
    private Task<long> MarcaAsync(CancellationToken ct) => MarcaAuditoria.UltimoIdAsync(fabrica.CadenaConexion, SemillaDatos.EmpresaA, ct);

    /// <summary>
    /// Deja a carla Activa, como en la semilla, aunque la prueba haya fallado a medias: así ninguna otra prueba de la clase
    /// hereda su estado. Usa la API con la versión vigente; si ya está Activa, no hace nada.
    /// </summary>
    private static async Task RestaurarCarlaActivaAsync(HttpClient cliente)
    {
        var detalle = await LeerAsync<DetalleUsuario>(await cliente.GetAsync($"/api/usuarios/{SemillaDatos.Carla.Id}", CancellationToken.None), CancellationToken.None);
        if (detalle.Estado != "activo")
        {
            var activacion = await CambiarAsync(cliente, SemillaDatos.Carla.Id, "activacion", detalle.Version, CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, activacion.StatusCode);
        }
    }

    private static void AssertRegistro(RegistroAuditoria registro, TipoOperacion tipo, Guid afectado, DateTimeOffset desde)
    {
        // Los 5 datos de SC-002: actor, momento, empresa, usuario afectado y tipo de operación.
        Assert.Equal(tipo, registro.Tipo);
        Assert.Equal(SemillaDatos.Ana.Id, registro.Actor);
        Assert.Equal(SemillaDatos.EmpresaA, registro.EmpresaId);
        Assert.Equal(afectado, registro.Afectado);
        Assert.InRange(registro.OcurridoEn, desde, DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Null(registro.OperacionSolicitada);
    }

    private static async Task<string> VersionAsync(HttpClient cliente, Guid usuarioId, CancellationToken ct)
    {
        var pagina = await LeerAsync<PaginaUsuarios>(await cliente.GetAsync("/api/usuarios", ct), ct);
        return Assert.Single(pagina.Elementos, u => u.Id == usuarioId).Version;
    }

    private static Task<HttpResponseMessage> CambiarAsync(HttpClient cliente, Guid usuarioId, string operacion, string version, CancellationToken ct)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, $"/api/usuarios/{usuarioId}/{operacion}");
        solicitud.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(version));
        return cliente.SendAsync(solicitud, ct);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta, CancellationToken ct) =>
        JsonSerializer.Deserialize<T>(await respuesta.Content.ReadAsStringAsync(ct), Json)
        ?? throw new InvalidOperationException("La respuesta no tiene cuerpo.");

    private static async Task<Problema> LeerProblemaAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        return await LeerAsync<Problema>(respuesta, ct);
    }

    /// <summary>
    /// Registros de auditoría de la empresa A sobre <paramref name="afectado"/> posteriores a la marca
    /// <paramref name="despuesDeId"/>, por SQL directo y en orden.
    /// </summary>
    private async Task<List<RegistroAuditoria>> AuditoriaAsync(Guid afectado, long despuesDeId, CancellationToken ct)
    {
        await using var conexion = new SqlConnection(new SqlConnectionStringBuilder(fabrica.CadenaConexion) { Pooling = false }.ConnectionString);
        await conexion.OpenAsync(ct);

        await using (var fijar = conexion.CreateCommand())
        {
            fijar.CommandText = "EXEC sys.sp_set_session_context @key = N'EmpresaId', @value = @EmpresaId, @read_only = 1;";
            fijar.Parameters.Add("@EmpresaId", SqlDbType.UniqueIdentifier).Value = SemillaDatos.EmpresaA;
            await fijar.ExecuteNonQueryAsync(ct);
        }

        await using var consulta = conexion.CreateCommand();
        consulta.CommandText = """
            SELECT TipoOperacion, ActorUsuarioId, EmpresaId, UsuarioAfectadoId, OcurridoEn, OperacionSolicitada
            FROM dbo.AuditoriaUsuarios
            WHERE UsuarioAfectadoId = @Afectado AND Id > @DespuesDeId
            ORDER BY Id;
            """;
        consulta.Parameters.Add("@Afectado", SqlDbType.UniqueIdentifier).Value = afectado;
        consulta.Parameters.Add("@DespuesDeId", SqlDbType.BigInt).Value = despuesDeId;
        await using var lector = await consulta.ExecuteReaderAsync(ct);

        var registros = new List<RegistroAuditoria>();
        while (await lector.ReadAsync(ct))
        {
            registros.Add(new RegistroAuditoria(
                (TipoOperacion)lector.GetByte(0),
                lector.GetGuid(1),
                lector.GetGuid(2),
                lector.GetGuid(3),
                lector.GetDateTimeOffset(4),
                lector.IsDBNull(5) ? null : (OperacionSolicitada)lector.GetByte(5)));
        }

        return registros;
    }

    private sealed record RegistroAuditoria(
        TipoOperacion Tipo,
        Guid Actor,
        Guid EmpresaId,
        Guid Afectado,
        DateTimeOffset OcurridoEn,
        OperacionSolicitada? OperacionSolicitada);
}
