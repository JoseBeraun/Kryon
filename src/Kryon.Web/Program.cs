using Kryon.Web;
using Kryon.Web.Usuarios;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = urlBaseApi });
builder.Services.AddScoped<ClienteUsuarios>();

await builder.Build().RunAsync();
