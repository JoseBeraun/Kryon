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
/// Tabla del listado (<c>contracts/ui-usuarios.md</c> → <i>Listado</i>; US1-2, US1-3, US1-5, FR-010, FR-012, FR-020,
/// FR-048, EC-9, EC-10). Renderiza la página real <c>Kryon.Web.Usuarios.ListadoUsuarios</c> (T051) con el
/// <see cref="ClienteUsuarios"/> real sobre el HTTP falso de <see cref="ContextoPruebaWeb"/>.
/// </summary>
/// <remarks>
/// Test-first: la página se localiza por nombre en tiempo de ejecución, así que el proyecto compila sin ella. Mientras
/// no exista, cada prueba falla con "ListadoUsuarios aún no está implementado (T051)"; cuando exista, se renderiza el
/// componente real sin envoltorio. Las acciones visibles salen solo de las banderas de <c>acciones</c>: los datos
/// las combinan a propósito sin relación con el estado o el rol, porque la interfaz no decide permisos.
/// </remarks>
public sealed class ListadoUsuariosTablaTests : IAsyncDisposable
{
    private const string NombreComponente = "Kryon.Web.Usuarios.ListadoUsuarios";

    // Máximos provisionales de Usuarios:Validacion en src/Kryon.Api/appsettings.json (Kryon.Web no los conoce).
    private const int LongitudMaximaNombre = 200;
    private const int LongitudMaximaIdentificador = 256;

    private static readonly string[] Columnas = ["Nombre", "Identificador de acceso", "Rol", "Estado", "Acciones"];

