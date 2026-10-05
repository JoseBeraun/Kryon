using System.Net;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Contracts.Usuarios;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// <c>GET /api/usuarios/roles-asignables</c> (FR-015, FR-026) sobre la API real, SQL Server real y los datos semilla.
/// El contrato devuelve los "roles de la empresa actual": todos los de esa empresa, tengan o no usuarios, y ninguno de
/// otra. No fija un orden, así que se comparan como conjuntos.
/// </summary>
public sealed class RolesAsignablesTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    private const string RutaRolesAsignables = "/api/usuarios/roles-asignables";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ComoAna_DevuelveExactamenteLosRolesDeA()
    {
        var (respuesta, cuerpo) = await ObtenerComoAsync(SemillaDatos.Ana);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var roles = Deserializar<List<RolResumen>>(cuerpo);
        var esperados = SemillaDatos.Roles
            .Where(r => r.EmpresaId == SemillaDatos.EmpresaA)
            .Select(r => new RolResumen { Id = r.Id, Nombre = r.Nombre })
            .OrderBy(r => r.Id);
        Assert.Equal(esperados, roles.OrderBy(r => r.Id));
    }

    [Fact]
    public async Task ComoAna_IncluyeUnRolDeASinUsuariosAsignados()
    {
        // Ningún usuario sembrado tiene "Sin desactivar": la lista es el catálogo de la empresa, no los roles en uso.
        Assert.DoesNotContain(SemillaDatos.Usuarios, u => u.Rol.Id == SemillaDatos.RolSinDesactivarA.Id);

        var (_, cuerpo) = await ObtenerComoAsync(SemillaDatos.Ana);

        Assert.Contains(Deserializar<List<RolResumen>>(cuerpo), r => r.Id == SemillaDatos.RolSinDesactivarA.Id);
    }

    [Fact]
    public async Task ComoAna_NoDevuelveNingunRolDeB()
    {
        var (_, cuerpo) = await ObtenerComoAsync(SemillaDatos.Ana);

        var ids = Deserializar<List<RolResumen>>(cuerpo).Select(r => r.Id).ToHashSet();
        Assert.All(SemillaDatos.Roles.Where(r => r.EmpresaId == SemillaDatos.EmpresaB), rolDeB =>
        {
            Assert.DoesNotContain(rolDeB.Id, ids);
            Assert.DoesNotContain(rolDeB.Id.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        });
        Assert.DoesNotContain(SemillaDatos.EmpresaB.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComoCarla_SinPermiso_Responde403SinRoles()
    {
        var (respuesta, cuerpo) = await ObtenerComoAsync(SemillaDatos.Carla);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = Deserializar<Problema>(cuerpo);
        Assert.Equal(403, problema.Status);
        Assert.Equal(CodigosError.SinPermiso, problema.Codigo);
        Assert.All(SemillaDatos.Roles, r => Assert.DoesNotContain(r.Id.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(HttpResponseMessage Respuesta, string Cuerpo)> ObtenerComoAsync(UsuarioSembrado usuario)
    {
        var ct = TestContext.Current.CancellationToken;
        var respuesta = await fabrica.ClienteComo(usuario.Identidad).GetAsync(RutaRolesAsignables, ct);
        return (respuesta, await respuesta.Content.ReadAsStringAsync(ct));
    }

    private static T Deserializar<T>(string cuerpo) =>
        JsonSerializer.Deserialize<T>(cuerpo, Json) ?? throw new InvalidOperationException("La respuesta no tiene cuerpo.");
}
