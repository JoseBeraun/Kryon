using System.Net;
using System.Web;

using AngleSharp.Dom;

using Bunit;

using Kryon.Contracts.Usuarios;
using Kryon.Web.Tests.Infraestructura;
using Kryon.Web.Usuarios;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Web.Tests.Usuarios;

/// <summary>
/// Estados del listado (<c>contracts/ui-usuarios.md</c> → <i>Listado</i>; US1-4, US1-6, US1-10, US1-11, EC-11, FR-005,
/// FR-013, FR-017, FR-018, FR-019, FR-050): estado vacío frente a sin resultados, paginación, anuncio de resultados y
/// <c>sin-permiso</c>. Renderiza la página real <c>Kryon.Web.Usuarios.ListadoUsuarios</c> (T051) con el
/// <see cref="ClienteUsuarios"/> real sobre el HTTP falso de <see cref="ContextoPruebaWeb"/>.
/// </summary>
/// <remarks>
/// Test-first, igual que <c>ListadoUsuariosTablaTests</c>: la página se localiza por nombre en tiempo de ejecución y,
/// mientras no exista, cada prueba falla con "ListadoUsuarios aún no está implementado (T051)".
/// </remarks>
public sealed class ListadoUsuariosEstadosTests : IAsyncDisposable
{
    private const string NombreComponente = "Kryon.Web.Usuarios.ListadoUsuarios";

    private const string TextoEstadoVacio = "No hay usuarios que mostrar";
    private const string TextoSinResultados = "No hay usuarios que coincidan con la búsqueda y los filtros";
    private const string Limpiar = "Limpiar búsqueda y filtros";

    private readonly ContextoPruebaWeb contexto = new();

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    [Fact]
    public void SinCriterios_YSinUsuarios_MuestraElEstadoVacio()
    {
        var corte = RenderizarListado("/usuarios", _ => Pagina(total: 0, pagina: 1, tamanoPagina: 25, elementos: 0));

        corte.WaitForAssertion(() => Assert.Contains(TextoEstadoVacio, Texto(corte), StringComparison.Ordinal));
        Assert.DoesNotContain(TextoSinResultados, Texto(corte), StringComparison.Ordinal);
        Assert.DoesNotContain(Controles(corte), c => NombreAccesible(c) == Limpiar);
        Assert.Empty(corte.FindAll("tbody tr"));
    }

    [Fact]
    public void ConCriterios_YSinCoincidencias_MuestraSinResultadosYLimpiar()
    {
        var corte = RenderizarListado("/usuarios?busqueda=zzzz", _ => Pagina(total: 0, pagina: 1, tamanoPagina: 25, elementos: 0));

        corte.WaitForAssertion(() => Assert.Contains(TextoSinResultados, Texto(corte), StringComparison.Ordinal));
        Assert.DoesNotContain(TextoEstadoVacio, Texto(corte), StringComparison.Ordinal);
        Assert.Single(Controles(corte), c => NombreAccesible(c) == Limpiar);
        Assert.Empty(corte.FindAll("tbody tr"));

        // La búsqueda de la query string llegó a la API.
        Assert.Contains(contexto.Http.Solicitudes, s => EsListado(s) && Query(s)["busqueda"] == "zzzz");
    }

    /// <summary>73 usuarios en páginas de 25: 3 páginas; la última tiene 23.</summary>
    [Theory]
    [InlineData(1, 25, "Página 1 de 3 · 73 usuarios", true, false)]
    [InlineData(2, 25, "Página 2 de 3 · 73 usuarios", false, false)]
    [InlineData(3, 23, "Página 3 de 3 · 73 usuarios", false, true)]
    public void Paginacion_MuestraPaginaDeTotal_YDeshabilitaLosExtremos(
        int pagina,
        int elementos,
        string textoEsperado,
        bool anteriorDeshabilitado,
        bool siguienteDeshabilitado)
    {
        var uri = pagina == 1 ? "/usuarios" : $"/usuarios?pagina={pagina}";
        var corte = RenderizarListado(uri, _ => Pagina(total: 73, pagina: pagina, tamanoPagina: 25, elementos: elementos));

        var nav = corte.WaitForElement("nav[aria-label='Paginación de usuarios']");
        Assert.Contains(textoEsperado, Normalizar(nav.TextContent), StringComparison.Ordinal);

        var anterior = Assert.Single(Controles(nav), c => NombreAccesible(c) == "Página anterior");
        var siguiente = Assert.Single(Controles(nav), c => NombreAccesible(c) == "Página siguiente");
        Assert.Equal(anteriorDeshabilitado, Deshabilitado(anterior));
        Assert.Equal(siguienteDeshabilitado, Deshabilitado(siguiente));
    }

