using System.Net;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Contracts.Usuarios;
using Kryon.Core.Usuarios;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// Aislamiento y contenido de <c>GET /api/usuarios</c> (US1-1, US1-2, US1-4, US1-5, US1-9; SC-001, SC-008) sobre la API
/// real, SQL Server real y los datos semilla. La empresa sale solo de la identidad de prueba: ninguna solicitud envía
/// <c>empresaId</c>.
/// </summary>
public sealed class ListarUsuariosAislamientoTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    private const string RutaUsuarios = "/api/usuarios";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly UsuarioSembrado[] UsuariosDeA =
        [.. SemillaDatos.Usuarios.Where(u => u.Rol.EmpresaId == SemillaDatos.EmpresaA)];

    private static readonly UsuarioSembrado[] UsuariosDeB =
        [.. SemillaDatos.Usuarios.Where(u => u.Rol.EmpresaId == SemillaDatos.EmpresaB)];

    [Fact]
    public async Task ComoAna_SoloDevuelveUsuariosDeA_YTotalNoCuentaB()
    {
        var (respuesta, cuerpo) = await ListarComoAsync(SemillaDatos.Ana, RutaUsuarios);
        var pagina = Deserializar<PaginaUsuarios>(cuerpo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(UsuariosDeA.Select(u => u.Id).Order(), pagina.Elementos.Select(e => e.Id).Order());
        Assert.Equal(UsuariosDeA.Length, pagina.Total);
        SinRastroDeB(cuerpo);
    }

    [Fact]
    public async Task ComoAna_CadaFilaTraeNombreIdentificadorRolYEstado()
    {
        var (_, cuerpo) = await ListarComoAsync(SemillaDatos.Ana, RutaUsuarios);
        var pagina = Deserializar<PaginaUsuarios>(cuerpo);

        Assert.All(UsuariosDeA, semilla =>
        {
            var fila = Assert.Single(pagina.Elementos, e => e.Id == semilla.Id);
            Assert.Equal(semilla.NombreCompleto, fila.NombreCompleto);
            Assert.Equal(semilla.IdentificadorAcceso, fila.IdentificadorAcceso);
            Assert.NotNull(fila.Rol);
            Assert.Equal(semilla.Rol.Id, fila.Rol.Id);
            Assert.Equal(semilla.Rol.Nombre, fila.Rol.Nombre);
            Assert.Equal(semilla.Estado == EstadoUsuario.Activo ? "activo" : "inactivo", fila.Estado);
        });
    }

    [Fact]
    public async Task ComoAna_SuFilaEsCuentaPropia_YNoPuedeDesactivarse()
    {
        var (_, cuerpo) = await ListarComoAsync(SemillaDatos.Ana, RutaUsuarios);
        var pagina = Deserializar<PaginaUsuarios>(cuerpo);

        // ana tiene la capacidad de desactivar: la API entrega la acción efectiva sobre su propia cuenta (FR-034).
        Assert.Contains(SemillaDatos.Desactivar, SemillaDatos.Ana.Identidad.Capacidades);
        var propia = Assert.Single(pagina.Elementos, e => e.Id == SemillaDatos.Ana.Id);
        Assert.True(propia.EsCuentaPropia);
        Assert.False(propia.Acciones.Desactivar);
        Assert.All(pagina.Elementos.Where(e => e.Id != SemillaDatos.Ana.Id), e => Assert.False(e.EsCuentaPropia));
    }

    [Fact]
    public async Task ComoAna_BuscarAna_NoIncluyeAnaBeta_NiLaCuentaEnElTotal()
    {
        var (respuesta, cuerpo) = await ListarComoAsync(SemillaDatos.Ana, $"{RutaUsuarios}?busqueda=Ana");
        var pagina = Deserializar<PaginaUsuarios>(cuerpo);

        // En A solo "Ana" contiene "Ana"; "Ana Beta" (B) también coincide, pero no puede aparecer ni contarse (US1-9).
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var fila = Assert.Single(pagina.Elementos);
        Assert.Equal(SemillaDatos.Ana.Id, fila.Id);
        Assert.Equal(1, pagina.Total);
        SinRastroDeB(cuerpo);
    }

    [Fact]
    public async Task ComoCarla_SinPermiso_Responde403SinDatosDeUsuarios()
    {
        var (respuesta, cuerpo) = await ListarComoAsync(SemillaDatos.Carla, RutaUsuarios);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = Deserializar<Problema>(cuerpo);
        Assert.Equal(CodigosError.SinPermiso, problema.Codigo);
        Assert.Equal(403, problema.Status);
        Assert.Null(problema.Actual);
        Assert.Null(problema.Errors);

        Assert.DoesNotContain("elementos", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.All(SemillaDatos.Usuarios, u =>
        {
            Assert.DoesNotContain(u.Id.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"\"{u.IdentificadorAcceso}\"", cuerpo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"\"{u.NombreCompleto}\"", cuerpo, StringComparison.OrdinalIgnoreCase);
        });
    }

    private async Task<(HttpResponseMessage Respuesta, string Cuerpo)> ListarComoAsync(UsuarioSembrado usuario, string ruta)
    {
        var ct = TestContext.Current.CancellationToken;
        var respuesta = await fabrica.ClienteComo(usuario.Identidad).GetAsync(ruta, ct);
        return (respuesta, await respuesta.Content.ReadAsStringAsync(ct));
    }

    private static T Deserializar<T>(string cuerpo) =>
        JsonSerializer.Deserialize<T>(cuerpo, Json) ?? throw new InvalidOperationException("La respuesta no tiene cuerpo.");

    /// <summary>Ningún id, nombre ni identificador de un usuario de B, ni el id de la empresa B, en el cuerpo.</summary>
    private static void SinRastroDeB(string cuerpo)
    {
        Assert.DoesNotContain(SemillaDatos.EmpresaB.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.All(UsuariosDeB, u =>
        {
            Assert.DoesNotContain(u.Id.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(u.NombreCompleto, cuerpo, StringComparison.Ordinal);
            Assert.DoesNotContain(u.IdentificadorAcceso, cuerpo, StringComparison.Ordinal);
        });
        Assert.All(SemillaDatos.Roles.Where(r => r.EmpresaId == SemillaDatos.EmpresaB), r =>
            Assert.DoesNotContain(r.Id.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase));
    }
}
