using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Microsoft.Playwright;

namespace Kryon.E2E.Tests.Infraestructura;

/// <summary>
/// Entorno E2E de la interfaz (T101): sobre el entorno de la API (<see cref="EntornoApiE2E"/>), levanta
/// <c>Kryon.Web</c> (Blazor WebAssembly standalone) como <b>proceso real</b> y abre un navegador de Playwright con un
/// <see cref="IBrowserContext"/> que se identifica como <see cref="DatasetE2E.Ana"/> (empresa A, todas las capacidades
/// de gestión) solo mediante <see cref="IBrowserContext.SetExtraHTTPHeadersAsync"/> con los encabezados
/// <c>X-Kryon-Prueba-*</c>. No hay ningún mecanismo de autenticación real.
/// </summary>
/// <remarks>
/// <para>
/// Entorno del cliente: en .NET 10 el entorno de una aplicación WebAssembly standalone se fija al compilar, así que se
/// usa un build propio de E2E, en una configuración aparte (<see cref="ConfiguracionE2E"/>, con su <c>bin</c> y su
/// <c>obj</c>) generado con <c>-p:WasmApplicationEnvironmentName=E2E</c> (ver <see cref="ComandoCompilacion"/>). No es
/// Development: no registra <c>IdentidadPruebaHandler</c> (los encabezados los pone solo el navegador) y no lee el
/// <c>appsettings.Development.json</c> local del desarrollador, sino <c>appsettings.E2E.json</c>.
/// </para>
/// <para>
/// URL de la API: <c>appsettings.E2E.json</c> lleva un marcador no utilizable. El contexto intercepta su descarga y
/// responde con el mismo JSON cambiando solo <c>Api:UrlBase</c> por <see cref="EntornoApiE2E.BaseAddress"/>; no se
/// escribe nada en el repositorio. Si el navegador pidiera <c>appsettings.Development.json</c> (un build que no es el
/// de E2E), la solicitud se rechaza y queda registrada en <see cref="ConfiguracionesSolicitadas"/>.
/// </para>
/// <para>
/// Puerto: la política CORS de pruebas de la API (T010) admite solo los orígenes de
/// <c>src/Kryon.Web/Properties/launchSettings.json</c>, así que la interfaz escucha en
/// <see cref="OrigenPermitidoPorCors"/>. Si ese puerto está ocupado, la preparación falla de inmediato sin tocar el
/// proceso que lo ocupa.
/// </para>
/// </remarks>
public sealed class EntornoWebE2E : IAsyncLifetime
{
    /// <summary>Origen http de <c>launchSettings.json</c> de Kryon.Web, permitido por la política CORS de pruebas (T010).</summary>
    public const string OrigenPermitidoPorCors = "http://localhost:5106";

    /// <summary>Configuración de compilación del build E2E de la interfaz.</summary>
    public const string ConfiguracionE2E = "E2E";

    /// <summary>Comando que genera el build E2E de la interfaz (lo ejecuta el CI antes de las pruebas).</summary>
    public const string ComandoCompilacion =
        "dotnet build src/Kryon.Web/Kryon.Web.csproj --configuration E2E -p:WasmApplicationEnvironmentName=E2E";

    private const string ConfiguracionDelEntorno = "/appsettings.E2E.json";
    private static readonly TimeSpan EsperaArranque = TimeSpan.FromSeconds(90);
    private static readonly Regex ArchivoConfiguracion = new(@"/appsettings(\.[^/]+)?\.json(\?.*)?$", RegexOptions.Compiled);

    private readonly ConcurrentQueue<string> configuracionesSolicitadas = new();
    private ProcesoE2E? web;
    private IPlaywright? playwright;
    private IBrowser? navegador;

    /// <summary>Entorno de la API al que apunta la interfaz; este entorno lo crea y lo libera.</summary>
    public EntornoApiE2E Api { get; } = new();

    /// <summary>URL base de la interfaz real (con barra final).</summary>
    public Uri BaseAddress { get; } = new($"{OrigenPermitidoPorCors}/");

    /// <summary>Contexto del navegador identificado como ana; las pruebas abren sus páginas con <see cref="NuevaPaginaAsync"/>.</summary>
    public IBrowserContext Contexto { get; private set; } = null!;

