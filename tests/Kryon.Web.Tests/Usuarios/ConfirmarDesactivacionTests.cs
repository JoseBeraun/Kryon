using System.Net;

using AngleSharp.Dom;

using Bunit;

using Kryon.Contracts.Usuarios;
using Kryon.Web.Tests.Infraestructura;
using Kryon.Web.Usuarios;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Web.Tests.Usuarios;

/// <summary>
/// Diálogo de confirmación de la desactivación y mensajes de éxito (contracts/ui-usuarios.md → <i>Diálogo de
/// confirmación de desactivación</i> y <i>Mensajes</i>; US2-3, US2-4, SC-007, FR-032, FR-049) sobre la página real del
/// listado con el <see cref="ClienteUsuarios"/> real y el HTTP falso de <see cref="ContextoPruebaWeb"/>.
/// </summary>
/// <remarks>
/// Test-first: el diálogo (T067) y su conexión con el listado (T068) aún no existen. Contrato observable que fijan estas
/// pruebas: el diálogo es un <c>&lt;dialog&gt;</c> que solo contiene su título mientras está abierto; el foco inicial
/// en "Cancelar" se da con <c>autofocus</c> o con <c>FocusAsync</c>; Escape llega como evento <c>cancel</c> del
/// diálogo o como <c>keydown</c> de Escape; el foco vuelve al botón de origen con <c>FocusAsync</c>. bUnit no tiene
/// navegador, así que el foco se comprueba por las llamadas de interoperabilidad que registra.
/// </remarks>
public sealed class ConfirmarDesactivacionTests : IAsyncDisposable
{
    private const string FuncionFoco = "Blazor._internal.domWrapper.focus";

    private static readonly UsuarioResumen Bruno = Usuario(
        "20000000-0000-0000-0000-000000000002", "Bruno Díaz", "bruno.diaz", "activo", "\"AAAAAAAAB9E=\"", desactivar: true);

    private static readonly UsuarioResumen Carmen = Usuario(
        "20000000-0000-0000-0000-000000000003", "Carmen Ruiz", "carmen.ruiz", "inactivo", "\"AAAAAAAAB9I=\"", activar: true);

    private readonly ContextoPruebaWeb contexto = new();