    [Fact]
    public void AlCambiarLaBusqueda_AnunciaLosResultadosEnLaRegionViva()
    {
        var corte = RenderizarListado(
            "/usuarios",
            query => query["busqueda"] == "Ana"
                ? Pagina(total: 12, pagina: 1, tamanoPagina: 25, elementos: 12)
                : Pagina(total: 37, pagina: 1, tamanoPagina: 25, elementos: 25));
        corte.WaitForAssertion(() => Assert.NotEmpty(corte.FindAll("tbody tr")));

        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo("/usuarios?busqueda=Ana");

        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[aria-live='polite']"),
            region => Normalizar(region.TextContent).Contains("12 usuarios encontrados", StringComparison.Ordinal)));
    }

    [Fact]
    public void SinPermiso_MuestraElMensajeEnLaRegionDeError_YNoMuestraLaTabla()
    {
        var problema = new Problema
        {
            Type = "https://kryon.app/errores/sin-permiso",
            Title = "Sin permiso",
            Status = 403,
            Codigo = CodigosError.SinPermiso,
        };
        var corte = RenderizarListado("/usuarios", _ => null, sinPermiso: problema);

        var esperado = TextosErrores.ParaCodigo(CodigosError.SinPermiso);
        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[role='alert']"),
            region => Normalizar(region.TextContent).Contains(esperado, StringComparison.Ordinal)));
        Assert.Empty(corte.FindAll("table"));
        Assert.Empty(corte.FindAll("tbody tr"));
    }

    /// <summary>
    /// Renderiza la página real de T051 en <paramref name="uri"/>. <c>GET /api/usuarios</c> responde con
    /// <paramref name="paginaPara"/> según la query recibida, o con <paramref name="sinPermiso"/> si se indica;
    /// <c>GET /api/usuarios/roles-asignables</c> (filtros) responde lo mismo que el permiso del escenario.
    /// </summary>
    private IRenderedComponent<IComponent> RenderizarListado(
        string uri,
        Func<System.Collections.Specialized.NameValueCollection, PaginaUsuarios?> paginaPara,
        Problema? sinPermiso = null)
    {
        var tipo = typeof(ClienteUsuarios).Assembly.GetType(NombreComponente);
        if (tipo is null)
        {
            Assert.Fail("ListadoUsuarios aún no está implementado (T051).");
        }

        Assert.True(typeof(IComponent).IsAssignableFrom(tipo), $"{NombreComponente} no es un componente de Blazor.");

        contexto.Http.Responder = (solicitud, _) => (solicitud.RequestUri!.AbsolutePath, sinPermiso) switch
        {
            (_, not null) when solicitud.RequestUri.AbsolutePath.StartsWith("/api/usuarios", StringComparison.Ordinal) =>
                HttpFalso.ConProblema(sinPermiso),
            ("/api/usuarios", null) => HttpFalso.Json(HttpStatusCode.OK, paginaPara(HttpUtility.ParseQueryString(solicitud.RequestUri.Query))),
            ("/api/usuarios/roles-asignables", null) => HttpFalso.Json(HttpStatusCode.OK, Array.Empty<RolResumen>()),
            var (ruta, _) => throw new InvalidOperationException($"Solicitud no prevista en T047: {solicitud.Method} {ruta}"),
        };
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo(uri);

        return (IRenderedComponent<IComponent>)contexto.Render(builder =>
        {
            builder.OpenComponent(0, tipo);
            builder.CloseComponent();
        });
    }

    private static bool EsListado(SolicitudRecibida s) =>
        s.Solicitud.Method == HttpMethod.Get && s.Solicitud.RequestUri!.AbsolutePath == "/api/usuarios";

    private static System.Collections.Specialized.NameValueCollection Query(SolicitudRecibida s) =>
        HttpUtility.ParseQueryString(s.Solicitud.RequestUri!.Query);

    private static string Texto(IRenderedComponent<IComponent> corte) =>
        Normalizar(string.Join(' ', corte.Nodes.Select(n => n.TextContent)));

    private static IEnumerable<IElement> Controles(IRenderedComponent<IComponent> corte) =>
        corte.Nodes.OfType<IElement>().SelectMany(r => (r.Matches("button, a") ? [r] : Enumerable.Empty<IElement>()).Concat(r.QuerySelectorAll("button, a")));

    private static IEnumerable<IElement> Controles(IElement raiz) => raiz.QuerySelectorAll("button, a");

    /// <summary>Deshabilitado con <c>disabled</c> o con <c>aria-disabled="true"</c>.</summary>
    private static bool Deshabilitado(IElement control) =>
        control.HasAttribute("disabled") || control.GetAttribute("aria-disabled") == "true";

    /// <summary>
    /// Nombre accesible de un botón o enlace, sin exigir una técnica concreta: el texto de los elementos de
    /// <c>aria-labelledby</c> (en su orden); si no, <c>aria-label</c>; si no, el texto del propio control.
    /// </summary>
    private static string NombreAccesible(IElement control)
    {
        if (control.GetAttribute("aria-labelledby") is { Length: > 0 } ids)
        {
            var raiz = control;
            while (raiz.ParentElement is not null)
            {
                raiz = raiz.ParentElement;
            }

            var referenciados = ids.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => raiz.Id == id ? raiz : raiz.QuerySelector($"[id='{id}']"))
                .OfType<IElement>()
                .Select(e => e.GetAttribute("aria-label") is { Length: > 0 } etiqueta ? etiqueta : e.TextContent);
            return Normalizar(string.Join(' ', referenciados));
        }

        return control.GetAttribute("aria-label") is { Length: > 0 } propia ? Normalizar(propia) : Normalizar(control.TextContent);
    }

    private static string Normalizar(string texto) => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Página con <paramref name="elementos"/> usuarios de ejemplo, coherente con el total y el tamaño.</summary>
    private static PaginaUsuarios Pagina(int total, int pagina, int tamanoPagina, int elementos) => new()
    {
        Elementos = [.. Enumerable.Range(1, elementos).Select(i => Usuario((pagina - 1) * tamanoPagina + i))],
        Total = total,
        Pagina = pagina,
        TamanoPagina = tamanoPagina,
        PuedeRegistrar = false,
    };

    private static UsuarioResumen Usuario(int numero) => new()
    {
        Id = new Guid(numero, 0, 0, new byte[8]),
        NombreCompleto = $"Usuario {numero}",
        IdentificadorAcceso = $"usuario.{numero}",
        Rol = new RolResumen { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Nombre = "Ventas" },
        Estado = "activo",
        EsCuentaPropia = false,
        Version = $"\"{numero}\"",
        Acciones = new AccionesPermitidas
        {
            VerDetalle = true,
            Editar = false,
            CambiarRol = false,
            EditarIdentificador = false,
            Activar = false,
            Desactivar = false,
        },
    };
}
