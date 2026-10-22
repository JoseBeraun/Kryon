using System.Net;

using AngleSharp.Dom;

using Bunit;

using Kryon.Contracts.Usuarios;
using Kryon.Web.Tests.Infraestructura;
using Kryon.Web.Usuarios;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using DetalleUsuarioPagina = Kryon.Web.Usuarios.Detalle.DetalleUsuario;

namespace Kryon.Web.Tests.Usuarios;

/// <summary>
/// Acciones visibles según los permisos (US5-1, US5-3, FR-007, EC-7): el listado y el detalle muestran una acción solo
/// si su bandera de <c>acciones</c> (o <c>puedeRegistrar</c>) es verdadera, sin recalcular nada en la interfaz; y ante
/// un 403 <c>sin-permiso</c> al ejecutarla, muestran "No tienes permiso para realizar esta acción." y vuelven a pedir
/// los datos para usar las acciones vigentes que devuelve la API.
/// </summary>
/// <remarks>
/// Las combinaciones de banderas son a propósito independientes del estado y de la cuenta propia (por ejemplo,
/// <c>activar</c> en un usuario Activo): la interfaz obedece la bandera tal cual llega y no aplica reglas propias.
/// </remarks>
public sealed class AccionesVisiblesTests : IAsyncDisposable
{
    private const string QueryListado = "?busqueda=car&estado=inactivo&pagina=1";
    private const string MensajeSinPermiso = "No tienes permiso para realizar esta acción.";