    public ConfirmarDesactivacionTests()
    {
        // El diálogo modal puede necesitar JavaScript (showModal); estas pruebas no fijan esas llamadas.
        contexto.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    [Fact]
    public void Desactivar_AbreElDialogoConTituloTextoYBotones()
    {
        var corte = RenderizarListado();

        Pulsar(corte, "Desactivar a Bruno Díaz");

        var dialogo = DialogoAbierto(corte, Bruno);
        Assert.Contains("Bruno Díaz dejará de tener acceso a Kryon. Puedes volver a activarlo más adelante.", Normalizar(dialogo.TextContent), StringComparison.Ordinal);
        Assert.Single(Controles(dialogo), c => NombreAccesible(c) == "Desactivar");
        Assert.Single(Controles(dialogo), c => NombreAccesible(c) == "Cancelar");
    }

    [Fact]
    public void AlAbrir_ElFocoVaACancelar()
    {
        var corte = RenderizarListado();

        Pulsar(corte, "Desactivar a Bruno Díaz");

        var cancelar = Assert.Single(Controles(DialogoAbierto(corte, Bruno)), c => NombreAccesible(c) == "Cancelar");
        corte.WaitForAssertion(() => Assert.True(
            cancelar.HasAttribute("autofocus") || FocoEn(cancelar),
            "El foco inicial no va a \"Cancelar\" (ni autofocus ni FocusAsync)."));
    }

    [Fact]
    public void Cancelar_CierraSinLlamarALaApi_YDevuelveElFocoAlBotonDeOrigen()
    {
        var corte = RenderizarListado();
        var origen = Pulsar(corte, "Desactivar a Bruno Díaz");

        var cancelar = Assert.Single(Controles(DialogoAbierto(corte, Bruno)), c => NombreAccesible(c) == "Cancelar");
        cancelar.Click();

        corte.WaitForAssertion(() => Assert.Null(Dialogo(corte, Bruno)));
        Assert.DoesNotContain(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Post);
        corte.WaitForAssertion(() => Assert.True(FocoEn(Fila(corte, Bruno).QuerySelectorAll("button, a").Single(c => NombreAccesible(c) == NombreAccesible(origen))),
            "El foco no vuelve al botón \"Desactivar a Bruno Díaz\"."));
    }

    [Fact]
    public void Escape_EquivaleACancelar()
    {
        var corte = RenderizarListado();
        Pulsar(corte, "Desactivar a Bruno Díaz");
        var dialogo = DialogoAbierto(corte, Bruno);

        try
        {
            dialogo.TriggerEvent("oncancel", EventArgs.Empty);
        }
        catch (MissingEventHandlerException)
        {
            dialogo.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Escape", Code = "Escape" });
        }

        corte.WaitForAssertion(() => Assert.Null(Dialogo(corte, Bruno)));
        Assert.DoesNotContain(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Post);
    }

    [Fact]
    public void ConfirmarDesactivar_EnviaLaVersion_YMuestraElExito()
    {
        var corte = RenderizarListado();
        Pulsar(corte, "Desactivar a Bruno Díaz");

        Assert.Single(Controles(DialogoAbierto(corte, Bruno)), c => NombreAccesible(c) == "Desactivar").Click();

        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[aria-live='polite']"),
            region => Normalizar(region.TextContent).Contains("Bruno Díaz fue desactivado.", StringComparison.Ordinal)));
        var solicitud = Assert.Single(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Post);
        Assert.Equal($"/api/usuarios/{Bruno.Id}/desactivacion", solicitud.Solicitud.RequestUri!.AbsolutePath);
        Assert.Equal(Bruno.Version, solicitud.Solicitud.Headers.IfMatch.ToString());
        Assert.Null(Dialogo(corte, Bruno));
    }

    [Fact]
    public void Activar_NoAbreDialogo_YMuestraElExito()
    {
        var corte = RenderizarListado();

        Pulsar(corte, "Activar a Carmen Ruiz");

        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[aria-live='polite']"),
            region => Normalizar(region.TextContent).Contains("Carmen Ruiz fue activado.", StringComparison.Ordinal)));
        Assert.DoesNotContain(corte.FindAll("dialog"), d => d.TextContent.Contains("Desactivar a", StringComparison.Ordinal));
        var solicitud = Assert.Single(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Post);
        Assert.Equal($"/api/usuarios/{Carmen.Id}/activacion", solicitud.Solicitud.RequestUri!.AbsolutePath);
        Assert.Equal(Carmen.Version, solicitud.Solicitud.Headers.IfMatch.ToString());
    }

    /// <summary>
    /// Listado real con Bruno (activo, se puede desactivar) y Carmen (inactiva, se puede activar). Desactivar y activar
    /// responden 200 con el usuario cambiado y su nuevo ETag.
    /// </summary>
    private IRenderedComponent<ListadoUsuarios> RenderizarListado()
    {
        contexto.Http.Responder = (solicitud, _) => (solicitud.Method.Method, solicitud.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/api/usuarios") => HttpFalso.Json(HttpStatusCode.OK, new PaginaUsuarios
            {
                Elementos = [Bruno, Carmen],
                Total = 2,
                Pagina = 1,
                TamanoPagina = 25,
                PuedeRegistrar = false,
            }),
            ("GET", "/api/usuarios/roles-asignables") => HttpFalso.Json(HttpStatusCode.OK, Array.Empty<RolResumen>()),
            ("POST", var ruta) when ruta == $"/api/usuarios/{Bruno.Id}/desactivacion" =>
                HttpFalso.Json(HttpStatusCode.OK, Detalle(Bruno, "inactivo", "\"AAAAAAAAB9M=\""), "\"AAAAAAAAB9M=\""),
            ("POST", var ruta) when ruta == $"/api/usuarios/{Carmen.Id}/activacion" =>
                HttpFalso.Json(HttpStatusCode.OK, Detalle(Carmen, "activo", "\"AAAAAAAAB9Q=\""), "\"AAAAAAAAB9Q=\""),
            var (metodo, ruta) => throw new InvalidOperationException($"Solicitud no prevista en T059: {metodo} {ruta}"),
        };
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo("/usuarios");

        var corte = contexto.Render<ListadoUsuarios>();
        corte.WaitForAssertion(() => Assert.Equal(2, corte.FindAll("tbody tr").Count));
        return corte;
    }

    /// <summary>Pulsa el control de la tabla con ese nombre accesible y lo devuelve.</summary>
    private static IElement Pulsar(IRenderedComponent<ListadoUsuarios> corte, string nombreAccesible)
    {
        var control = Assert.Single(Controles(corte.Find("table")), c => NombreAccesible(c) == nombreAccesible);
        control.Click();
        return control;
    }

    private static IElement Fila(IRenderedComponent<ListadoUsuarios> corte, UsuarioResumen usuario) =>
        Assert.Single(corte.FindAll("tbody tr"), f => f.TextContent.Contains(usuario.IdentificadorAcceso, StringComparison.Ordinal));

    /// <summary>El diálogo de <paramref name="usuario"/>, identificado por su título; null si no está abierto.</summary>
    private static IElement? Dialogo(IRenderedComponent<ListadoUsuarios> corte, UsuarioResumen usuario) =>
        corte.FindAll("dialog").SingleOrDefault(d => d.QuerySelectorAll("h1, h2, h3, [role='heading']")
            .Any(t => Normalizar(t.TextContent) == $"Desactivar a {usuario.NombreCompleto}"));

    private static IElement DialogoAbierto(IRenderedComponent<ListadoUsuarios> corte, UsuarioResumen usuario)
    {
        IElement? dialogo = null;
        corte.WaitForAssertion(() =>
        {
            dialogo = Dialogo(corte, usuario);
            Assert.True(dialogo is not null, $"No se abrió el diálogo \"Desactivar a {usuario.NombreCompleto}\".");
        });
        return dialogo!;
    }

    /// <summary>Alguna llamada a <c>FocusAsync</c> registrada por bUnit apunta a <paramref name="elemento"/>.</summary>
    private bool FocoEn(IElement elemento) =>
        contexto.JSInterop.Invocations[FuncionFoco].Any(llamada =>
        {
            try
            {
                llamada.Arguments[0]!.ShouldBeElementReferenceTo(elemento);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        });

    private static IEnumerable<IElement> Controles(IElement raiz) => raiz.QuerySelectorAll("button, a");

    /// <summary>
    /// Nombre accesible de un botón o enlace: el texto de <c>aria-labelledby</c> (en su orden); si no, <c>aria-label</c>;
    /// si no, su texto.
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

    private static UsuarioResumen Usuario(string id, string nombre, string identificador, string estado, string version, bool activar = false, bool desactivar = false) => new()
    {
        Id = Guid.Parse(id),
        NombreCompleto = nombre,
        IdentificadorAcceso = identificador,
        Rol = new RolResumen { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" },
        Estado = estado,
        EsCuentaPropia = false,
        Version = version,
        Acciones = new AccionesPermitidas
        {
            VerDetalle = true,
            Editar = true,
            CambiarRol = true,
            EditarIdentificador = false,
            Activar = activar,
            Desactivar = desactivar,
        },
    };

    private static DetalleUsuario Detalle(UsuarioResumen usuario, string estado, string version) => new()
    {
        Id = usuario.Id,
        NombreCompleto = usuario.NombreCompleto,
        IdentificadorAcceso = usuario.IdentificadorAcceso,
        Rol = usuario.Rol,
        Estado = estado,
        EsCuentaPropia = false,
        Version = version,
        Acciones = usuario.Acciones with { Activar = estado == "inactivo", Desactivar = estado == "activo" },
    };
}
