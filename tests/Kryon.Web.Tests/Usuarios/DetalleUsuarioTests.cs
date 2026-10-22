using System.Net;

using AngleSharp.Dom;

using Bunit;

using Kryon.Contracts.Usuarios;
using Kryon.Web.Tests.Infraestructura;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using PaginaDetalle = Kryon.Web.Usuarios.Detalle.DetalleUsuario;

namespace Kryon.Web.Tests.Usuarios;

/// <summary>
/// Detalle de usuario (contracts/ui-usuarios.md → <i>Detalle</i>; US3-2, EC-1, EC-4) con el <c>ClienteUsuarios</c> real
/// sobre el HTTP falso de <see cref="ContextoPruebaWeb"/>: campos de solo lectura, marca "Tú", acciones según las
/// banderas de la API, "Volver al listado" con la query string del listado y "Usuario no encontrado".
/// </summary>
public sealed class DetalleUsuarioTests : IAsyncDisposable
{
    private const string QueryListado = "?busqueda=ana&estado=activo&pagina=2";

    private readonly ContextoPruebaWeb contexto = new();

    public DetalleUsuarioTests()
    {
        // El diálogo de desactivación puede usar JavaScript (showModal); estas pruebas no fijan esas llamadas.
        contexto.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public ValueTask DisposeAsync() => contexto.DisposeAsync();

    [Fact]
    public void Muestra_LosCamposEnSoloLectura()
    {
        var bruno = Usuario("Bruno Díaz", "bruno.diaz", "activo", propia: false, Acciones(editar: true));

        var corte = Renderizar(bruno);

        var campos = Campos(corte);
        Assert.Equal("Bruno Díaz", campos["Nombre completo"]);
        Assert.Equal("bruno.diaz", campos["Identificador de acceso"]);
        Assert.Equal("Ventas", campos["Rol"]);
        Assert.Equal("Activo", campos["Estado"]);
        Assert.Empty(corte.FindAll("input, select, textarea, form"));
    }

    [Fact]
    public void RolNulo_SeMuestraComoSinRolAsignado()
    {
        var corte = Renderizar(Usuario("Diego Sol", "diego.sol", "inactivo", propia: false, Acciones(), sinRol: true));

        Assert.Equal("Sin rol asignado", Campos(corte)["Rol"]);
        Assert.Equal("Inactivo", Campos(corte)["Estado"]);
    }

    [Theory]
    [InlineData(true, "Tú")]
    [InlineData(false, null)]
    public void CuentaPropia_MuestraLaMarcaTu(bool propia, string? marca)
    {
        var corte = Renderizar(Usuario("Ana Pérez", "ana.perez", "activo", propia, Acciones()));

        var titulo = Normalizar(corte.Find("h1").TextContent);
        if (marca is null)
        {
            Assert.DoesNotContain("Tú", corte.Markup, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal($"Ana Pérez {marca}", titulo);
        }
    }

    /// <summary>Cada acción aparece solo si su bandera es verdadera; la interfaz no la deduce del estado (FR-007).</summary>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public void Acciones_SoloSegunLasBanderasDeLaApi(bool editar, bool activar, bool desactivar)
    {
        var corte = Renderizar(Usuario("Carmen Ruiz", "carmen.ruiz", "activo", propia: false, Acciones(editar, activar, desactivar)));

        var nombres = Controles(corte).Select(NombreAccesible).ToArray();
        Assert.Equal(editar, nombres.Contains("Editar a Carmen Ruiz"));
        Assert.Equal(activar, nombres.Contains("Activar a Carmen Ruiz"));
        Assert.Equal(desactivar, nombres.Contains("Desactivar a Carmen Ruiz"));
    }

    [Fact]
    public void VolverAlListado_ConservaLaQueryStringDelListado()
    {
        var corte = Renderizar(Usuario("Bruno Díaz", "bruno.diaz", "activo", propia: false, Acciones()));

        var volver = Assert.Single(Controles(corte), c => NombreAccesible(c) == "Volver al listado");
        Assert.Equal($"usuarios{QueryListado}", volver.GetAttribute("href"));
    }

    [Fact]
    public void UsuarioNoEncontrado_MuestraElMensaje_YElEnlaceAlListado()
    {
        var id = Guid.Parse("f0000000-0000-0000-0000-0000000000a3");
        contexto.Http.Responder = (_, _) => HttpFalso.ConProblema(new Problema
        {
            Type = "https://kryon.app/errores/usuario-no-encontrado",
            Title = "Usuario no encontrado",
            Status = 404,
            Codigo = CodigosError.UsuarioNoEncontrado,
        });
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo($"/usuarios/{id}{QueryListado}");

        var corte = contexto.Render<PaginaDetalle>(p => p.Add(d => d.Id, id));

        corte.WaitForAssertion(() => Assert.Contains("Usuario no encontrado", Normalizar(corte.Markup), StringComparison.Ordinal));
        var volver = Assert.Single(Controles(corte), c => NombreAccesible(c) == "Volver al listado");
        Assert.Equal($"usuarios{QueryListado}", volver.GetAttribute("href"));
        Assert.Empty(corte.FindAll("dl"));
    }

    /// <summary>Renderiza el detalle de <paramref name="usuario"/> llegando desde el listado con <see cref="QueryListado"/>.</summary>
    private IRenderedComponent<PaginaDetalle> Renderizar(UsuarioResumen usuario)
    {
        contexto.Http.Responder = (solicitud, _) => solicitud.RequestUri!.AbsolutePath == $"/api/usuarios/{usuario.Id}"
            ? HttpFalso.Json(HttpStatusCode.OK, usuario, usuario.Version)
            : throw new InvalidOperationException($"Solicitud no prevista en T070: {solicitud.Method} {solicitud.RequestUri}");
        contexto.Services.GetRequiredService<NavigationManager>().NavigateTo($"/usuarios/{usuario.Id}{QueryListado}");

        var corte = contexto.Render<PaginaDetalle>(p => p.Add(d => d.Id, usuario.Id));
        corte.WaitForElement("dl");
        return corte;
    }

    /// <summary>Pares etiqueta → valor de la lista de definición del detalle.</summary>
    private static Dictionary<string, string> Campos(IRenderedComponent<PaginaDetalle> corte) =>
        corte.FindAll("dt").ToDictionary(dt => Normalizar(dt.TextContent), dt => Normalizar(dt.NextElementSibling!.TextContent));

    private static IEnumerable<IElement> Controles(IRenderedComponent<PaginaDetalle> corte) => corte.FindAll("button, a");

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

    private static readonly RolResumen Ventas = new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nombre = "Ventas" };

    private static DetalleUsuario Usuario(string nombre, string identificador, string estado, bool propia, AccionesPermitidas acciones, bool sinRol = false) => new()
    {
        Id = Guid.Parse("20000000-0000-0000-0000-000000000009"),
        NombreCompleto = nombre,
        IdentificadorAcceso = identificador,
        Rol = sinRol ? null : Ventas,
        Estado = estado,
        EsCuentaPropia = propia,
        Version = "\"AAAAAAAAB9E=\"",
        Acciones = acciones,
    };

    private static AccionesPermitidas Acciones(bool editar = false, bool activar = false, bool desactivar = false) => new()
    {
        VerDetalle = true,
        Editar = editar,
        CambiarRol = editar,
        EditarIdentificador = false,
        Activar = activar,
        Desactivar = desactivar,
    };
}
