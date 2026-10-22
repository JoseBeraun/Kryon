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
/// Flujo de <c>PUT /api/usuarios/{id}</c> (US4-3, US4-7, US4-10, US4-14, SC-002, SC-009, FR-038, FR-044) sobre la API
/// real, SQL Server real y los datos semilla. Cada prueba deja los datos como los encontró.
/// </summary>
/// <remarks>
/// <c>CambiosJson</c> sigue el formato de T027 (<c>[{"campo":…,"anterior":…,"nuevo":…}]</c>) con los nombres de campo
/// del contrato. Las claves de <c>errors</c> son los nombres de los campos del cuerpo, que la interfaz usa para mostrar
/// cada error junto a su campo (FR-025).
/// </remarks>
public sealed class EditarUsuarioApiTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EditarNombre_Responde200ConNuevoETag_YAuditaSoloElCampoCambiado()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var carla = SemillaDatos.Carla;
        var (version, _) = await LeerAsync(ana, carla.Id, ct);
        var antes = DateTimeOffset.UtcNow.AddSeconds(-5);

        var respuesta = await EditarAsync(ana, carla.Id, "Carla Ruiz", carla.Rol.Id, version, ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = (await respuesta.Content.ReadFromJsonAsync<DetalleUsuario>(Json, ct))!;
        Assert.Equal("Carla Ruiz", detalle.NombreCompleto);
        Assert.Equal(carla.Rol.Id, detalle.Rol?.Id);
        Assert.Equal(detalle.Version, respuesta.Headers.ETag?.ToString());
        Assert.NotEqual(version, detalle.Version);

        var registro = Assert.Single(await AuditoriaAsync(carla.Id, antes, ct));
        Assert.Equal(TipoOperacion.Modificacion, registro.Tipo);
        Assert.Equal(SemillaDatos.Ana.Id, registro.Actor);
        Assert.Equal(SemillaDatos.EmpresaA, registro.EmpresaId);
        Assert.InRange(registro.OcurridoEn, antes, DateTimeOffset.UtcNow.AddSeconds(5));
        var cambio = Assert.Single(Cambios(registro));
        Assert.Equal("nombreCompleto", cambio.GetProperty("campo").GetString());
        Assert.Equal(carla.NombreCompleto, cambio.GetProperty("anterior").GetString());
        Assert.Equal("Carla Ruiz", cambio.GetProperty("nuevo").GetString());

        Assert.Equal(HttpStatusCode.OK, (await EditarAsync(ana, carla.Id, carla.NombreCompleto, carla.Rol.Id, detalle.Version, ct)).StatusCode);
    }

    [Fact]
    public async Task UnAdministradorEditaAOtro_YElRolCambiadoSeAudita()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var beto = SemillaDatos.Beto;
        var (version, _) = await LeerAsync(ana, beto.Id, ct);
        var antes = DateTimeOffset.UtcNow.AddSeconds(-5);

        // beto pasa a otro rol de administración: ana sigue siendo administradora, así que se admite.
        var respuesta = await EditarAsync(ana, beto.Id, beto.NombreCompleto, SemillaDatos.RolAdministracionA.Id, version, ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = (await respuesta.Content.ReadFromJsonAsync<DetalleUsuario>(Json, ct))!;
        Assert.Equal(SemillaDatos.RolAdministracionA.Id, detalle.Rol?.Id);
        var cambio = Assert.Single(Cambios(Assert.Single(await AuditoriaAsync(beto.Id, antes, ct))));
        Assert.Equal("rolId", cambio.GetProperty("campo").GetString());

        Assert.Equal(HttpStatusCode.OK, (await EditarAsync(ana, beto.Id, beto.NombreCompleto, beto.Rol.Id, detalle.Version, ct)).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DatosInvalidos_Responden400ValidacionConErroresPorCampo_SinCambios(string nombre)
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var carla = SemillaDatos.Carla;
        var (version, _) = await LeerAsync(ana, carla.Id, ct);
        var antes = DateTimeOffset.UtcNow.AddSeconds(-5);

        var respuesta = await EditarAsync(ana, carla.Id, nombre, carla.Rol.Id, version, ct);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await ProblemaAsync(respuesta, ct);
        Assert.Equal(CodigosError.Validacion, problema.Codigo);
        Assert.NotNull(problema.Errors);
        Assert.Contains(problema.Errors.Keys, k => string.Equals(k, "nombreCompleto", StringComparison.OrdinalIgnoreCase));
        Assert.All(problema.Errors.Values, mensajes => Assert.NotEmpty(mensajes));
        Assert.Equal((version, carla.NombreCompleto), await LeerAsync(ana, carla.Id, ct));
        Assert.Empty(await AuditoriaAsync(carla.Id, antes, ct));
    }

    [Fact]
    public async Task IfMatchObsoleto_Responde412ConActual_SinCambiosNiAuditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var carla = SemillaDatos.Carla;
        var (obsoleta, _) = await LeerAsync(ana, carla.Id, ct);

        // Otra sesión guarda antes: la versión del cliente queda obsoleta.
        var primera = await EditarAsync(ana, carla.Id, "Carla Primera", carla.Rol.Id, obsoleta, ct);
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        var vigente = primera.Headers.ETag!.ToString();
        var antes = DateTimeOffset.UtcNow.AddSeconds(-1);

        var segunda = await EditarAsync(ana, carla.Id, "Carla Segunda", carla.Rol.Id, obsoleta, ct);

        Assert.Equal(HttpStatusCode.PreconditionFailed, segunda.StatusCode);
        var problema = await ProblemaAsync(segunda, ct);
        Assert.Equal(CodigosError.UsuarioModificado, problema.Codigo);
        Assert.NotNull(problema.Actual);
        Assert.Equal("Carla Primera", problema.Actual.NombreCompleto);
        Assert.Equal(vigente, problema.Actual.Version);
        Assert.Equal((vigente, "Carla Primera"), await LeerAsync(ana, carla.Id, ct));
        Assert.Empty(await AuditoriaAsync(carla.Id, antes, ct));

        Assert.Equal(HttpStatusCode.OK, (await EditarAsync(ana, carla.Id, carla.NombreCompleto, carla.Rol.Id, vigente, ct)).StatusCode);
    }

    [Fact]
    public async Task SinIfMatch_Responde428FaltaVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var carla = SemillaDatos.Carla;

        var respuesta = await EditarAsync(ana, carla.Id, "Carla Ruiz", carla.Rol.Id, version: null, ct);

        Assert.Equal(HttpStatusCode.PreconditionRequired, respuesta.StatusCode);
        Assert.Equal(CodigosError.FaltaVersion, (await ProblemaAsync(respuesta, ct)).Codigo);
        Assert.Equal(carla.NombreCompleto, (await LeerAsync(ana, carla.Id, ct)).Nombre);
    }

    private static async Task<(string Version, string Nombre)> LeerAsync(HttpClient cliente, Guid id, CancellationToken ct)
    {
        var detalle = (await cliente.GetFromJsonAsync<DetalleUsuario>($"/api/usuarios/{id}", Json, ct))!;
        return (detalle.Version, detalle.NombreCompleto);
    }

    private static Task<HttpResponseMessage> EditarAsync(HttpClient cliente, Guid id, string nombre, Guid rolId, string? version, CancellationToken ct)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Put, $"/api/usuarios/{id}")
        {
            Content = JsonContent.Create(new EditarUsuarioSolicitud { NombreCompleto = nombre, RolId = rolId }),
        };
        if (version is not null)
        {
            solicitud.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(version));
        }

        return cliente.SendAsync(solicitud, ct);
    }

    private static async Task<Problema> ProblemaAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        return (await respuesta.Content.ReadFromJsonAsync<Problema>(Json, ct))!;
    }

    private static JsonElement[] Cambios(RegistroAuditoria registro)
    {
        Assert.NotNull(registro.CambiosJson);
        using var documento = JsonDocument.Parse(registro.CambiosJson);
        return [.. documento.RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    /// <summary>Registros de auditoría de la empresa A sobre <paramref name="afectado"/>, por SQL directo y en orden.</summary>
    private async Task<List<RegistroAuditoria>> AuditoriaAsync(Guid afectado, DateTimeOffset desde, CancellationToken ct)
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
            SELECT TipoOperacion, ActorUsuarioId, EmpresaId, OcurridoEn, CambiosJson
            FROM dbo.AuditoriaUsuarios
            WHERE UsuarioAfectadoId = @Afectado AND OcurridoEn >= @Desde
            ORDER BY Id;
            """;
        consulta.Parameters.Add("@Afectado", SqlDbType.UniqueIdentifier).Value = afectado;
        consulta.Parameters.Add("@Desde", SqlDbType.DateTimeOffset).Value = desde;
        await using var lector = await consulta.ExecuteReaderAsync(ct);

        var registros = new List<RegistroAuditoria>();
        while (await lector.ReadAsync(ct))
        {
            registros.Add(new RegistroAuditoria(
                (TipoOperacion)lector.GetByte(0),
                lector.GetGuid(1),
                lector.GetGuid(2),
                lector.GetDateTimeOffset(3),
                lector.IsDBNull(4) ? null : lector.GetString(4)));
        }

        return registros;
    }

    private sealed record RegistroAuditoria(TipoOperacion Tipo, Guid Actor, Guid EmpresaId, DateTimeOffset OcurridoEn, string? CambiosJson);
}
