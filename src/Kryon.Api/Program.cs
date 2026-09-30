using Kryon.Api.Seguridad;

using Microsoft.Net.Http.Headers;

const string PoliticaCorsKryonWebLocal = "KryonWebLocal";

var builder = WebApplication.CreateBuilder(args);

// CORS solo para Development y Test: permite que Kryon.Web local envíe los encabezados de IdentidadPrueba.
// Fuera de esos entornos la política no existe.
var corsKryonWebLocal = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test");
if (corsKryonWebLocal)
{
    builder.Services.AddCors(opciones =>
    {
        opciones.AddPolicy(PoliticaCorsKryonWebLocal, politica =>
        {
            // Orígenes de src/Kryon.Web/Properties/launchSettings.json (perfiles https y http).
            politica.WithOrigins("https://localhost:7288", "http://localhost:5106");

            // Métodos que usa contracts/usuarios-api.openapi.yaml.
            politica.WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put);

            // Content-Type (application/json) e If-Match (concurrencia) no son encabezados simples de CORS.
            politica.WithHeaders(
                IdentidadPrueba.EncabezadoUsuarioId,
                IdentidadPrueba.EncabezadoEmpresaId,
                IdentidadPrueba.EncabezadoCapacidades,
                HeaderNames.ContentType,
                HeaderNames.IfMatch);
        });
    });
}

var app = builder.Build();

app.UseHttpsRedirection();

if (corsKryonWebLocal)
{
    app.UseCors(PoliticaCorsKryonWebLocal);
}

app.Run();
