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
/// Reglas y rechazos de la desactivación y la activación (EC-1, EC-5, EC-6, FR-003, FR-006, FR-034, FR-036, FR-046)
/// sobre la API real, SQL Server real y los datos semilla. Cada prueba deja los datos como los encontró.
/// </summary>
/// <remarks>
/// Para el último administrador, el actor que intenta desactivar a <c>ana</c> es <c>carla</c> con la capacidad de
/// desactivar concedida por su identidad de prueba: ana y beto son los únicos administradores de A y ninguno puede
/// desactivarse a sí mismo, así que hace falta un tercer actor con esa capacidad. El recuento de administradores sale
/// de los roles guardados, no de la identidad del actor.
/// </remarks>
public sealed class CambiarEstadoReglasTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    private const string EtagCualquiera = "\"AAAAAAAAB9E=\"";

    /// <summary>Id que no existe en ninguna empresa.</summary>
    private static readonly Guid IdInexistente = Guid.Parse("f0000000-0000-0000-0000-0000000000ff");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DesactivarseASiMismo_Responde409()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);

        var respuesta = await CambiarAsync(ana, SemillaDatos.Ana.Id, "desactivacion", await VersionAsync(ana, SemillaDatos.Ana.Id, ct), ct);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(CodigosError.NoPuedeDesactivarseASiMismo, (await LeerProblemaAsync(respuesta, ct)).Codigo);
        Assert.Equal("activo", await EstadoAsync(ana, SemillaDatos.Ana.Id, ct));
    }

    [Fact]
    public async Task DesactivarAlUltimoAdministrador_Responde409()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);

        // ana desactiva a beto: ana queda como única administradora activa de A.
        var beto = await CambiarAsync(ana, SemillaDatos.Beto.Id, "desactivacion", await VersionAsync(ana, SemillaDatos.Beto.Id, ct), ct);
        Assert.Equal(HttpStatusCode.OK, beto.StatusCode);
        try
        {
            var carla = fabrica.ClienteComo(SemillaDatos.Carla.Identidad, [SemillaDatos.Administrar, SemillaDatos.Desactivar]);

            var respuesta = await CambiarAsync(carla, SemillaDatos.Ana.Id, "desactivacion", await VersionAsync(ana, SemillaDatos.Ana.Id, ct), ct);

            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
            Assert.Equal(CodigosError.UltimoAdministrador, (await LeerProblemaAsync(respuesta, ct)).Codigo);
            Assert.Equal("activo", await EstadoAsync(ana, SemillaDatos.Ana.Id, ct));
        }
        finally
        {
            await CambiarAsync(ana, SemillaDatos.Beto.Id, "activacion", beto.Headers.ETag!.ToString(), ct);
        }
    }

    /// <summary>
    /// Un id de B y uno inexistente responden el mismo 404, byte a byte, y dejan el mismo registro
    /// <c>UsuarioNoAccesible</c> bajo la empresa del actor (EC-1, FR-003, FR-046).
    /// </summary>
    [Theory]
    [InlineData("desactivacion", OperacionSolicitada.Desactivacion)]
    [InlineData("activacion", OperacionSolicitada.Activacion)]
    public async Task IdDeOtraEmpresa_EsIndistinguibleDeUnoInexistente_YSeAuditaComoNoAccesible(string operacion, OperacionSolicitada esperada)
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var idDeB = operacion == "desactivacion" ? SemillaDatos.Berta.Id : SemillaDatos.AnaBeta.Id;
        var antes = DateTimeOffset.UtcNow.AddSeconds(-5);

        var deB = await CambiarAsync(ana, idDeB, operacion, EtagCualquiera, ct);
        var inexistente = await CambiarAsync(ana, IdInexistente, operacion, EtagCualquiera, ct);

        Assert.Equal(HttpStatusCode.NotFound, deB.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        var cuerpoDeB = await deB.Content.ReadAsStringAsync(ct);
        Assert.Equal(cuerpoDeB, await inexistente.Content.ReadAsStringAsync(ct));
        Assert.Equal(deB.Content.Headers.ContentType?.ToString(), inexistente.Content.Headers.ContentType?.ToString());
        Assert.Equal(CodigosError.UsuarioNoEncontrado, (await LeerProblemaAsync(deB, ct)).Codigo);
        Assert.Null(deB.Headers.ETag);

        // Sin datos de B en la respuesta.
        Assert.DoesNotContain(SemillaDatos.EmpresaB.ToString(), cuerpoDeB, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(idDeB.ToString(), cuerpoDeB, StringComparison.OrdinalIgnoreCase);

        // Mismo registro para los dos: actor, empresa del actor, id solicitado, operación y momento; nada de B.
        var registros = await NoAccesiblesAsync(antes, ct);
        foreach (var id in new[] { idDeB, IdInexistente })
        {
            var registro = Assert.Single(registros, r => r.Afectado == id && r.OperacionSolicitada == esperada);
            Assert.Equal(TipoOperacion.UsuarioNoAccesible, registro.Tipo);
            Assert.Equal(SemillaDatos.Ana.Id, registro.Actor);
            Assert.Equal(SemillaDatos.EmpresaA, registro.EmpresaId);
            Assert.InRange(registro.OcurridoEn, antes, DateTimeOffset.UtcNow.AddSeconds(5));
            Assert.Null(registro.CambiosJson);
        }

        Assert.DoesNotContain(registros, r => r.EmpresaId == SemillaDatos.EmpresaB);
    }

    [Theory]
    [InlineData("desactivacion")]
    [InlineData("activacion")]
    public async Task RolSinLaCapacidad_Responde403SinCambios(string operacion)
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var carla = fabrica.ClienteComo(SemillaDatos.Carla.Identidad);
        var objetivo = operacion == "desactivacion" ? SemillaDatos.Beto : SemillaDatos.Dario;
        var estadoAntes = await EstadoAsync(ana, objetivo.Id, ct);

        var respuesta = await CambiarAsync(carla, objetivo.Id, operacion, await VersionAsync(ana, objetivo.Id, ct), ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(CodigosError.SinPermiso, (await LeerProblemaAsync(respuesta, ct)).Codigo);
        Assert.Equal(estadoAntes, await EstadoAsync(ana, objetivo.Id, ct));
    }

    private static async Task<UsuarioResumen> UsuarioAsync(HttpClient cliente, Guid usuarioId, CancellationToken ct)
    {
        var respuesta = await cliente.GetAsync("/api/usuarios", ct);
        var pagina = JsonSerializer.Deserialize<PaginaUsuarios>(await respuesta.Content.ReadAsStringAsync(ct), Json)!;
        return Assert.Single(pagina.Elementos, u => u.Id == usuarioId);
    }

    private static async Task<string> VersionAsync(HttpClient cliente, Guid usuarioId, CancellationToken ct) =>
        (await UsuarioAsync(cliente, usuarioId, ct)).Version;

    private static async Task<string> EstadoAsync(HttpClient cliente, Guid usuarioId, CancellationToken ct) =>
        (await UsuarioAsync(cliente, usuarioId, ct)).Estado;

    private static Task<HttpResponseMessage> CambiarAsync(HttpClient cliente, Guid usuarioId, string operacion, string version, CancellationToken ct)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, $"/api/usuarios/{usuarioId}/{operacion}");
        solicitud.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(version));
        return cliente.SendAsync(solicitud, ct);
    }

    private static async Task<Problema> LeerProblemaAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        return JsonSerializer.Deserialize<Problema>(await respuesta.Content.ReadAsStringAsync(ct), Json)!;
    }

    /// <summary>Registros <c>UsuarioNoAccesible</c> desde <paramref name="desde"/>, leídos por SQL bajo la empresa A.</summary>
    private async Task<List<RegistroNoAccesible>> NoAccesiblesAsync(DateTimeOffset desde, CancellationToken ct)
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
            SELECT TipoOperacion, ActorUsuarioId, EmpresaId, UsuarioAfectadoId, OcurridoEn, OperacionSolicitada, CambiosJson
            FROM dbo.AuditoriaUsuarios
            WHERE TipoOperacion = @Tipo AND OcurridoEn >= @Desde;
            """;
        consulta.Parameters.Add("@Tipo", SqlDbType.TinyInt).Value = (byte)TipoOperacion.UsuarioNoAccesible;
        consulta.Parameters.Add("@Desde", SqlDbType.DateTimeOffset).Value = desde;
        await using var lector = await consulta.ExecuteReaderAsync(ct);

        var registros = new List<RegistroNoAccesible>();
        while (await lector.ReadAsync(ct))
        {
            registros.Add(new RegistroNoAccesible(
                (TipoOperacion)lector.GetByte(0),
                lector.GetGuid(1),
                lector.GetGuid(2),
                lector.GetGuid(3),
                lector.GetDateTimeOffset(4),
                lector.IsDBNull(5) ? null : (OperacionSolicitada)lector.GetByte(5),
                lector.IsDBNull(6) ? null : lector.GetString(6)));
        }

        return registros;
    }

    private sealed record RegistroNoAccesible(
        TipoOperacion Tipo,
        Guid Actor,
        Guid EmpresaId,
        Guid Afectado,
        DateTimeOffset OcurridoEn,
        OperacionSolicitada? OperacionSolicitada,
        string? CambiosJson);
}
