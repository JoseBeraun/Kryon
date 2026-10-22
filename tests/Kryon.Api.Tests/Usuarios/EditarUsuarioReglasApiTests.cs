using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Contracts.Usuarios;
using Kryon.Core.Usuarios;

using Microsoft.Data.SqlClient;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// Reglas y rechazos de <c>PUT /api/usuarios/{id}</c> (US4-8, US4-9, US4-11, US4-13, SC-006, EC-1, FR-003, FR-006,
/// FR-026, FR-035, FR-036, FR-046) sobre la API real, SQL Server real y los datos semilla. Cada prueba deja los datos
/// como los encontró.
/// </summary>
/// <remarks>
/// Para el último administrador, quien quita el rol a <c>ana</c> es <c>carla</c> con la capacidad de editar concedida
/// por su identidad de prueba: ana no puede cambiar su propio rol, y beto queda Inactivo para que ana sea la única
/// administradora. El recuento de administradores sale de los roles guardados, no de la identidad del actor.
/// </remarks>
public sealed class EditarUsuarioReglasApiTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    private const string EtagCualquiera = "\"AAAAAAAAB9E=\"";

    /// <summary>Id que no existe en ninguna empresa.</summary>
    private static readonly Guid IdInexistente = Guid.Parse("f0000000-0000-0000-0000-0000000000e4");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CambiarElPropioRol_Responde409()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var (version, rol) = await LeerAsync(ana, SemillaDatos.Ana.Id, ct);

        var respuesta = await EditarAsync(ana, SemillaDatos.Ana.Id, SemillaDatos.Ana.NombreCompleto, SemillaDatos.RolSinGestionA.Id, version, ct);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(CodigosError.NoPuedeCambiarSuPropioRol, (await ProblemaAsync(respuesta, ct)).Codigo);
        Assert.Equal((version, rol), await LeerAsync(ana, SemillaDatos.Ana.Id, ct));
    }

    [Fact]
    public async Task QuitarElRolAlUltimoAdministrador_Responde409()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);

        var desactivacion = new HttpRequestMessage(HttpMethod.Post, $"/api/usuarios/{SemillaDatos.Beto.Id}/desactivacion");
        desactivacion.Headers.IfMatch.Add(EntityTagHeaderValue.Parse((await LeerAsync(ana, SemillaDatos.Beto.Id, ct)).Version));
        var beto = await ana.SendAsync(desactivacion, ct);
        Assert.Equal(HttpStatusCode.OK, beto.StatusCode);
        try
        {
            var carla = fabrica.ClienteComo(SemillaDatos.Carla.Identidad, [SemillaDatos.Administrar, SemillaDatos.Editar]);
            var (version, rol) = await LeerAsync(ana, SemillaDatos.Ana.Id, ct);

            var respuesta = await EditarAsync(carla, SemillaDatos.Ana.Id, SemillaDatos.Ana.NombreCompleto, SemillaDatos.RolSinGestionA.Id, version, ct);

            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
            Assert.Equal(CodigosError.UltimoAdministrador, (await ProblemaAsync(respuesta, ct)).Codigo);
            Assert.Equal((version, rol), await LeerAsync(ana, SemillaDatos.Ana.Id, ct));
        }
        finally
        {
            var activacion = new HttpRequestMessage(HttpMethod.Post, $"/api/usuarios/{SemillaDatos.Beto.Id}/activacion");
            activacion.Headers.IfMatch.Add(beto.Headers.ETag!);
            await ana.SendAsync(activacion, ct);
        }
    }

    [Fact]
    public async Task RolDeOtraEmpresa_Responde409RolNoValido()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var carla = SemillaDatos.Carla;
        var (version, rol) = await LeerAsync(ana, carla.Id, ct);

        var respuesta = await EditarAsync(ana, carla.Id, carla.NombreCompleto, SemillaDatos.RolSinGestionB.Id, version, ct);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        Assert.Equal(CodigosError.RolNoValido, JsonSerializer.Deserialize<Problema>(cuerpo, Json)!.Codigo);
        Assert.DoesNotContain(SemillaDatos.EmpresaB.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SemillaDatos.RolSinGestionB.Nombre, cuerpo, StringComparison.Ordinal);
        Assert.Equal((version, rol), await LeerAsync(ana, carla.Id, ct));
    }

    [Fact]
    public async Task UsuarioDeOtraEmpresa_EsIndistinguibleDeUnoInexistente_YSeAuditaComoNoAccesible()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var berta = SemillaDatos.Berta;
        var antes = DateTimeOffset.UtcNow.AddSeconds(-5);

        var deB = await EditarAsync(ana, berta.Id, "Intruso", SemillaDatos.RolSinGestionA.Id, EtagCualquiera, ct);
        var inexistente = await EditarAsync(ana, IdInexistente, "Intruso", SemillaDatos.RolSinGestionA.Id, EtagCualquiera, ct);

        Assert.Equal(HttpStatusCode.NotFound, deB.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        var bytesDeB = await deB.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(bytesDeB, await inexistente.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal(CodigosError.UsuarioNoEncontrado, JsonSerializer.Deserialize<Problema>(bytesDeB, Json)!.Codigo);
        var cuerpo = System.Text.Encoding.UTF8.GetString(bytesDeB);
        foreach (var dato in new[] { berta.Id.ToString(), SemillaDatos.EmpresaB.ToString(), berta.NombreCompleto, berta.IdentificadorAcceso })
        {
            Assert.DoesNotContain(dato, cuerpo, StringComparison.OrdinalIgnoreCase);
        }

        var registros = await NoAccesiblesAsync(antes, ct);
        foreach (var id in new[] { berta.Id, IdInexistente })
        {
            var registro = Assert.Single(registros, r => r.Afectado == id);
            Assert.Equal(SemillaDatos.Ana.Id, registro.Actor);
            Assert.Equal(SemillaDatos.EmpresaA, registro.EmpresaId);
            Assert.Equal(OperacionSolicitada.Edicion, registro.OperacionSolicitada);
            Assert.InRange(registro.OcurridoEn, antes, DateTimeOffset.UtcNow.AddSeconds(5));
            Assert.Null(registro.CambiosJson);
        }
    }

    [Fact]
    public async Task SinLaCapacidadDeEditar_Responde403SinCambios()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var carla = fabrica.ClienteComo(SemillaDatos.Carla.Identidad);
        var beto = SemillaDatos.Beto;
        var (version, rol) = await LeerAsync(ana, beto.Id, ct);

        var respuesta = await EditarAsync(carla, beto.Id, "Beto Cambiado", beto.Rol.Id, version, ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(CodigosError.SinPermiso, (await ProblemaAsync(respuesta, ct)).Codigo);
        Assert.Equal((version, rol), await LeerAsync(ana, beto.Id, ct));
    }

    private static async Task<(string Version, Guid? Rol)> LeerAsync(HttpClient cliente, Guid id, CancellationToken ct)
    {
        var detalle = (await cliente.GetFromJsonAsync<DetalleUsuario>($"/api/usuarios/{id}", Json, ct))!;
        return (detalle.Version, detalle.Rol?.Id);
    }

    private static Task<HttpResponseMessage> EditarAsync(HttpClient cliente, Guid id, string nombre, Guid rolId, string version, CancellationToken ct)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Put, $"/api/usuarios/{id}")
        {
            Content = JsonContent.Create(new EditarUsuarioSolicitud { NombreCompleto = nombre, RolId = rolId }),
        };
        solicitud.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(version));
        return cliente.SendAsync(solicitud, ct);
    }

    private static async Task<Problema> ProblemaAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        return (await respuesta.Content.ReadFromJsonAsync<Problema>(Json, ct))!;
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
            SELECT ActorUsuarioId, EmpresaId, UsuarioAfectadoId, OcurridoEn, OperacionSolicitada, CambiosJson
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
                lector.GetGuid(0),
                lector.GetGuid(1),
                lector.GetGuid(2),
                lector.GetDateTimeOffset(3),
                lector.IsDBNull(4) ? null : (OperacionSolicitada)lector.GetByte(4),
                lector.IsDBNull(5) ? null : lector.GetString(5)));
        }

        return registros;
    }

    private sealed record RegistroNoAccesible(
        Guid Actor,
        Guid EmpresaId,
        Guid Afectado,
        DateTimeOffset OcurridoEn,
        OperacionSolicitada? OperacionSolicitada,
        string? CambiosJson);
}
