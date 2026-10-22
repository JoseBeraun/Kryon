using System.Net;

using AngleSharp.Dom;

using Bunit;

using Kryon.Contracts.Usuarios;
using Kryon.Web.Tests.Infraestructura;
using Kryon.Web.Usuarios;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Web.Tests.Usuarios;

/// <summary>
/// Recorrido completo Listado → Editar → Listado (T084; FR-022, FR-050; contracts/ui-usuarios.md → <i>Mensajes</i>):
/// el enlace "Editar a {nombre}" del listado lleva la query string del listado, Guardar y Cancelar vuelven con ella, y
/// tras guardar el listado anuncia "Usuario {nombre} guardado." en la región <c>aria-live="polite"</c>, una sola vez.
/// </summary>
/// <remarks>
/// bUnit no tiene enrutador: seguir un enlace es navegar a su <c>href</c> y renderizar la página de esa ruta, igual
/// que haría el <c>Router</c> de la aplicación.
/// </remarks>
public sealed class EdicionDesdeListadoTests : IAsyncDisposable
{
    private const string QueryListado = "?busqueda=ana&estado=activo&pagina=2";

    private static readonly RolResumen Ventas = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" };

    private static readonly DetalleUsuario Bruno = new()
    {
        Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
        NombreCompleto = "Bruno Díaz",
        IdentificadorAcceso = "bruno.diaz",
        Rol = Ventas,
        Estado = "activo",
        EsCuentaPropia = false,
        Version = "\"AAAAAAAAB9E=\"",
        Acciones = new() { VerDetalle = true, Editar = true, CambiarRol = true, EditarIdentificador = false, Activar = false, Desactivar = true },
    };

    private readonly ContextoPruebaWeb contexto = new();

    public EdicionDesdeListadoTests()
    {
        contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var guardado = Bruno with { NombreCompleto = "Bruno Díaz Ruiz", Version = "\"AAAAAAAAB9M=\"" };
        contexto.Http.Responder = (solicitud, _) => (solicitud.Method.Method, solicitud.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/api/usuarios") => HttpFalso.Json(HttpStatusCode.OK, new PaginaUsuarios
            {
                Elementos = [Bruno],
                Total = 26,
                Pagina = 2,
                TamanoPagina = 25,
                PuedeRegistrar = false,
            }),
            ("GET", "/api/usuarios/roles-asignables") => HttpFalso.Json(HttpStatusCode.OK, new[] { Ventas }),
            ("GET", var ruta) when ruta == $"/api/usuarios/{Bruno.Id}" => HttpFalso.Json(HttpStatusCode.OK, Bruno, Bruno.Version),
            ("PUT", var ruta) when ruta == $"/api/usuarios/{Bruno.Id}" => HttpFalso.Json(HttpStatusCode.OK, guardado, guardado.Version),
            var (metodo, ruta) => throw new InvalidOperationException($"Solicitud no prevista: {metodo} {ruta}"),
        };
    }

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    [Fact]
    public void Guardar_VuelveAlListadoConSuQuery_YElListadoAnunciaElExitoUnaVez()
    {
        var edicion = EntrarAEditarDesdeElListado();

        edicion.Find("[id='" + Etiqueta(edicion, "Nombre completo").GetAttribute("for") + "']").Change("Bruno Díaz Ruiz");
        edicion.Find("form").Submit();

        var navegacion = contexto.Services.GetRequiredService<NavigationManager>();
        edicion.WaitForAssertion(() => Assert.EndsWith($"/usuarios{QueryListado}", navegacion.Uri, StringComparison.Ordinal));
        Assert.Single(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Put);

        var listado = RenderizarListado();
        listado.WaitForAssertion(() => Assert.Contains(
            "Usuario Bruno Díaz Ruiz guardado.",
            Normalizar(listado.Find("[aria-live='polite']").TextContent),
            StringComparison.Ordinal));

        // Se consume una sola vez: otro listado ya no lo repite.
        var otro = RenderizarListado();
        Assert.DoesNotContain("guardado", otro.Find("[aria-live='polite']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancelar_VuelveAlListadoConSuQuery_SinGuardarNiAnunciar()
    {
        var edicion = EntrarAEditarDesdeElListado();

        Assert.Single(edicion.FindAll("button"), b => Normalizar(b.TextContent) == "Cancelar").Click();

        var navegacion = contexto.Services.GetRequiredService<NavigationManager>();
        edicion.WaitForAssertion(() => Assert.EndsWith($"/usuarios{QueryListado}", navegacion.Uri, StringComparison.Ordinal));
        Assert.DoesNotContain(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Put);
        var listado = RenderizarListado();
        Assert.DoesNotContain("guardado", listado.Find("[aria-live='polite']").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Abre el listado con <see cref="QueryListado"/>, comprueba que "Editar a Bruno Díaz" lleva esa query, sigue el
    /// enlace y renderiza la página de edición de esa ruta.
    /// </summary>
    private IRenderedComponent<EditarUsuario> EntrarAEditarDesdeElListado()
    {
        var navegacion = contexto.Services.GetRequiredService<NavigationManager>();
        navegacion.NavigateTo($"/usuarios{QueryListado}");
        var listado = contexto.Render<ListadoUsuarios>();
        listado.WaitForAssertion(() => Assert.Single(listado.FindAll("tbody tr")));

        var editar = Assert.Single(listado.FindAll("a"), a => a.GetAttribute("aria-label") == $"Editar a {Bruno.NombreCompleto}");
        var href = editar.GetAttribute("href")!;
        Assert.Equal($"usuarios/{Bruno.Id}/editar{QueryListado}", href);

        navegacion.NavigateTo(href);
        Assert.EndsWith($"/usuarios/{Bruno.Id}/editar{QueryListado}", navegacion.Uri, StringComparison.Ordinal);
        var edicion = contexto.Render<EditarUsuario>(p => p.Add(e => e.Id, Bruno.Id));
        edicion.WaitForAssertion(() => Assert.Equal(
            Bruno.NombreCompleto,
            edicion.Find("[id='" + Etiqueta(edicion, "Nombre completo").GetAttribute("for") + "']").GetAttribute("value")));
        return edicion;
    }

    private IRenderedComponent<ListadoUsuarios> RenderizarListado()
    {
        var listado = contexto.Render<ListadoUsuarios>();
        listado.WaitForAssertion(() => Assert.Single(listado.FindAll("tbody tr")));
        return listado;
    }

    private static IElement Etiqueta(IRenderedComponent<EditarUsuario> corte, string campo) =>
        Assert.Single(corte.FindAll("label"), l => Normalizar(l.TextContent).StartsWith(campo, StringComparison.Ordinal));

    private static string Normalizar(string texto) => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