    /// <summary>Rutas de los archivos de configuración que pidió el navegador, en orden.</summary>
    public IReadOnlyCollection<string> ConfiguracionesSolicitadas => configuracionesSolicitadas;

    public async ValueTask InitializeAsync()
    {
        using var cancelacion = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = cancelacion.Token;

        // Si otro proceso ya escucha en el origen permitido, el sondeo lo confundiría con la interfaz. Se comprueba antes
        // de preparar nada para fallar rápido.
        ExigirPuertoLibre(BaseAddress.Port);
        Rutas.SalidaCompilada("src/Kryon.Web", "Kryon.Web.dll", ConfiguracionE2E, ComandoCompilacion);

        await Api.InitializeAsync();

        // El entorno del cliente ya viene en el build E2E. ASPNETCORE_URLS solo indica dónde escucha el servidor de
        // desarrollo que sirve los archivos estáticos.
        web = ProcesoE2E.Iniciar(
            "Kryon.Web",
            "dotnet",
            [
                "run",
                "--project", Path.Combine(Rutas.Raiz, "src", "Kryon.Web", "Kryon.Web.csproj"),
                "--no-build",
                "--configuration", ConfiguracionE2E,
                "--no-launch-profile",
            ],
            Rutas.Raiz,
            new Dictionary<string, string> { ["ASPNETCORE_URLS"] = OrigenPermitidoPorCors });
        await web.EsperarDisponibleAsync(BaseAddress, EsperaArranque, ct);

        playwright = await Playwright.CreateAsync();
        navegador = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        Contexto = await navegador.NewContextAsync(new() { BaseURL = BaseAddress.ToString() });

        // Identidad de las pruebas E2E: solo los encabezados de IdentidadPrueba, en cada solicitud del navegador.
        await Contexto.SetExtraHTTPHeadersAsync(DatasetE2E.Ana.EncabezadosIdentidad);
        await Contexto.RouteAsync(ArchivoConfiguracion, ResponderConfiguracionAsync);
    }

    /// <summary>Página nueva del contexto identificado como ana.</summary>
    public Task<IPage> NuevaPaginaAsync() => Contexto.NewPageAsync();

    public async ValueTask DisposeAsync()
    {
        if (Contexto is not null)
        {
            await Contexto.DisposeAsync();
        }

        if (navegador is not null)
        {
            await navegador.DisposeAsync();
        }

        playwright?.Dispose();

        if (web is not null)
        {
            await web.DisposeAsync();
        }

        await Api.DisposeAsync();
    }

    private static void ExigirPuertoLibre(int puerto)
    {
        try
        {
            using var escucha = new TcpListener(IPAddress.Loopback, puerto);
            escucha.Start();
        }
        catch (SocketException e)
        {
            throw new InvalidOperationException($"El puerto {puerto} requerido por el entorno E2E de Kryon.Web está ocupado.", e);
        }
    }

    /// <summary>
    /// <c>appsettings.E2E.json</c>: el mismo JSON que sirve la interfaz, con <c>Api:UrlBase</c> = la API de este entorno.
    /// Cualquier otro archivo de configuración se rechaza: el build de E2E no debe pedirlo.
    /// </summary>
    private async Task ResponderConfiguracionAsync(IRoute ruta)
    {
        var archivo = new Uri(ruta.Request.Url).AbsolutePath;
        configuracionesSolicitadas.Enqueue(archivo);
        if (!archivo.EndsWith(ConfiguracionDelEntorno, StringComparison.Ordinal))
        {
            await ruta.AbortAsync();
            return;
        }

        var original = await ruta.FetchAsync();
        var configuracion = JsonNode.Parse(await original.TextAsync())
            ?? throw new InvalidOperationException($"{archivo} no es un JSON válido.");
        var api = configuracion["Api"] as JsonObject
            ?? throw new InvalidOperationException($"{archivo} no tiene la sección Api.");
        api["UrlBase"] = Api.BaseAddress.ToString();
        await ruta.FulfillAsync(new() { Response = original, Body = configuracion.ToJsonString() });
    }
}
