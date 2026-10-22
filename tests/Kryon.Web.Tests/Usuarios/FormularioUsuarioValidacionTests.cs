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
/// Validación accesible del formulario de edición (contracts/ui-usuarios.md → <i>Formulario de registro y edición</i>;
/// FR-024, FR-025, FR-029, US4-6) a través de la página real <c>/usuarios/{id}/editar</c> con el
/// <see cref="ClienteUsuarios"/> real sobre el HTTP falso de <see cref="ContextoPruebaWeb"/>.
/// </summary>
/// <remarks>
/// Test-first: el formulario (T083) y la página de edición (T084) aún no existen. La página se localiza por nombre
/// (<c>EditarUsuario</c>) en tiempo de ejecución y recibe el parámetro de ruta <c>Id</c>; mientras falte, cada prueba
/// falla diciendo qué tarea la aporta. La marca de obligatorio se busca como texto ("obligatorio") en la etiqueta, y los
/// errores de <c>errors</c> se asocian a su campo por el nombre del campo del contrato.
/// </remarks>
public sealed class FormularioUsuarioValidacionTests : IAsyncDisposable
{
    private const string QueryListado = "?busqueda=ana&pagina=2";
    private const string FuncionFoco = "Blazor._internal.domWrapper.focus";

    private static readonly RolResumen Ventas = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" };
    private static readonly RolResumen Compras = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nombre = "Compras" };

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

    public FormularioUsuarioValidacionTests()
    {
        contexto.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    [Fact]
    public void Campos_TienenEtiquetaVisibleYMarcaDeObligatorioEnTexto()
    {
        var corte = RenderizarEdicion(_ => throw new InvalidOperationException("No debe guardarse."));

        foreach (var campo in new[] { "Nombre completo", "Identificador de acceso", "Rol" })
        {
            var etiqueta = Etiqueta(corte, campo);
            Assert.False(string.IsNullOrWhiteSpace(etiqueta.GetAttribute("for")), $"La etiqueta \"{campo}\" no está asociada a su campo.");
            Assert.NotNull(corte.Find($"[id='{etiqueta.GetAttribute("for")}']"));
            Assert.Null(etiqueta.GetAttribute("hidden"));
        }

        Assert.Contains("obligatorio", Normalizar(Etiqueta(corte, "Nombre completo").TextContent), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("obligatorio", Normalizar(Etiqueta(corte, "Rol").TextContent), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErroresDeLaApi_SeMuestranJuntoACadaCampo_YElResumenRecibeElFoco()
    {
        var corte = RenderizarEdicion(_ => HttpFalso.ConProblema(new Problema
        {
            Type = "https://kryon.app/errores/validacion",
            Title = "Los datos enviados no son válidos",
            Status = 400,
            Codigo = CodigosError.Validacion,
            Errors = new Dictionary<string, string[]> { ["nombreCompleto"] = ["El nombre completo es obligatorio."] },
        }));

        Escribir(Campo(corte, "Nombre completo"), string.Empty);
        Guardar(corte);

        // Junto al campo: aria-invalid y aria-describedby apuntando al texto del error.
        corte.WaitForAssertion(() => Assert.Equal("true", Campo(corte, "Nombre completo").GetAttribute("aria-invalid")));
        var nombre = Campo(corte, "Nombre completo");
        var descritoPor = (nombre.GetAttribute("aria-describedby") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(descritoPor, id => Normalizar(corte.Find($"[id='{id}']").TextContent).Contains("El nombre completo es obligatorio.", StringComparison.Ordinal));
        Assert.NotEqual("true", Campo(corte, "Rol").GetAttribute("aria-invalid"));

        // Arriba: un resumen con role="alert" y enlaces a cada campo con error, que recibe el foco.
        var resumen = Assert.Single(corte.FindAll("[role='alert']"), r => r.QuerySelectorAll("a[href]").Length > 0);
        var enlace = Assert.Single(resumen.QuerySelectorAll("a[href]"));
        Assert.Equal($"#{nombre.Id}", enlace.GetAttribute("href"));
        Assert.Contains("El nombre completo es obligatorio.", Normalizar(resumen.TextContent), StringComparison.Ordinal);
        var referencia = resumen.GetAttribute("blazor:elementReference");
        corte.WaitForAssertion(() => Assert.True(
            !string.IsNullOrEmpty(referencia) && contexto.JSInterop.Invocations[FuncionFoco].Any(l => l.Arguments[0] is ElementReference r && r.Id == referencia),
            "El foco no va al resumen de errores."));
    }

    [Fact]
    public void Cancelar_VuelveAlListadoConSuQuery_SinGuardar()
    {
        var corte = RenderizarEdicion(_ => throw new InvalidOperationException("Cancelar no debe guardar."));

        Escribir(Campo(corte, "Nombre completo"), "Otro nombre");
        Assert.Single(Controles(corte), c => NombreAccesible(c) == "Cancelar").Click();

        var navegacion = contexto.Services.GetRequiredService<NavigationManager>();
        corte.WaitForAssertion(() => Assert.EndsWith($"/usuarios{QueryListado}", navegacion.Uri, StringComparison.Ordinal));
        Assert.DoesNotContain(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Put);
    }

    /// <summary>
    /// Renderiza la página de edición real de Bruno llegando desde el listado con <see cref="QueryListado"/>. El PUT
    /// responde con <paramref name="alGuardar"/>.
    /// </summary>
    private IRenderedComponent<IComponent> RenderizarEdicion(Func<HttpRequestMessage, HttpResponseMessage> alGuardar)
    {
        var tipo = typeof(ClienteUsuarios).Assembly.GetTypes()
            .SingleOrDefault(t => t.Name == "EditarUsuario" && typeof(IComponent).IsAssignableFrom(t));
        if (tipo is null)
        {
            Assert.Fail("La página EditarUsuario aún no está implementada (T084).");
        }

        contexto.Http.Responder = (solicitud, _) => (solicitud.Method.Method, solicitud.RequestUri!.AbsolutePath) switch
        {
            ("GET", var ruta) when ruta == $"/api/usuarios/{Bruno.Id}" => HttpFalso.Json(HttpStatusCode.OK, Bruno, Bruno.Version),
            ("GET", "/api/usuarios/roles-asignables") => HttpFalso.Json(HttpStatusCode.OK, new[] { Ventas, Compras }),
            ("PUT", var ruta) when ruta == $"/api/usuarios/{Bruno.Id}" => alGuardar(solicitud),
            var (metodo, ruta) => throw new InvalidOperationException($"Solicitud no prevista en T076: {metodo} {ruta}"),
        };
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo($"/usuarios/{Bruno.Id}/editar{QueryListado}");

        var corte = (IRenderedComponent<IComponent>)contexto.Render(builder =>
        {
            builder.OpenComponent(0, tipo);
            builder.AddComponentParameter(1, "Id", Bruno.Id);
            builder.CloseComponent();
        });
        corte.WaitForAssertion(() => Assert.Equal("Bruno Díaz", Campo(corte, "Nombre completo").GetAttribute("value")));
        return corte;
    }

    private static IElement Etiqueta(IRenderedComponent<IComponent> corte, string campo) =>
        Assert.Single(corte.FindAll("label"), l => Normalizar(l.TextContent).StartsWith(campo, StringComparison.Ordinal));

    private static IElement Campo(IRenderedComponent<IComponent> corte, string campo) =>
        corte.Find($"[id='{Etiqueta(corte, campo).GetAttribute("for")}']");

    /// <summary>Escribe en el campo con el evento que tenga enlazado (<c>change</c> o <c>input</c>).</summary>
    private static void Escribir(IElement campo, string valor)
    {
        try
        {
            campo.Change(valor);
        }
        catch (MissingEventHandlerException)
        {
            campo.Input(valor);
        }
    }

    /// <summary>Pulsa "Guardar": su manejador, o el envío de su formulario.</summary>
    private static void Guardar(IRenderedComponent<IComponent> corte)
    {
        var guardar = Assert.Single(Controles(corte), c => NombreAccesible(c) == "Guardar");
        try
        {
            guardar.Click();
        }
        catch (MissingEventHandlerException)
        {
            (guardar.Closest("form") ?? throw new Xunit.Sdk.XunitException("\"Guardar\" no tiene manejador ni formulario.")).Submit();
        }
    }

    private static IEnumerable<IElement> Controles(IRenderedComponent<IComponent> corte) => corte.FindAll("button, a");

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
}
