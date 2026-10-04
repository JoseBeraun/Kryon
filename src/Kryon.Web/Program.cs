using Kryon.Web;
using Kryon.Web.Desarrollo;
using Kryon.Web.Usuarios;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;

// URL base de la API, solo desde configuración (wwwroot/appsettings*.json). Sin valor por defecto.
const string ClaveUrlBaseApi = "Api:UrlBase";

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var valorUrlBaseApi = builder.Configuration[ClaveUrlBaseApi];
if (!Uri.TryCreate(valorUrlBaseApi, UriKind.Absolute, out var urlBaseApi)
    || (urlBaseApi.Scheme != Uri.UriSchemeHttps && urlBaseApi.Scheme != Uri.UriSchemeHttp))
{
    throw new InvalidOperationException(
        $"Falta '{ClaveUrlBaseApi}' en la configuración de Kryon.Web o no es una URL http(s) absoluta.");
}

// Las rutas del cliente son relativas ("api/usuarios"): la URL base debe terminar en '/'.
if (!urlBaseApi.AbsolutePath.EndsWith('/'))
{
    urlBaseApi = new Uri(urlBaseApi.AbsoluteUri + "/");
}

RegistrarHttpClientApi(builder.Services, urlBaseApi, builder.HostEnvironment, builder.Configuration);
builder.Services.AddScoped<ClienteUsuarios>();

await builder.Build().RunAsync();

public partial class Program
{
    /// <summary>
    /// HttpClient de la API. Solo en Development lleva delante <see cref="IdentidadPruebaHandler"/>; en cualquier otro
    /// entorno es un HttpClient normal y el handler no existe en la cadena.
    /// </summary>
    internal static void RegistrarHttpClientApi(
        IServiceCollection servicios,
        Uri urlBaseApi,
        IWebAssemblyHostEnvironment entorno,
        IConfiguration configuracion)
    {
        if (entorno.IsDevelopment())
        {
            // Se valida al arrancar: sin identidad de Development configurada, la aplicación no arranca.
            IdentidadPruebaHandler.DesdeConfiguracion(configuracion).Dispose();
            servicios.AddScoped(sp =>
            {
                var identidadPrueba = IdentidadPruebaHandler.DesdeConfiguracion(configuracion);
                identidadPrueba.InnerHandler = new HttpClientHandler();
                return new HttpClient(identidadPrueba) { BaseAddress = urlBaseApi };
            });
        }
        else
        {
            servicios.AddScoped(sp => new HttpClient { BaseAddress = urlBaseApi });
        }
    }
}
