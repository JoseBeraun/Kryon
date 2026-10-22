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
/// Fallo de comunicación al desactivar (tras confirmar) y al activar (EC-8, FR-040, FR-042): con el mecanismo de T042,
/// el <see cref="HttpMessageHandler"/> falso lanza <see cref="HttpRequestException"/> en el POST. Se muestra el mensaje
/// del contrato en <c>role="alert"</c>, sin mensaje de éxito, y la fila conserva el estado anterior.
/// </summary>
/// <remarks>
/// Test-first: depende del diálogo (T067) y de la conexión de las acciones en el listado (T068). Para desactivar se pulsa
/// el botón "Desactivar" del <c>&lt;dialog&gt;</c> abierto.
/// </remarks>
public sealed class FalloComunicacionTests : IAsyncDisposable
{
    private const string MensajeSinRespuesta = "No se pudo confirmar el resultado. Revisa el listado y vuelve a intentarlo.";

    private static readonly UsuarioResumen Bruno = Usuario(
        "20000000-0000-0000-0000-000000000002", "Bruno Díaz", "bruno.diaz", "activo", desactivar: true);

    private static readonly UsuarioResumen Carmen = Usuario(
        "20000000-0000-0000-0000-000000000003", "Carmen Ruiz", "carmen.ruiz", "inactivo", activar: true);

    private readonly ContextoPruebaWeb contexto = new();

    public FalloComunicacionTests()
    {
        contexto.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    [Fact]
    public void Desactivar_SinRespuesta_MuestraElMensajeDelContrato_YLaFilaNoCambia()
    {
        var corte = RenderizarListado();

        Pulsar(corte.Find("table"), "Desactivar a Bruno Díaz");
        IElement? dialogo = null;
        corte.WaitForAssertion(() =>
        {
            dialogo = corte.FindAll("dialog").SingleOrDefault(d => d.TextContent.Contains("Desactivar a Bruno Díaz", StringComparison.Ordinal));
            Assert.True(dialogo is not null, "No se abrió el diálogo de confirmación.");
        });
        Pulsar(dialogo!, "Desactivar");

        AssertFalloSinCambios(corte, Bruno, "Activo", "Desactivar a Bruno Díaz", "desactivacion");
    }

    [Fact]
    public void Activar_SinRespuesta_MuestraElMensajeDelContrato_YLaFilaNoCambia()
    {
        var corte = RenderizarListado();

        Pulsar(corte.Find("table"), "Activar a Carmen Ruiz");

        AssertFalloSinCambios(corte, Carmen, "Inactivo", "Activar a Carmen Ruiz", "activacion");
    }

    private void AssertFalloSinCambios(
        IRenderedComponent<ListadoUsuarios> corte,
        UsuarioResumen usuario,
        string estadoAnterior,
        string accion,
        string operacion)
    {
        // (1) y (2): el mensaje del contrato se anuncia en la región de error.
        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[role='alert']"),
            region => Normalizar(region.TextContent).Contains(MensajeSinRespuesta, StringComparison.Ordinal)));

        // Hubo un intento real de la operación (el fallo no es un error de la prueba).
        Assert.Contains(contexto.Http.Solicitudes, s =>
            s.Solicitud.Method == HttpMethod.Post && s.Solicitud.RequestUri!.AbsolutePath == $"/api/usuarios/{usuario.Id}/{operacion}");

        // (3): ningún mensaje de éxito.
        Assert.All(corte.FindAll("[aria-live='polite']"), region =>
        {
            Assert.DoesNotContain("fue desactivado", region.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("fue activado", region.TextContent, StringComparison.Ordinal);
        });

        // (4): la fila sigue en la tabla con el estado y las acciones de antes.
        var fila = Assert.Single(corte.FindAll("tbody tr"), f => f.TextContent.Contains(usuario.IdentificadorAcceso, StringComparison.Ordinal));
        Assert.Contains(estadoAnterior, fila.QuerySelectorAll("td").Select(c => Normalizar(c.TextContent)));
        Assert.Single(fila.QuerySelectorAll("button, a"), c => NombreAccesible(c) == accion);
        Assert.DoesNotContain(corte.FindAll("dialog"), d => d.TextContent.Contains("Desactivar a", StringComparison.Ordinal));
    }

    /// <summary>Listado real con Bruno (activo) y Carmen (inactiva); cualquier POST falla sin respuesta.</summary>
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
            ("POST", _) => throw new HttpRequestException("Sin conexión con la API (simulado por la prueba)."),
            var (metodo, ruta) => throw new InvalidOperationException($"Solicitud no prevista en T060: {metodo} {ruta}"),
        };
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo("/usuarios");

        var corte = contexto.Render<ListadoUsuarios>();
        corte.WaitForAssertion(() => Assert.Equal(2, corte.FindAll("tbody tr").Count));
        return corte;
    }

    private static void Pulsar(IElement raiz, string nombreAccesible) =>
        Assert.Single(raiz.QuerySelectorAll("button, a"), c => NombreAccesible(c) == nombreAccesible).Click();

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

    private static UsuarioResumen Usuario(string id, string nombre, string identificador, string estado, bool activar = false, bool desactivar = false) => new()
    {
        Id = Guid.Parse(id),
        NombreCompleto = nombre,
        IdentificadorAcceso = identificador,
        Rol = new RolResumen { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" },
        Estado = estado,
        EsCuentaPropia = false,
        Version = "\"AAAAAAAAB9E=\"",
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
}