    private static readonly RolResumen RolAdministracion =
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Nombre = "Administración" };

    private static readonly RolResumen RolVentas =
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" };

    // Cuenta propia, activa.
    private static readonly UsuarioResumen Ana =
        Usuario("20000000-0000-0000-0000-000000000001", "Ana Pérez", "ana.perez", RolAdministracion, "activo", propia: true, Acciones(verDetalle: true, editar: true));

    // Activo, con todas las acciones de la tabla salvo activar.
    private static readonly UsuarioResumen Bruno =
        Usuario("20000000-0000-0000-0000-000000000002", "Bruno Díaz", "bruno.diaz", RolVentas, "activo", propia: false, Acciones(verDetalle: true, editar: true, desactivar: true));

    // Inactivo: puede activarse, pero la API no permite editarlo.
    private static readonly UsuarioResumen Carmen =
        Usuario("20000000-0000-0000-0000-000000000003", "Carmen Ruiz", "carmen.ruiz", RolVentas, "inactivo", propia: false, Acciones(verDetalle: true, activar: true));

    // Sin rol reconocido (EC-10) y sin ninguna acción.
    private static readonly UsuarioResumen Diego =
        Usuario("20000000-0000-0000-0000-000000000004", "Diego Sol", "diego.sol", rol: null, "activo", propia: false, Acciones());

    private readonly ContextoPruebaWeb contexto = new();

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    [Fact]
    public void Tabla_TieneCaptionYLasColumnasDelContrato()
    {
        var corte = RenderizarListado(Pagina(puedeRegistrar: false, Ana, Bruno));

        var tabla = corte.Find("table");
        Assert.False(string.IsNullOrWhiteSpace(tabla.QuerySelector("caption")?.TextContent));
        Assert.Equal(Columnas, tabla.QuerySelectorAll("thead th").Select(Texto));
    }

    [Fact]
    public void CargaLaPaginaConClienteUsuarios()
    {
        RenderizarListado(Pagina(puedeRegistrar: false, Ana));

        Assert.Contains(
            contexto.Http.Solicitudes,
            s => s.Solicitud.Method == HttpMethod.Get && s.Solicitud.RequestUri!.AbsolutePath == "/api/usuarios");
    }

    [Fact]
    public void CadaFila_MuestraNombreIdentificadorYRol()
    {
        var corte = RenderizarListado(Pagina(puedeRegistrar: false, Ana, Bruno, Carmen));

        foreach (var usuario in new[] { Ana, Bruno, Carmen })
        {
            var fila = Fila(corte, usuario);
            Assert.Contains(usuario.NombreCompleto, Celda(corte, fila, "Nombre").TextContent, StringComparison.Ordinal);
            Assert.Equal(usuario.IdentificadorAcceso, Texto(Celda(corte, fila, "Identificador de acceso")));
            Assert.Equal(usuario.Rol!.Nombre, Texto(Celda(corte, fila, "Rol")));
        }
    }

    [Fact]
    public void Estado_SeMuestraComoTexto()
    {
        var corte = RenderizarListado(Pagina(puedeRegistrar: false, Bruno, Carmen));

        Assert.Equal("Activo", Texto(Celda(corte, Fila(corte, Bruno), "Estado")));
        Assert.Equal("Inactivo", Texto(Celda(corte, Fila(corte, Carmen), "Estado")));
    }

    [Fact]
    public void CuentaPropia_MuestraTuJuntoAlNombre_YSoloEnSuFila()
    {
        var corte = RenderizarListado(Pagina(puedeRegistrar: false, Ana, Bruno, Carmen));

        Assert.Contains("Tú", Celda(corte, Fila(corte, Ana), "Nombre").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Tú", Fila(corte, Bruno).TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Tú", Fila(corte, Carmen).TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void RolNulo_SeMuestraComoSinRolAsignado()
    {
        var corte = RenderizarListado(Pagina(puedeRegistrar: false, Diego, Bruno));

        Assert.Equal("Sin rol asignado", Texto(Celda(corte, Fila(corte, Diego), "Rol")));
        Assert.Equal(RolVentas.Nombre, Texto(Celda(corte, Fila(corte, Bruno), "Rol")));
    }

    /// <summary>Cada acción aparece en la fila de su usuario solo si su bandera es verdadera (FR-007, FR-048).</summary>
    [Theory]
    [InlineData("Ana", "Ver detalle de {0}", true)]
    [InlineData("Ana", "Editar a {0}", true)]
    [InlineData("Ana", "Activar a {0}", false)]
    [InlineData("Ana", "Desactivar a {0}", false)]
    [InlineData("Bruno", "Ver detalle de {0}", true)]
    [InlineData("Bruno", "Editar a {0}", true)]
    [InlineData("Bruno", "Activar a {0}", false)]
    [InlineData("Bruno", "Desactivar a {0}", true)]
    [InlineData("Carmen", "Ver detalle de {0}", true)]
    [InlineData("Carmen", "Editar a {0}", false)]
    [InlineData("Carmen", "Activar a {0}", true)]
    [InlineData("Carmen", "Desactivar a {0}", false)]
    [InlineData("Diego", "Ver detalle de {0}", false)]
    [InlineData("Diego", "Editar a {0}", false)]
    [InlineData("Diego", "Activar a {0}", false)]
    [InlineData("Diego", "Desactivar a {0}", false)]
    public void AccionPorFila_SoloSiSuBanderaEsVerdadera(string clave, string plantilla, bool visible)
    {
        var usuario = clave switch
        {
            "Ana" => Ana,
            "Bruno" => Bruno,
            "Carmen" => Carmen,
            _ => Diego,
        };
        var corte = RenderizarListado(Pagina(puedeRegistrar: false, Ana, Bruno, Carmen, Diego));
        var nombreAccesible = string.Format(System.Globalization.CultureInfo.InvariantCulture, plantilla, usuario.NombreCompleto);

        var enLaFila = Controles(Fila(corte, usuario)).Count(c => NombreAccesible(c) == nombreAccesible);
        var enLaTabla = Controles(corte.Find("table")).Count(c => NombreAccesible(c) == nombreAccesible);

        Assert.Equal(visible ? 1 : 0, enLaFila);
        Assert.Equal(enLaFila, enLaTabla);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NuevoUsuario_SoloSiPuedeRegistrar(bool puedeRegistrar)
    {
        var corte = RenderizarListado(Pagina(puedeRegistrar, Ana, Bruno));

        var controles = Controles(corte.Nodes.OfType<IElement>()).Count(c => NombreAccesible(c) == "Nuevo usuario");
        Assert.Equal(puedeRegistrar ? 1 : 0, controles);
    }

    [Fact]
    public void TextoExtenso_LaFilaConservaSusCeldas_YElTextoCompleto()
    {
        var largo = Usuario(
            "20000000-0000-0000-0000-000000000005",
            "Nombre" + new string('n', LongitudMaximaNombre - "Nombre".Length - 1),
            "identificador" + new string('i', LongitudMaximaIdentificador - "identificador".Length - 1),
            RolVentas,
            "activo",
            propia: false,
            Acciones(verDetalle: true));
        var corte = RenderizarListado(Pagina(puedeRegistrar: false, largo, Bruno));

        var fila = Fila(corte, largo);
        Assert.Equal(Columnas.Length, fila.QuerySelectorAll("td, th").Length);
        Assert.Equal(Fila(corte, Bruno).QuerySelectorAll("td, th").Length, fila.QuerySelectorAll("td, th").Length);
        Assert.Contains(largo.NombreCompleto, Celda(corte, fila, "Nombre").TextContent, StringComparison.Ordinal);
        Assert.Equal(largo.IdentificadorAcceso, Texto(Celda(corte, fila, "Identificador de acceso")));
        Assert.Equal(RolVentas.Nombre, Texto(Celda(corte, fila, "Rol")));
        Assert.Equal("Activo", Texto(Celda(corte, fila, "Estado")));

        // Nada de la fila queda oculto a los lectores de pantalla.
        Assert.Empty(fila.QuerySelectorAll("[hidden], [aria-hidden='true']"));
        Assert.Null(fila.GetAttribute("hidden"));
    }

    /// <summary>
    /// Renderiza la página real de T051 con el HTTP falso preparado. <c>GET /api/usuarios</c> devuelve
    /// <paramref name="pagina"/>; <c>GET /api/usuarios/roles-asignables</c> (filtros), una lista vacía.
    /// </summary>
    private IRenderedComponent<IComponent> RenderizarListado(PaginaUsuarios pagina)
    {
        var tipo = typeof(ClienteUsuarios).Assembly.GetType(NombreComponente);
        if (tipo is null)
        {
            Assert.Fail("ListadoUsuarios aún no está implementado (T051).");
        }

        Assert.True(typeof(IComponent).IsAssignableFrom(tipo), $"{NombreComponente} no es un componente de Blazor.");

        contexto.Http.Responder = (solicitud, _) => solicitud.RequestUri!.AbsolutePath switch
        {
            "/api/usuarios" => HttpFalso.Json(HttpStatusCode.OK, pagina),
            "/api/usuarios/roles-asignables" => HttpFalso.Json(HttpStatusCode.OK, Array.Empty<RolResumen>()),
            var ruta => throw new InvalidOperationException($"Solicitud no prevista en T046: {solicitud.Method} {ruta}"),
        };
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo("/usuarios");

        var corte = (IRenderedComponent<IComponent>)contexto.Render(builder =>
        {
            builder.OpenComponent(0, tipo);
            builder.CloseComponent();
        });
        corte.WaitForAssertion(() => Assert.NotEmpty(corte.FindAll("tbody tr")));
        return corte;
    }

    private static IElement Fila(IRenderedComponent<IComponent> corte, UsuarioResumen usuario) =>
        Assert.Single(corte.FindAll("tbody tr"), f => f.TextContent.Contains(usuario.IdentificadorAcceso, StringComparison.Ordinal));

    /// <summary>Celda de la columna <paramref name="columna"/>, localizada por la posición de su encabezado.</summary>
    private static IElement Celda(IRenderedComponent<IComponent> corte, IElement fila, string columna)
    {
        var indice = Array.IndexOf(corte.FindAll("thead th").Select(Texto).ToArray(), columna);
        Assert.True(indice >= 0, $"No hay columna \"{columna}\".");
        return fila.QuerySelectorAll("td, th")[indice];
    }

    private static IEnumerable<IElement> Controles(IElement raiz) => raiz.QuerySelectorAll("button, a");

    private static IEnumerable<IElement> Controles(IEnumerable<IElement> raices) =>
        raices.SelectMany(r => (r.Matches("button, a") ? [r] : Enumerable.Empty<IElement>()).Concat(r.QuerySelectorAll("button, a")));

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

        return control.GetAttribute("aria-label") is { Length: > 0 } propia ? Normalizar(propia) : Texto(control);
    }

    private static string Normalizar(string texto) => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Texto(IElement elemento) => Normalizar(elemento.TextContent);

    private static PaginaUsuarios Pagina(bool puedeRegistrar, params UsuarioResumen[] usuarios) => new()
    {
        Elementos = usuarios,
        Total = usuarios.Length,
        Pagina = 1,
        TamanoPagina = 25,
        PuedeRegistrar = puedeRegistrar,
    };

    private static UsuarioResumen Usuario(
        string id,
        string nombre,
        string identificador,
        RolResumen? rol,
        string estado,
        bool propia,
        AccionesPermitidas acciones) => new()
        {
            Id = Guid.Parse(id),
            NombreCompleto = nombre,
            IdentificadorAcceso = identificador,
            Rol = rol,
            Estado = estado,
            EsCuentaPropia = propia,
            Version = $"\"{id}\"",
            Acciones = acciones,
        };

    private static AccionesPermitidas Acciones(bool verDetalle = false, bool editar = false, bool activar = false, bool desactivar = false) => new()
    {
        VerDetalle = verDetalle,
        Editar = editar,
        CambiarRol = false,
        EditarIdentificador = false,
        Activar = activar,
        Desactivar = desactivar,
    };
}