    private static readonly UsuarioResumen Carmen = new()
    {
        Id = Guid.Parse("20000000-0000-0000-0000-000000000003"),
        NombreCompleto = "Carmen Ruiz",
        IdentificadorAcceso = "carmen.ruiz",
        Rol = new RolResumen { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" },
        Estado = "inactivo",
        EsCuentaPropia = false,
        Version = "\"AAAAAAAAB9I=\"",
        Acciones = SinAcciones(),
    };

    private readonly ContextoPruebaWeb contexto = new();

    public AccionesVisiblesTests()
    {
        contexto.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    /// <summary>Acción → nombre accesible de su control (contracts/ui-usuarios.md → Listado).</summary>
    public static TheoryData<string> Acciones => ["verDetalle", "editar", "activar", "desactivar"];

    // ---- Listado ----

    [Fact]
    public void Listado_SinBanderas_NoMuestraNingunaAccion_NiNuevoUsuario()
    {
        var corte = RenderizarListado(Pagina(Carmen, puedeRegistrar: false));

        Assert.Empty(Controles(corte.Find("tbody")));
        Assert.DoesNotContain(corte.FindAll("button"), b => Texto(b) == "Nuevo usuario");
    }

    [Theory]
    [MemberData(nameof(Acciones))]
    public void Listado_SoloMuestraLaAccionConBanderaVerdadera(string accion)
    {
        var corte = RenderizarListado(Pagina(Carmen with { Acciones = Con(accion) }, puedeRegistrar: false));

        var controles = Controles(corte.Find("tbody")).Select(NombreAccesible).ToArray();
        Assert.Equal([NombreAccesible(accion, Carmen.NombreCompleto)], controles);
    }

    [Fact]
    public void Listado_PuedeRegistrar_MuestraNuevoUsuario()
    {
        var corte = RenderizarListado(Pagina(Carmen, puedeRegistrar: true));

        Assert.Single(corte.FindAll("button"), b => Texto(b) == "Nuevo usuario");
    }

    /// <summary>Tampoco en la cuenta propia: si la API anuncia una acción, se muestra; la regla la aplica la API.</summary>
    [Fact]
    public void Listado_CuentaPropia_ObedeceLasBanderasSinRecalcularlas()
    {
        var propia = Carmen with { EsCuentaPropia = true, Estado = "activo", Acciones = Con("desactivar") };

        var corte = RenderizarListado(Pagina(propia, puedeRegistrar: false));

        Assert.Equal([$"Desactivar a {Carmen.NombreCompleto}"], Controles(corte.Find("tbody")).Select(NombreAccesible));
    }

    // ---- Detalle ----

    [Theory]
    [InlineData("editar")]
    [InlineData("activar")]
    [InlineData("desactivar")]
    public void Detalle_SoloMuestraLaAccionConBanderaVerdadera(string accion)
    {
        var corte = RenderizarDetalle(Detalle(Carmen with { Acciones = Con(accion) }));

        var acciones = Controles(corte).Select(NombreAccesible).Where(n => n.EndsWith(Carmen.NombreCompleto, StringComparison.Ordinal)).ToArray();
        Assert.Equal([NombreAccesible(accion, Carmen.NombreCompleto)], acciones);
    }

    [Fact]
    public void Detalle_SinBanderas_NoMuestraNingunaAccion()
    {
        var corte = RenderizarDetalle(Detalle(Carmen));

        Assert.DoesNotContain(Controles(corte), c => NombreAccesible(c).EndsWith(Carmen.NombreCompleto, StringComparison.Ordinal));
    }

    // ---- 403 durante una acción (US5-3) ----

    [Fact]
    public void Listado_SinPermisoAlActivar_MuestraElMensaje_YRecargaConLasAccionesVigentes()
    {
        var antes = Carmen with { Acciones = Con("activar") };
        var despues = Carmen with { Acciones = Con("verDetalle") };
        var listados = new Queue<PaginaUsuarios>([Pagina(antes, puedeRegistrar: false), Pagina(despues, puedeRegistrar: false)]);
        var corte = RenderizarListado(() => HttpFalso.Json(HttpStatusCode.OK, listados.Dequeue()), SinPermiso);

        Pulsar(corte, $"Activar a {Carmen.NombreCompleto}");

        corte.WaitForAssertion(() => Assert.DoesNotContain(Controles(corte.Find("tbody")), c => NombreAccesible(c) == $"Activar a {Carmen.NombreCompleto}"));
        Assert.Contains(corte.FindAll("[role='alert']"), r => Texto(r).Contains(MensajeSinPermiso, StringComparison.Ordinal));
        Assert.Single(Controles(corte.Find("tbody")), c => NombreAccesible(c) == $"Ver detalle de {Carmen.NombreCompleto}");

        // Se volvió a pedir la misma página del listado, con su búsqueda, filtros y página.
        var consultas = contexto.Http.Solicitudes.Where(EsListado).Select(s => s.Solicitud.RequestUri!.Query).ToArray();
        Assert.Equal(2, consultas.Length);
        Assert.Equal(consultas[0], consultas[1]);
    }

    [Fact]
    public void Listado_SinPermisoYaTampocoParaVer_NoConservaDatosObsoletos()
    {
        var listados = new Queue<Func<HttpResponseMessage>>(
        [
            () => HttpFalso.Json(HttpStatusCode.OK, Pagina(Carmen with { Acciones = Con("activar") }, puedeRegistrar: false)),
            () => SinPermiso(),
        ]);
        var corte = RenderizarListado(() => listados.Dequeue()(), SinPermiso);

        Pulsar(corte, $"Activar a {Carmen.NombreCompleto}");

        corte.WaitForAssertion(() => Assert.Empty(corte.FindAll("table")));
        Assert.DoesNotContain(Carmen.NombreCompleto, corte.Markup, StringComparison.Ordinal);
        Assert.Contains(corte.FindAll("[role='alert']"), r => Texto(r).Contains(MensajeSinPermiso, StringComparison.Ordinal));
    }

    [Fact]
    public void Detalle_SinPermisoAlActivar_MuestraElMensaje_YRecargaElUsuario()
    {
        var detalles = new Queue<Func<HttpResponseMessage>>(
        [
            () => Json(Detalle(Carmen with { Acciones = Con("activar") })),
            () => Json(Detalle(Carmen with { Acciones = Con("editar") })),
        ]);
        var corte = RenderizarDetalle(() => detalles.Dequeue()(), SinPermiso);

        Pulsar(corte, $"Activar a {Carmen.NombreCompleto}");

        corte.WaitForAssertion(() => Assert.DoesNotContain(Controles(corte), c => NombreAccesible(c) == $"Activar a {Carmen.NombreCompleto}"));
        Assert.Contains(corte.FindAll("[role='alert']"), r => Texto(r).Contains(MensajeSinPermiso, StringComparison.Ordinal));
        Assert.Single(Controles(corte), c => NombreAccesible(c) == $"Editar a {Carmen.NombreCompleto}");
        Assert.Equal(2, contexto.Http.Solicitudes.Count(s => s.Solicitud.Method == HttpMethod.Get));
    }

    [Fact]
    public void Detalle_SinPermisoYaTampocoParaVer_NoConservaDatosObsoletos()
    {
        var detalles = new Queue<Func<HttpResponseMessage>>(
        [
            () => Json(Detalle(Carmen with { Acciones = Con("activar") })),
            () => SinPermiso(),
        ]);
        var corte = RenderizarDetalle(() => detalles.Dequeue()(), SinPermiso);

        Pulsar(corte, $"Activar a {Carmen.NombreCompleto}");

        corte.WaitForAssertion(() => Assert.DoesNotContain(Carmen.IdentificadorAcceso, corte.Markup, StringComparison.Ordinal));
        Assert.Empty(corte.FindAll("dl"));
        Assert.Contains(corte.FindAll("[role='alert']"), r => Texto(r).Contains(MensajeSinPermiso, StringComparison.Ordinal));
    }

    // ---- Preparación ----

    private IRenderedComponent<ListadoUsuarios> RenderizarListado(PaginaUsuarios pagina) =>
        RenderizarListado(() => HttpFalso.Json(HttpStatusCode.OK, pagina), () => throw new InvalidOperationException("Esta prueba no ejecuta acciones."));

    private IRenderedComponent<ListadoUsuarios> RenderizarListado(Func<HttpResponseMessage> listado, Func<HttpResponseMessage> accion)
    {
        contexto.Http.Responder = (solicitud, _) => (solicitud.Method.Method, solicitud.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/api/usuarios") => listado(),
            ("GET", "/api/usuarios/roles-asignables") => HttpFalso.Json(HttpStatusCode.OK, Array.Empty<RolResumen>()),
            ("POST", _) => accion(),
            var (metodo, ruta) => throw new InvalidOperationException($"Solicitud no prevista en T097: {metodo} {ruta}"),
        };
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo($"/usuarios{QueryListado}");

        var corte = contexto.Render<ListadoUsuarios>();
        corte.WaitForAssertion(() => Assert.Single(corte.FindAll("tbody tr")));
        return corte;
    }

    private IRenderedComponent<DetalleUsuarioPagina> RenderizarDetalle(DetalleUsuario detalle) =>
        RenderizarDetalle(() => Json(detalle), () => throw new InvalidOperationException("Esta prueba no ejecuta acciones."));

    private IRenderedComponent<DetalleUsuarioPagina> RenderizarDetalle(Func<HttpResponseMessage> detalle, Func<HttpResponseMessage> accion)
    {
        contexto.Http.Responder = (solicitud, _) => (solicitud.Method.Method, solicitud.RequestUri!.AbsolutePath) switch
        {
            ("GET", var ruta) when ruta == $"/api/usuarios/{Carmen.Id}" => detalle(),
            ("POST", _) => accion(),
            var (metodo, ruta) => throw new InvalidOperationException($"Solicitud no prevista en T097: {metodo} {ruta}"),
        };
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo($"/usuarios/{Carmen.Id}");

        var corte = contexto.Render<DetalleUsuarioPagina>(p => p.Add(d => d.Id, Carmen.Id));
        corte.WaitForAssertion(() => Assert.Contains(Carmen.IdentificadorAcceso, corte.Markup, StringComparison.Ordinal));
        return corte;
    }

    private static HttpResponseMessage SinPermiso() => HttpFalso.ConProblema(new Problema
    {
        Type = "https://kryon.app/errores/sin-permiso",
        Title = "Sin permiso para esta acción",
        Status = 403,
        Codigo = CodigosError.SinPermiso,
    });

    private static HttpResponseMessage Json(DetalleUsuario detalle) => HttpFalso.Json(HttpStatusCode.OK, detalle, detalle.Version);

    private static PaginaUsuarios Pagina(UsuarioResumen usuario, bool puedeRegistrar) => new()
    {
        Elementos = [usuario],
        Total = 1,
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

    private static AccionesPermitidas SinAcciones() => new()
    {
        VerDetalle = false,
        Editar = false,
        CambiarRol = false,
        EditarIdentificador = false,
        Activar = false,
        Desactivar = false,
    };

    private static AccionesPermitidas Con(string accion) => SinAcciones() with
    {
        VerDetalle = accion == "verDetalle",
        Editar = accion == "editar",
        Activar = accion == "activar",
        Desactivar = accion == "desactivar",
    };

    private static string NombreAccesible(string accion, string nombre) => accion switch
    {
        "verDetalle" => $"Ver detalle de {nombre}",
        "editar" => $"Editar a {nombre}",
        "activar" => $"Activar a {nombre}",
        "desactivar" => $"Desactivar a {nombre}",
        _ => throw new ArgumentOutOfRangeException(nameof(accion), accion, null),
    };

    private static bool EsListado(SolicitudRecibida s) =>
        s.Solicitud.Method == HttpMethod.Get && s.Solicitud.RequestUri!.AbsolutePath == "/api/usuarios";

    private static void Pulsar<T>(IRenderedComponent<T> corte, string nombreAccesible)
        where T : IComponent =>
        Assert.Single(Controles(corte), c => NombreAccesible(c) == nombreAccesible).Click();

    private static IEnumerable<IElement> Controles<T>(IRenderedComponent<T> corte)
        where T : IComponent =>
        corte.FindAll("button, a");

    private static IEnumerable<IElement> Controles(IElement raiz) => raiz.QuerySelectorAll("button, a");

    private static string NombreAccesible(IElement control) =>
        control.GetAttribute("aria-label") is { Length: > 0 } etiqueta ? Texto(etiqueta) : Texto(control);

    private static string Texto(IElement elemento) => Texto(elemento.TextContent);

    private static string Texto(string texto) => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
