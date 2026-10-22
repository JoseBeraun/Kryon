using System.Net;
using System.Text.Json;

using AngleSharp.Dom;

using Bunit;

using Kryon.Contracts.Usuarios;
using Kryon.Web.Tests.Infraestructura;
using Kryon.Web.Usuarios;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Web.Tests.Usuarios;

/// <summary>
/// Página de registro <c>/usuarios/nuevo</c> (T089; contracts/ui-usuarios.md → <i>Formulario de registro y edición</i>
/// y <i>Mensajes</i>; US4-2, US4-3, FR-022, FR-024 a FR-028, FR-042, FR-050) con el <see cref="ClienteUsuarios"/> real
/// sobre el HTTP falso. El recorrido empieza en el listado: "Nuevo usuario" lleva su query string, y Guardar y Cancelar
/// vuelven con ella. Las reglas las aplica la API; aquí solo se comprueba cómo se muestran sus respuestas.
/// </summary>
public sealed class NuevoUsuarioTests : IAsyncDisposable
{
    private const string QueryListado = "?busqueda=ana&estado=activo&pagina=2";

    private static readonly RolResumen Ventas = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" };
    private static readonly RolResumen Compras = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nombre = "Compras" };

    private static readonly DetalleUsuario Creado = new()
    {
        Id = Guid.Parse("20000000-0000-0000-0000-0000000000e1"),
        NombreCompleto = "Elena Soto",
        IdentificadorAcceso = "elena.soto",
        Rol = Compras,
        Estado = "activo",
        EsCuentaPropia = false,
        Version = "\"AAAAAAAAB+E=\"",
        Acciones = new() { VerDetalle = true, Editar = true, CambiarRol = true, EditarIdentificador = false, Activar = false, Desactivar = true },
    };

    private readonly ContextoPruebaWeb contexto = new();

    /// <summary>Respuesta del POST de cada prueba.</summary>
    private Func<HttpResponseMessage> alRegistrar = () => throw new InvalidOperationException("Esta prueba no debe registrar.");

    public NuevoUsuarioTests()
    {
        contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        contexto.Http.Responder = (solicitud, _) => (solicitud.Method.Method, solicitud.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/api/usuarios") => HttpFalso.Json(HttpStatusCode.OK, new PaginaUsuarios
            {
                Elementos = [],
                Total = 0,
                Pagina = 2,
                TamanoPagina = 25,
                PuedeRegistrar = true,
            }),
            ("GET", "/api/usuarios/roles-asignables") => HttpFalso.Json(HttpStatusCode.OK, new[] { Ventas, Compras }),
            ("POST", "/api/usuarios") => alRegistrar(),
            var (metodo, ruta) => throw new InvalidOperationException($"Solicitud no prevista en T089: {metodo} {ruta}"),
        };
    }

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    [Fact]
    public void Formulario_CargaLosRoles_ConLosTresCamposEditablesYObligatorios()
    {
        var corte = EntrarDesdeElListado();

        foreach (var campo in new[] { "Nombre completo", "Identificador de acceso", "Rol" })
        {
            Assert.Contains("obligatorio", Normalizar(Etiqueta(corte, campo).TextContent), StringComparison.OrdinalIgnoreCase);
            var control = Campo(corte, campo);
            Assert.False(control.HasAttribute("readonly") || control.HasAttribute("disabled"), $"\"{campo}\" debe poder editarse.");
        }

        var opciones = Campo(corte, "Rol").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray();
        Assert.Contains(Ventas.Id.ToString(), opciones);
        Assert.Contains(Compras.Id.ToString(), opciones);
        Assert.DoesNotContain("No puedes cambiar tu propio rol", corte.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Guardar_EnviaLaSolicitud_VuelveAlListadoConSuQuery_YElListadoAnunciaElExito()
    {
        alRegistrar = () => HttpFalso.Json(HttpStatusCode.Created, Creado, Creado.Version);
        var corte = EntrarDesdeElListado();

        Rellenar(corte, "  Elena Soto  ", "elena.soto", Compras.Id);
        corte.Find("form").Submit();

        var navegacion = contexto.Services.GetRequiredService<NavigationManager>();
        corte.WaitForAssertion(() => Assert.EndsWith($"/usuarios{QueryListado}", navegacion.Uri, StringComparison.Ordinal));

        // Se envían los datos tal cual: las reglas y la normalización las aplica la API.
        var post = Assert.Single(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Post);
        using var cuerpo = JsonDocument.Parse(post.Cuerpo!);
        Assert.Equal(["nombreCompleto", "identificadorAcceso", "rolId"], cuerpo.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("  Elena Soto  ", cuerpo.RootElement.GetProperty("nombreCompleto").GetString());
        Assert.Equal("elena.soto", cuerpo.RootElement.GetProperty("identificadorAcceso").GetString());
        Assert.Equal(Compras.Id, cuerpo.RootElement.GetProperty("rolId").GetGuid());

        // Con el nombre que confirmó la API, en la región aria-live="polite" del listado.
        var listado = contexto.Render<ListadoUsuarios>();
        listado.WaitForAssertion(() => Assert.Contains(
            "Usuario Elena Soto guardado.",
            Normalizar(listado.Find("[aria-live='polite']").TextContent),
            StringComparison.Ordinal));
    }

    [Fact]
    public void Cancelar_VuelveAlListadoConSuQuery_SinRegistrar()
    {
        var corte = EntrarDesdeElListado();

        Rellenar(corte, "Elena Soto", "elena.soto", Compras.Id);
        Assert.Single(corte.FindAll("button"), b => Normalizar(b.TextContent) == "Cancelar").Click();

        var navegacion = contexto.Services.GetRequiredService<NavigationManager>();
        corte.WaitForAssertion(() => Assert.EndsWith($"/usuarios{QueryListado}", navegacion.Uri, StringComparison.Ordinal));
        Assert.DoesNotContain(contexto.Http.Solicitudes, s => s.Solicitud.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData("validacion", "nombreCompleto", "Nombre completo", "El nombre completo es obligatorio.")]
    [InlineData("validacion", "identificadorAcceso", "Identificador de acceso", "Problema de formato informado por la API.")]
    [InlineData("identificador-en-uso", null, "Identificador de acceso", "Este identificador de acceso no está disponible. Usa otro.")]
    [InlineData("rol-no-valido", null, "Rol", "Selecciona un rol de la lista.")]
    public void RechazoDeUnCampo_SeMuestraJuntoAEse_ConResumenYSinSalir(string codigo, string? campoErrors, string campo, string mensaje)
    {
        alRegistrar = () => HttpFalso.ConProblema(new Problema
        {
            Type = $"https://kryon.app/errores/{codigo}",
            Title = "Rechazo",
            Status = codigo == "validacion" ? 400 : 409,
            Codigo = codigo,
            Errors = campoErrors is null ? null : new Dictionary<string, string[]> { [campoErrors] = [mensaje] },
        });
        var corte = EntrarDesdeElListado();

        Rellenar(corte, "Elena Soto", "elena.soto", Compras.Id);
        corte.Find("form").Submit();

        corte.WaitForAssertion(() => Assert.Equal("true", Campo(corte, campo).GetAttribute("aria-invalid")));
        var descritoPor = (Campo(corte, campo).GetAttribute("aria-describedby") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(descritoPor, id => Normalizar(corte.Find($"[id='{id}']").TextContent).Contains(mensaje, StringComparison.Ordinal));
        var resumen = Assert.Single(corte.FindAll("[role='alert']"), r => r.QuerySelectorAll("a[href]").Length > 0);
        Assert.Equal($"#{Campo(corte, campo).Id}", Assert.Single(resumen.QuerySelectorAll("a[href]")).GetAttribute("href"));

        // No hubo éxito: se queda en el formulario y no deja ningún aviso para el listado.
        Assert.EndsWith($"/usuarios/nuevo{QueryListado}", contexto.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.DoesNotContain("guardado", corte.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void LimiteAlcanzado_MuestraElMensajeGeneral_SinSalir()
    {
        alRegistrar = () => HttpFalso.ConProblema(new Problema
        {
            Type = "https://kryon.app/errores/limite-usuarios-alcanzado",
            Title = "Límite",
            Status = 409,
            Codigo = CodigosError.LimiteUsuariosAlcanzado,
        });
        var corte = EntrarDesdeElListado();

        Rellenar(corte, "Elena Soto", "elena.soto", Compras.Id);
        corte.Find("form").Submit();

        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[role='alert']"),
            r => Normalizar(r.TextContent).Contains("No se registró el usuario: la empresa alcanzó su límite de usuarios.", StringComparison.Ordinal)));
        Assert.EndsWith($"/usuarios/nuevo{QueryListado}", contexto.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void SinRespuesta_MuestraElTextoDeFalloDeRed_SinFalsoExito()
    {
        alRegistrar = () => throw new HttpRequestException("Sin conexión simulada.");
        var corte = EntrarDesdeElListado();

        Rellenar(corte, "Elena Soto", "elena.soto", Compras.Id);
        corte.Find("form").Submit();

        corte.WaitForAssertion(() => Assert.Contains(
            corte.FindAll("[role='alert']"),
            r => Normalizar(r.TextContent).Contains(TextosErrores.SinRespuesta, StringComparison.Ordinal)));
        Assert.EndsWith($"/usuarios/nuevo{QueryListado}", contexto.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);

        // Un listado posterior no anuncia un alta que la API no confirmó.
        var listado = contexto.Render<ListadoUsuarios>();
        Assert.DoesNotContain("guardado", listado.Find("[aria-live='polite']").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Abre el listado con <see cref="QueryListado"/>, pulsa "Nuevo usuario", comprueba que la navegación lleva esa query
    /// y renderiza la página de registro de esa ruta (bUnit no tiene enrutador).
    /// </summary>
    private IRenderedComponent<NuevoUsuario> EntrarDesdeElListado()
    {
        var navegacion = contexto.Services.GetRequiredService<NavigationManager>();
        navegacion.NavigateTo($"/usuarios{QueryListado}");
        var listado = contexto.Render<ListadoUsuarios>();
        listado.WaitForAssertion(() => Assert.Single(listado.FindAll("button"), b => Normalizar(b.TextContent) == "Nuevo usuario").Click());
        Assert.EndsWith($"/usuarios/nuevo{QueryListado}", navegacion.Uri, StringComparison.Ordinal);

        var corte = contexto.Render<NuevoUsuario>();
        corte.WaitForAssertion(() => Assert.NotEmpty(Campo(corte, "Rol").QuerySelectorAll("option")));
        return corte;
    }

    private static void Rellenar(IRenderedComponent<NuevoUsuario> corte, string nombre, string identificador, Guid rol)
    {
        Campo(corte, "Nombre completo").Change(nombre);
        Campo(corte, "Identificador de acceso").Change(identificador);
        Campo(corte, "Rol").Change(rol.ToString());
    }

    private static IElement Etiqueta(IRenderedComponent<NuevoUsuario> corte, string campo) =>
        Assert.Single(corte.FindAll("label"), l => Normalizar(l.TextContent).StartsWith(campo, StringComparison.Ordinal));

    private static IElement Campo(IRenderedComponent<NuevoUsuario> corte, string campo) =>
        corte.Find($"[id='{Etiqueta(corte, campo).GetAttribute("for")}']");

    private static string Normalizar(string texto) => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
