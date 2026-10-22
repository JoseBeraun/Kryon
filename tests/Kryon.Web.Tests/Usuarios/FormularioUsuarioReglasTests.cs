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
/// Reglas del formulario de edición (contracts/ui-usuarios.md → <i>Formulario de registro y edición</i>; FR-035, FR-039)
/// a través de la página real <c>/usuarios/{id}/editar</c>: el rol de la propia cuenta en solo lectura, el identificador
/// editable o de solo lectura <b>según</b> <c>acciones.editarIdentificador</c> (la interfaz obedece la bandera; quién la
/// decide es DEP-4, T079/T080), y la recarga de <c>actual</c> ante <c>usuario-modificado</c>.
/// </summary>
/// <remarks>
/// Test-first, como <see cref="FormularioUsuarioValidacionTests"/>: mientras la página EditarUsuario (T084) no exista,
/// cada prueba falla diciendo qué tarea la aporta.
/// </remarks>
public sealed class FormularioUsuarioReglasTests : IAsyncDisposable
{
    private static readonly RolResumen Ventas = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" };
    private static readonly RolResumen Compras = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nombre = "Compras" };

    private readonly ContextoPruebaWeb contexto = new();

    public FormularioUsuarioReglasTests()
    {
        contexto.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    [Fact]
    public void CuentaPropia_ElRolEsDeSoloLectura_ConElTextoDeAyuda()
    {
        var corte = RenderizarEdicion(Usuario(propia: true, editarIdentificador: false), _ => throw new InvalidOperationException());

        Assert.Contains("No puedes cambiar tu propio rol", Normalizar(corte.Markup), StringComparison.Ordinal);
        Assert.False(EsEditable(CampoORnulo(corte, "Rol")), "El rol de la propia cuenta no debe poder cambiarse.");
        Assert.Contains("Ventas", Normalizar(corte.Markup), StringComparison.Ordinal);
    }

    [Fact]
    public void OtraCuenta_ElRolEsEditable_YSinElTextoDeAyuda()
    {
        var corte = RenderizarEdicion(Usuario(propia: false, editarIdentificador: false), _ => throw new InvalidOperationException());

        Assert.DoesNotContain("No puedes cambiar tu propio rol", Normalizar(corte.Markup), StringComparison.Ordinal);
        Assert.True(EsEditable(CampoORnulo(corte, "Rol")), "El rol de otra cuenta debe poder elegirse.");
    }

    /// <summary>La interfaz obedece la bandera de la API; no decide por su cuenta si el identificador es editable.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Identificador_EditableSoloSegunLaBandera(bool editarIdentificador)
    {
        var corte = RenderizarEdicion(Usuario(propia: false, editarIdentificador), _ => throw new InvalidOperationException());

        Assert.Equal(editarIdentificador, EsEditable(CampoORnulo(corte, "Identificador de acceso")));
        Assert.Contains("bruno.diaz", Normalizar(corte.Markup), StringComparison.Ordinal);
    }

    [Fact]
    public void UsuarioModificado_MuestraElMensaje_YRecargaLosDatosActuales()
    {
        var original = Usuario(propia: false, editarIdentificador: false);
        var vigente = original with { NombreCompleto = "Bruno Díaz Vigente", Rol = Compras, Version = "\"AAAAAAAAB9I=\"" };
        var corte = RenderizarEdicion(original, _ => HttpFalso.ConProblema(new Problema
        {
            Type = "https://kryon.app/errores/usuario-modificado",
            Title = "Otra persona modificó este usuario",
            Status = 412,
            Codigo = CodigosError.UsuarioModificado,
            Actual = vigente,
        }));

        Escribir(Campo(corte, "Nombre completo"), "Mi cambio");
        Guardar(corte);

        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[role='alert']"),
            r => Normalizar(r.TextContent).Contains(TextosErrores.ParaCodigo(CodigosError.UsuarioModificado), StringComparison.Ordinal)));
        corte.WaitForAssertion(() => Assert.Equal("Bruno Díaz Vigente", Campo(corte, "Nombre completo").GetAttribute("value")));
        Assert.Equal(Compras.Id.ToString(), Campo(corte, "Rol").GetAttribute("value"));
        var put = Assert.Single(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Put);
        Assert.Equal(original.Version, put.Solicitud.Headers.IfMatch.ToString());
    }

    private static DetalleUsuario Usuario(bool propia, bool editarIdentificador) => new()
    {
        Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
        NombreCompleto = "Bruno Díaz",
        IdentificadorAcceso = "bruno.diaz",
        Rol = Ventas,
        Estado = "activo",
        EsCuentaPropia = propia,
        Version = "\"AAAAAAAAB9E=\"",
        Acciones = new()
        {
            VerDetalle = true,
            Editar = true,
            CambiarRol = !propia,
            EditarIdentificador = editarIdentificador,
            Activar = false,
            Desactivar = !propia,
        },
    };

    private IRenderedComponent<IComponent> RenderizarEdicion(DetalleUsuario usuario, Func<HttpRequestMessage, HttpResponseMessage> alGuardar)
    {
        var tipo = typeof(ClienteUsuarios).Assembly.GetTypes()
            .SingleOrDefault(t => t.Name == "EditarUsuario" && typeof(IComponent).IsAssignableFrom(t));
        if (tipo is null)
        {
            Assert.Fail("La página EditarUsuario aún no está implementada (T084).");
        }

        contexto.Http.Responder = (solicitud, _) => (solicitud.Method.Method, solicitud.RequestUri!.AbsolutePath) switch
        {
            ("GET", var ruta) when ruta == $"/api/usuarios/{usuario.Id}" => HttpFalso.Json(HttpStatusCode.OK, usuario, usuario.Version),
            ("GET", "/api/usuarios/roles-asignables") => HttpFalso.Json(HttpStatusCode.OK, new[] { Ventas, Compras }),
            ("PUT", var ruta) when ruta == $"/api/usuarios/{usuario.Id}" => alGuardar(solicitud),
            var (metodo, ruta) => throw new InvalidOperationException($"Solicitud no prevista en T077: {metodo} {ruta}"),
        };
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo($"/usuarios/{usuario.Id}/editar");

        var corte = (IRenderedComponent<IComponent>)contexto.Render(builder =>
        {
            builder.OpenComponent(0, tipo);
            builder.AddComponentParameter(1, "Id", usuario.Id);
            builder.CloseComponent();
        });
        corte.WaitForAssertion(() => Assert.Contains(usuario.NombreCompleto, corte.Markup, StringComparison.Ordinal));
        return corte;
    }

    /// <summary>El control asociado a la etiqueta del campo, o null si el campo se muestra sin control (solo texto).</summary>
    private static IElement? CampoORnulo(IRenderedComponent<IComponent> corte, string campo)
    {
        var etiqueta = corte.FindAll("label, dt").FirstOrDefault(l => Normalizar(l.TextContent).StartsWith(campo, StringComparison.Ordinal));
        Assert.True(etiqueta is not null, $"No se muestra el campo \"{campo}\".");
        var id = etiqueta.GetAttribute("for");
        return string.IsNullOrEmpty(id) ? null : corte.FindAll($"[id='{id}']").SingleOrDefault();
    }

    private static IElement Campo(IRenderedComponent<IComponent> corte, string campo) =>
        CampoORnulo(corte, campo) ?? throw new Xunit.Sdk.XunitException($"El campo \"{campo}\" no tiene control.");

    /// <summary>Editable: un input, select o textarea sin <c>readonly</c>, <c>disabled</c> ni <c>aria-readonly="true"</c>.</summary>
    private static bool EsEditable(IElement? control) =>
        control is not null
        && control.LocalName is "input" or "select" or "textarea"
        && !control.HasAttribute("readonly")
        && !control.HasAttribute("disabled")
        && control.GetAttribute("aria-readonly") != "true";

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

    private static void Guardar(IRenderedComponent<IComponent> corte)
    {
        var guardar = Assert.Single(corte.FindAll("button"), b => Normalizar(b.GetAttribute("aria-label") ?? b.TextContent) == "Guardar");
        try
        {
            guardar.Click();
        }
        catch (MissingEventHandlerException)
        {
            (guardar.Closest("form") ?? throw new Xunit.Sdk.XunitException("\"Guardar\" no tiene manejador ni formulario.")).Submit();
        }
    }

    private static string Normalizar(string texto) => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
