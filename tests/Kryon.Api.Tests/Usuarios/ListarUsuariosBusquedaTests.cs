using System.Net;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Contracts.Usuarios;
using Kryon.Core.Usuarios;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// Búsqueda, filtros y paginación de <c>GET /api/usuarios</c> (US1-7, US1-8, US1-11; SC-008) sobre la API real, SQL
/// Server real y los datos semilla, sin añadir filas. El contrato no fija un orden, así que las páginas se comparan
/// como conjuntos. Todo resultado debe ser de la empresa de quien consulta.
/// </summary>
/// <remarks>
/// En la empresa A el nombre y el identificador de cada usuario coinciden salvo mayúsculas ("Ana"/"ana"), así que no
/// permiten saber qué campo produjo la coincidencia. Para eso se consulta como <c>berta</c> (administradora de B) sobre
/// "Ana Beta" / "ana.beta": "a B" solo está en el nombre y "a.b" solo en el identificador.
/// </remarks>
public sealed class ListarUsuariosBusquedaTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    private const string RutaUsuarios = "/api/usuarios";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task BuscarParteDelNombre_DevuelveSoloQuienLaContieneEnElNombre()
    {
        var pagina = await ListarOkAsync(SemillaDatos.Berta, ("busqueda", "a B"));

        Assert.Equal([SemillaDatos.AnaBeta.Id], pagina.Elementos.Select(e => e.Id));
        Assert.Equal(1, pagina.Total);
        SoloDeEmpresa(pagina, SemillaDatos.EmpresaB);
    }

    [Fact]
    public async Task BuscarParteDelIdentificador_DevuelveSoloQuienLaContieneEnElIdentificador()
    {
        var pagina = await ListarOkAsync(SemillaDatos.Berta, ("busqueda", "a.b"));

        Assert.Equal([SemillaDatos.AnaBeta.Id], pagina.Elementos.Select(e => e.Id));
        Assert.Equal(1, pagina.Total);
        SoloDeEmpresa(pagina, SemillaDatos.EmpresaB);
    }

    [Fact]
    public async Task BuscarParcial_ComoAna_DevuelveLasCoincidenciasDeA()
    {
        var pagina = await ListarOkAsync(SemillaDatos.Ana, ("busqueda", "ar"));

        Assert.Equal(Ids(SemillaDatos.Carla, SemillaDatos.Dario), pagina.Elementos.Select(e => e.Id).Order());
        Assert.Equal(2, pagina.Total);
        SoloDeEmpresa(pagina, SemillaDatos.EmpresaA);
    }

    /// <summary>
    /// Ningún usuario sembrado contiene <c>%</c> ni <c>_</c>. Si no se escaparan, <c>%</c> y <c>_</c> coincidirían
    /// con todos, <c>a%a</c> con "Ana" y "Carla", y <c>an_</c> con "Ana".
    /// </summary>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("a%a")]
    [InlineData("an_")]
    public async Task ComodinesEnLaBusqueda_SeTratanComoTexto(string busqueda)
    {
        var pagina = await ListarOkAsync(SemillaDatos.Ana, ("busqueda", busqueda));

        Assert.Empty(pagina.Elementos);
        Assert.Equal(0, pagina.Total);
    }

    [Fact]
    public async Task FiltrarPorRol_DevuelveSoloUsuariosConEseRol()
    {
        var pagina = await ListarOkAsync(SemillaDatos.Ana, ("rolId", SemillaDatos.RolSinGestionA.Id.ToString()));

        Assert.Equal(Ids(SemillaDatos.Carla, SemillaDatos.Dario), pagina.Elementos.Select(e => e.Id).Order());
        Assert.Equal(2, pagina.Total);
        Assert.All(pagina.Elementos, e => Assert.Equal(SemillaDatos.RolSinGestionA.Id, e.Rol?.Id));
    }

    [Theory]
    [InlineData("activo")]
    [InlineData("inactivo")]
    public async Task FiltrarPorEstado_DevuelveSoloUsuariosEnEseEstado(string estado)
    {
        var pagina = await ListarOkAsync(SemillaDatos.Ana, ("estado", estado));

        var esperados = SemillaDatos.Usuarios
            .Where(u => u.Rol.EmpresaId == SemillaDatos.EmpresaA && Estado(u) == estado)
            .Select(u => u.Id)
            .Order();
        Assert.Equal(esperados, pagina.Elementos.Select(e => e.Id).Order());
        Assert.Equal(esperados.Count(), pagina.Total);
        Assert.All(pagina.Elementos, e => Assert.Equal(estado, e.Estado));
    }

    [Fact]
    public async Task FiltrarPorRolYEstado_SeCombinanConAnd()
    {
        // Rol "Sin gestión": carla (activa) y dario (inactivo). Con OR también saldría carla.
        var pagina = await ListarOkAsync(
            SemillaDatos.Ana,
            ("rolId", SemillaDatos.RolSinGestionA.Id.ToString()),
            ("estado", "inactivo"));

        Assert.Equal([SemillaDatos.Dario.Id], pagina.Elementos.Select(e => e.Id));
        Assert.Equal(1, pagina.Total);
    }

    [Fact]
    public async Task BuscarYFiltrarPorEstado_SeCombinanConAnd()
    {
        // "ar": carla (activa) y dario (inactivo).
        var pagina = await ListarOkAsync(SemillaDatos.Ana, ("busqueda", "ar"), ("estado", "activo"));

        Assert.Equal([SemillaDatos.Carla.Id], pagina.Elementos.Select(e => e.Id));
        Assert.Equal(1, pagina.Total);
    }

    [Fact]
    public async Task Paginar_TotalCuentaTodasLasCoincidencias_YLasPaginasNoSeSolapan()
    {
        var usuariosDeA = SemillaDatos.Usuarios.Where(u => u.Rol.EmpresaId == SemillaDatos.EmpresaA).Select(u => u.Id).ToArray();

        var primera = await ListarOkAsync(SemillaDatos.Ana, ("pagina", "1"), ("tamanoPagina", "3"));
        var segunda = await ListarOkAsync(SemillaDatos.Ana, ("pagina", "2"), ("tamanoPagina", "3"));

        Assert.Equal((1, 3, 3), (primera.Pagina, primera.TamanoPagina, primera.Elementos.Count));
        Assert.Equal((2, 3, 1), (segunda.Pagina, segunda.TamanoPagina, segunda.Elementos.Count));
        Assert.Equal(usuariosDeA.Length, primera.Total);
        Assert.Equal(usuariosDeA.Length, segunda.Total);
        Assert.Empty(primera.Elementos.Select(e => e.Id).Intersect(segunda.Elementos.Select(e => e.Id)));
        Assert.Equal(usuariosDeA.Order(), primera.Elementos.Concat(segunda.Elementos).Select(e => e.Id).Order());
    }

    [Fact]
    public async Task Paginar_ConBusqueda_LaBusquedaSeMantieneEntrePaginas()
    {
        // "a" está en ana, carla y dario, no en beto.
        var coincidencias = Ids(SemillaDatos.Ana, SemillaDatos.Carla, SemillaDatos.Dario);

        var primera = await ListarOkAsync(SemillaDatos.Ana, ("busqueda", "a"), ("pagina", "1"), ("tamanoPagina", "2"));
        var segunda = await ListarOkAsync(SemillaDatos.Ana, ("busqueda", "a"), ("pagina", "2"), ("tamanoPagina", "2"));

        Assert.Equal((2, 1), (primera.Elementos.Count, segunda.Elementos.Count));
        Assert.Equal(coincidencias.Length, primera.Total);
        Assert.Equal(coincidencias.Length, segunda.Total);
        Assert.Empty(primera.Elementos.Select(e => e.Id).Intersect(segunda.Elementos.Select(e => e.Id)));
        Assert.Equal(coincidencias, primera.Elementos.Concat(segunda.Elementos).Select(e => e.Id).Order());
    }

    [Fact]
    public async Task TamanoPaginaPorEncimaDelMaximo_Responde400Validacion()
    {
        var maximo = fabrica.Services.GetRequiredService<IOptions<OpcionesUsuarios>>().Value.Paginacion.TamanoMaximo;

        var (respuesta, cuerpo) = await ListarAsync(SemillaDatos.Ana, ("tamanoPagina", (maximo + 1).ToString()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = Deserializar<Problema>(cuerpo);
        Assert.Equal(400, problema.Status);
        Assert.Equal(CodigosError.Validacion, problema.Codigo);

        // ProblemaValidacion exige `errors`, pero el contrato no fija el nombre de sus claves.
        Assert.NotNull(problema.Errors);
        Assert.NotEmpty(problema.Errors);
        Assert.DoesNotContain("elementos", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PaginaUsuarios> ListarOkAsync(UsuarioSembrado usuario, params (string Nombre, string Valor)[] parametros)
    {
        var (respuesta, cuerpo) = await ListarAsync(usuario, parametros);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return Deserializar<PaginaUsuarios>(cuerpo);
    }

    private async Task<(HttpResponseMessage Respuesta, string Cuerpo)> ListarAsync(
        UsuarioSembrado usuario,
        params (string Nombre, string Valor)[] parametros)
    {
        var ct = TestContext.Current.CancellationToken;
        var query = string.Join('&', parametros.Select(p => $"{p.Nombre}={Uri.EscapeDataString(p.Valor)}"));
        var respuesta = await fabrica.ClienteComo(usuario.Identidad).GetAsync($"{RutaUsuarios}?{query}", ct);
        return (respuesta, await respuesta.Content.ReadAsStringAsync(ct));
    }

    private static T Deserializar<T>(string cuerpo) =>
        JsonSerializer.Deserialize<T>(cuerpo, Json) ?? throw new InvalidOperationException("La respuesta no tiene cuerpo.");

    private static Guid[] Ids(params UsuarioSembrado[] usuarios) => [.. usuarios.Select(u => u.Id).Order()];

    private static string Estado(UsuarioSembrado usuario) => usuario.Estado == EstadoUsuario.Activo ? "activo" : "inactivo";

    /// <summary>Todas las filas son usuarios sembrados de <paramref name="empresaId"/>.</summary>
    private static void SoloDeEmpresa(PaginaUsuarios pagina, Guid empresaId)
    {
        var deLaEmpresa = SemillaDatos.Usuarios.Where(u => u.Rol.EmpresaId == empresaId).Select(u => u.Id).ToHashSet();
        Assert.All(pagina.Elementos, e => Assert.Contains(e.Id, deLaEmpresa));
    }
}
