using System.Net;

using AngleSharp.Dom;

using Bunit;

using Kryon.Contracts.Usuarios;
using Kryon.Web.Compartido;
using Kryon.Web.Tests.Infraestructura;
using Kryon.Web.Usuarios;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using DetalleUsuarioPagina = Kryon.Web.Usuarios.Detalle.DetalleUsuario;

namespace Kryon.Web.Tests.Usuarios;

/// <summary>
/// Nombres accesibles y anuncios (T105; US6-2, US6-4, FR-048, FR-050; contracts/ui-usuarios.md): todo <c>button</c>,
/// <c>a</c>, <c>input</c> y <c>select</c> de la gestión de usuarios tiene nombre accesible; las acciones de cada fila (y
/// del detalle) llevan el nombre del usuario; los éxitos se anuncian en <c>aria-live="polite"</c> y los errores en
/// <c>role="alert"</c>, siempre con su texto dentro de la región.
/// </summary>
/// <remarks>
/// El nombre accesible se calcula con <see cref="NombreAccesible"/>, que cubre solo los patrones que usa Kryon, en el
/// orden de precedencia de WAI-ARIA: <c>aria-labelledby</c>, <c>aria-label</c>, <c>label[for]</c> asociado (controles de
/// formulario) y el texto visible o el <c>value</c> (botones y enlaces). No es una implementación completa del cálculo
/// de nombres accesibles. Las pruebas protegen la semántica (roles, etiquetas, relaciones id/for y texto), no la
/// estructura exacta del HTML.
/// </remarks>
public sealed class NombresAccesiblesTests : IAsyncDisposable
{
    private static readonly RolResumen Ventas = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" };
    private static readonly RolResumen Compras = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nombre = "Compras" };

    /// <summary>Activa, con ver detalle, editar y desactivar.</summary>
    private static readonly UsuarioResumen Carla = new()
    {
        Id = Guid.Parse("20000000-0000-0000-0000-0000000000c1"),
        NombreCompleto = "Carla Ramos",
        IdentificadorAcceso = "carla.ramos",
        Rol = Ventas,
        Estado = "activo",
        EsCuentaPropia = false,
        Version = "\"AAAAAAAAC9E=\"",
        Acciones = new() { VerDetalle = true, Editar = true, CambiarRol = true, EditarIdentificador = false, Activar = false, Desactivar = true },
    };

    /// <summary>Inactivo, con ver detalle y activar.</summary>
    private static readonly UsuarioResumen Dario = new()
    {
        Id = Guid.Parse("20000000-0000-0000-0000-0000000000d1"),
        NombreCompleto = "Darío Núñez",
        IdentificadorAcceso = "dario.nunez",
        Rol = Compras,
        Estado = "inactivo",
        EsCuentaPropia = false,
        Version = "\"AAAAAAAAD9E=\"",
        Acciones = new() { VerDetalle = true, Editar = false, CambiarRol = false, EditarIdentificador = false, Activar = true, Desactivar = false },
    };

    private readonly ContextoPruebaWeb contexto = new();

    public NombresAccesiblesTests()
    {
        contexto.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    // ---- Filtros y paginación ----

    [Fact]
    public void Filtros_BusquedaRolEstadoYBotones_TienenNombreAccesible()
    {
        Responder(roles: [Ventas, Compras]);

        var corte = contexto.Render<FiltrosUsuarios>(p => p
            .Add(f => f.Busqueda, "car")
            .Add(f => f.RolId, Ventas.Id.ToString())
            .Add(f => f.Estado, "activo"));

        Assert.Equal("Buscar por nombre o identificador", NombreAccesible(corte, corte.Find("input[type='search']")));
        Assert.Equal("Rol", NombreAccesible(corte, Control(corte, "select", "Rol")));
        Assert.Equal("Estado", NombreAccesible(corte, Control(corte, "select", "Estado")));
        Assert.Single(Interactivos(corte), e => NombreAccesible(corte, e) == "Buscar");
        Assert.Single(Interactivos(corte), e => NombreAccesible(corte, e) == "Limpiar búsqueda y filtros");
        TodosConNombre(corte);
    }

    [Fact]
    public void Paginacion_LosBotonesTienenNombreAccesible_AunqueEstenDeshabilitados()
    {
        var corte = contexto.Render<PaginacionUsuarios>(p => p.Add(x => x.Pagina, 1).Add(x => x.TamanoPagina, 25).Add(x => x.Total, 1));

        var anterior = Assert.Single(corte.FindAll("button"), b => NombreAccesible(corte, b) == "Página anterior");
        var siguiente = Assert.Single(corte.FindAll("button"), b => NombreAccesible(corte, b) == "Página siguiente");

        // Una sola página: ambos deshabilitados, pero conservan su propio nombre (el del nav no lo sustituye).
        Assert.True(anterior.HasAttribute("disabled"));
        Assert.True(siguiente.HasAttribute("disabled"));
        TodosConNombre(corte);
    }

    // ---- Listado ----

    [Fact]
    public void Listado_TodosLosControles_TienenNombreAccesible()
    {
        Responder(pagina: Pagina(puedeRegistrar: true, Carla, Dario), roles: [Ventas, Compras]);
        var corte = RenderizarListado("/usuarios?busqueda=a");

        TodosConNombre(corte);
        Assert.Single(Interactivos(corte), e => NombreAccesible(corte, e) == "Nuevo usuario");
    }

    /// <summary>FR-048: cada acción de una fila dice sobre quién actúa; solo aparecen las que anuncia la API.</summary>
    [Fact]
    public void Listado_LasAccionesDeCadaFila_IncluyenElNombreDelUsuario()
    {
        Responder(pagina: Pagina(puedeRegistrar: false, Carla, Dario), roles: []);
        var corte = RenderizarListado("/usuarios");

        Assert.Equal(
            ["Ver detalle de Carla Ramos", "Editar a Carla Ramos", "Desactivar a Carla Ramos"],
            AccionesDeFila(corte, Carla));
        Assert.Equal(["Ver detalle de Darío Núñez", "Activar a Darío Núñez"], AccionesDeFila(corte, Dario));
    }

    // ---- Detalle y diálogo ----

    [Fact]
    public void Detalle_LasAccionesIncluyenElNombreDelUsuario_YTodosLosControlesTienenNombre()
    {
        contexto.Http.Responder = (solicitud, _) => solicitud.RequestUri!.AbsolutePath == $"/api/usuarios/{Carla.Id}"
            ? HttpFalso.Json(HttpStatusCode.OK, Detalle(Carla), Carla.Version)
            : throw new InvalidOperationException($"Solicitud no prevista en T105: {solicitud.Method} {solicitud.RequestUri}");
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo($"/usuarios/{Carla.Id}");

        var corte = contexto.Render<DetalleUsuarioPagina>(p => p.Add(d => d.Id, Carla.Id));
        corte.WaitForAssertion(() => Assert.Contains(Carla.IdentificadorAcceso, corte.Markup, StringComparison.Ordinal));

        TodosConNombre(corte);
        var acciones = Interactivos(corte).Select(e => NombreAccesible(corte, e)).Where(n => n != "Volver al listado").ToArray();
        Assert.Equal(["Editar a Carla Ramos", "Desactivar a Carla Ramos"], acciones);
        Assert.Single(Interactivos(corte), e => NombreAccesible(corte, e) == "Volver al listado");
    }

    [Fact]
    public void Dialogo_BotonesConNombre_YTituloYDescripcionReferenciadosConTexto()
    {
        var corte = contexto.Render<ConfirmarDesactivacion>(p => p.Add(d => d.Usuario, Carla));

        var dialogo = corte.Find("dialog");
        Assert.Equal("Desactivar a Carla Ramos", TextoReferenciado(corte, dialogo, "aria-labelledby"));
        Assert.Equal("Carla Ramos dejará de tener acceso a Kryon. Puedes volver a activarlo más adelante.", TextoReferenciado(corte, dialogo, "aria-describedby"));
        Assert.Equal(["Desactivar", "Cancelar"], corte.FindAll("button").Select(e => NombreAccesible(corte, e)));
    }

    // ---- Formulario ----

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Formulario_CamposConEtiquetaAsociada_YBotonesConNombre(bool esCuentaPropia, bool identificadorEditable)
    {
        var corte = contexto.Render<FormularioUsuario>(p => p
            .Add(f => f.Valores, new FormularioUsuario.ValoresFormulario { NombreCompleto = "Carla Ramos", IdentificadorAcceso = "carla.ramos", RolId = Ventas.Id.ToString() })
            .Add(f => f.Roles, [Ventas, Compras])
            .Add(f => f.RolActual, Ventas)
            .Add(f => f.EsCuentaPropia, esCuentaPropia)
            .Add(f => f.IdentificadorEditable, identificadorEditable));

        // El nombre sale de la etiqueta asociada por for/id, también en los campos de solo lectura.
        Assert.StartsWith("Nombre completo", NombreAccesible(corte, corte.Find("input[id='" + IdPorEtiqueta(corte, "Nombre completo") + "']")), StringComparison.Ordinal);
        Assert.StartsWith("Identificador de acceso", NombreAccesible(corte, corte.Find("[id='" + IdPorEtiqueta(corte, "Identificador de acceso") + "']")), StringComparison.Ordinal);

        var rol = corte.Find("[id='" + IdPorEtiqueta(corte, "Rol") + "']");
        Assert.StartsWith("Rol", NombreAccesible(corte, rol), StringComparison.Ordinal);
        Assert.Equal(esCuentaPropia ? "input" : "select", rol.LocalName);
        if (esCuentaPropia)
        {
            Assert.True(rol.HasAttribute("readonly"));
        }

        Assert.Single(corte.FindAll("button"), b => NombreAccesible(corte, b) == "Guardar");
        Assert.Single(corte.FindAll("button"), b => NombreAccesible(corte, b) == "Cancelar");
        TodosConNombre(corte);
    }

    // ---- Anuncios ----

    [Fact]
    public void Mensajes_ElExitoVaConSuTextoDentroDeAriaLivePolite()
    {
        var corte = contexto.Render<Mensajes>(p => p.Add(m => m.Exito, "Usuario Carla Ramos guardado."));

        var region = Assert.Single(corte.FindAll("[aria-live='polite']"));
        Assert.Equal("Usuario Carla Ramos guardado.", Normalizar(region.TextContent));
        Assert.Equal(string.Empty, Normalizar(corte.Find("[role='alert']").TextContent));
    }

    [Fact]
    public void Mensajes_ElErrorVaConSuTextoDentroDeRoleAlert()
    {
        var corte = contexto.Render<Mensajes>(p => p.Add(m => m.Error, "No tienes permiso para realizar esta acción."));

        var region = Assert.Single(corte.FindAll("[role='alert']"));
        Assert.Equal("No tienes permiso para realizar esta acción.", Normalizar(region.TextContent));
        Assert.Equal(string.Empty, Normalizar(corte.Find("[aria-live='polite']").TextContent));
    }

    /// <summary>Integración: una activación correcta del listado se anuncia en la región <c>aria-live="polite"</c>.</summary>
    [Fact]
    public void Listado_UnExitoDeLaApi_SeAnunciaEnAriaLivePolite()
    {
        var activado = Dario with { Estado = "activo", Acciones = Dario.Acciones with { Activar = false, Desactivar = true } };
        Responder(pagina: Pagina(puedeRegistrar: false, Dario), roles: [], activar: () => HttpFalso.Json(HttpStatusCode.OK, Detalle(activado), activado.Version));
        var corte = RenderizarListado("/usuarios");

        Assert.Single(Interactivos(corte), e => NombreAccesible(corte, e) == "Activar a Darío Núñez").Click();

        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[aria-live='polite']"),
            r => Normalizar(r.TextContent) == "Darío Núñez fue activado."));
    }

    /// <summary>Integración: un rechazo de la API se anuncia en <c>role="alert"</c> con el texto de su código.</summary>
    [Fact]
    public void Listado_UnErrorDeLaApi_SeAnunciaEnRoleAlert()
    {
        Responder(pagina: Pagina(puedeRegistrar: false, Dario), roles: [], activar: () => HttpFalso.ConProblema(new Problema
        {
            Type = "https://kryon.app/errores/usuario-modificado",
            Title = "Otra persona modificó este usuario",
            Status = 412,
            Codigo = CodigosError.UsuarioModificado,
            Actual = Detalle(Dario),
        }));
        var corte = RenderizarListado("/usuarios");

        Assert.Single(Interactivos(corte), e => NombreAccesible(corte, e) == "Activar a Darío Núñez").Click();

        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[role='alert']"),
            r => Normalizar(r.TextContent) == TextosErrores.ParaCodigo(CodigosError.UsuarioModificado)));
    }

    // ---- Nombre accesible ----

    /// <summary>
    /// Nombre accesible de un control para los patrones que usa Kryon: <c>aria-labelledby</c> (textos referenciados, en
    /// orden), <c>aria-label</c>, la etiqueta <c>label[for]</c> de un <c>input</c> o <c>select</c>, y el texto visible (o el
    /// <c>value</c> de un <c>input</c> de tipo botón) de botones y enlaces. Devuelve una cadena vacía si no tiene nombre.
    /// Las referencias por id y por <c>for</c> se resuelven entre <paramref name="elementos"/>, todos los elementos
    /// renderizados (bUnit los mantiene en fragmentos sin una raíz común).
    /// </summary>
    private static string NombreAccesible(IElement control, IReadOnlyCollection<IElement> elementos)
    {
        if (control.GetAttribute("aria-labelledby") is { Length: > 0 } referencias)
        {
            var textos = referencias.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => elementos.FirstOrDefault(e => e.Id == id))
                .OfType<IElement>()
                .Select(e => Normalizar(e.TextContent));
            var nombre = Normalizar(string.Join(' ', textos));
            if (nombre.Length > 0)
            {
                return nombre;
            }
        }

        if (control.GetAttribute("aria-label") is { } etiqueta && Normalizar(etiqueta).Length > 0)
        {
            return Normalizar(etiqueta);
        }

        if (control.LocalName is "input" or "select" or "textarea")
        {
            if (control.LocalName == "input" && control.GetAttribute("type") is "submit" or "button" or "reset")
            {
                return Normalizar(control.GetAttribute("value") ?? string.Empty);
            }

            var porFor = string.IsNullOrEmpty(control.Id)
                ? null
                : elementos.FirstOrDefault(e => e.LocalName == "label" && e.GetAttribute("for") == control.Id);
            var envolvente = control.Closest("label");
            return Normalizar((porFor ?? envolvente)?.TextContent ?? string.Empty);
        }

        return Normalizar(control.TextContent);
    }

    private static string NombreAccesible<T>(IRenderedComponent<T> corte, IElement control)
        where T : IComponent =>
        NombreAccesible(control, corte.FindAll("*").ToArray());

    /// <summary>Todo <c>button</c>, <c>a</c>, <c>input</c> y <c>select</c> visible del componente tiene nombre accesible.</summary>
    private static void TodosConNombre<T>(IRenderedComponent<T> corte)
        where T : IComponent
    {
        var controles = Interactivos(corte).ToArray();
        Assert.NotEmpty(controles);
        Assert.All(controles, c => Assert.True(
            NombreAccesible(corte, c).Length > 0,
            $"Sin nombre accesible: <{c.LocalName}> {c.OuterHtml}"));
    }

    private static IEnumerable<IElement> Interactivos<T>(IRenderedComponent<T> corte)
        where T : IComponent =>
        corte.FindAll("button, a, input, select")
            .Where(e => !e.HasAttribute("hidden") && e.GetAttribute("type") != "hidden");

    private static string[] AccionesDeFila(IRenderedComponent<ListadoUsuarios> corte, UsuarioResumen usuario)
    {
        var fila = Assert.Single(corte.FindAll("tbody tr"), f => f.TextContent.Contains(usuario.IdentificadorAcceso, StringComparison.Ordinal));
        return [.. fila.QuerySelectorAll("button, a").Select(e => NombreAccesible(corte, e))];
    }

    private static IElement Control<T>(IRenderedComponent<T> corte, string tipo, string etiqueta)
        where T : IComponent =>
        corte.Find($"{tipo}[id='{IdPorEtiqueta(corte, etiqueta)}']");

    private static string IdPorEtiqueta<T>(IRenderedComponent<T> corte, string etiqueta)
        where T : IComponent =>
        Assert.Single(corte.FindAll("label"), l => Normalizar(l.TextContent).StartsWith(etiqueta, StringComparison.Ordinal)).GetAttribute("for")
        ?? throw new Xunit.Sdk.XunitException($"La etiqueta \"{etiqueta}\" no está asociada a ningún control.");

    private static string TextoReferenciado<T>(IRenderedComponent<T> corte, IElement elemento, string atributo)
        where T : IComponent
    {
        var ids = (elemento.GetAttribute(atributo) ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(ids);
        var referenciados = ids.Select(id => corte.FindAll("*").FirstOrDefault(e => e.Id == id)).ToArray();
        Assert.All(referenciados, r => Assert.NotNull(r));
        return Normalizar(string.Join(' ', referenciados.Select(r => r!.TextContent)));
    }

    // ---- Preparación ----

    private IRenderedComponent<ListadoUsuarios> RenderizarListado(string uri)
    {
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
        var corte = contexto.Render<ListadoUsuarios>();
        corte.WaitForAssertion(() => Assert.NotEmpty(corte.FindAll("tbody tr")));
        return corte;
    }

    private void Responder(PaginaUsuarios? pagina = null, RolResumen[]? roles = null, Func<HttpResponseMessage>? activar = null) =>
        contexto.Http.Responder = (solicitud, _) => (solicitud.Method.Method, solicitud.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/api/usuarios") when pagina is not null => HttpFalso.Json(HttpStatusCode.OK, pagina),
            ("GET", "/api/usuarios/roles-asignables") => HttpFalso.Json(HttpStatusCode.OK, roles ?? []),
            ("POST", var ruta) when activar is not null && ruta.EndsWith("/activacion", StringComparison.Ordinal) => activar(),
            var (metodo, ruta) => throw new InvalidOperationException($"Solicitud no prevista en T105: {metodo} {ruta}"),
        };

    private static PaginaUsuarios Pagina(bool puedeRegistrar, params UsuarioResumen[] usuarios) => new()
    {
        Elementos = usuarios,
        Total = usuarios.Length,
        Pagina = 1,
        TamanoPagina = 25,
        PuedeRegistrar = puedeRegistrar,
    };

    private static DetalleUsuario Detalle(UsuarioResumen usuario) => new()
    {
        Id = usuario.Id,
        NombreCompleto = usuario.NombreCompleto,
        IdentificadorAcceso = usuario.IdentificadorAcceso,
        Rol = usuario.Rol,
        Estado = usuario.Estado,
        EsCuentaPropia = usuario.EsCuentaPropia,
        Version = usuario.Version,
        Acciones = usuario.Acciones,
    };

    private static string Normalizar(string texto) => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
