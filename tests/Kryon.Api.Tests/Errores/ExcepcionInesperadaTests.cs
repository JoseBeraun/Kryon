using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Api.Tests.Errores;

/// <summary>
/// Excepción no controlada sobre el pipeline real de la API (T030): <c>500</c> con el ProblemDetails genérico y sin
/// ningún detalle técnico (FR-041, SC-004). No necesita base de datos: la cadena de conexión es ficticia y nunca se
/// abre.
/// </summary>
public sealed class ExcepcionInesperadaTests
{
    private const string RutaFallo = "/prueba/errores/excepcion";

    private const string ConsultaSql = "SELECT * FROM dbo.Usuarios WHERE IdentificadorAcceso = 'marcador-sql'";
    private const string RutaServidor = @"C:\kryon\servidor\UsuariosRepositorio.cs";
    private const string TipoInterno = "Kryon.Infrastructure.Persistencia.KryonDbContext";

    private static readonly string MensajeOriginal =
        $"Fallo deliberado de la prueba: {ConsultaSql} en {RutaServidor} desde {TipoInterno}.";

    [Theory]
    [InlineData("Test")]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ExcepcionNoControlada_Responde500Generico_SinDetallesInternos(string entorno)
    {
        await using var fabrica = Fabrica(entorno);
        var ct = TestContext.Current.CancellationToken;

        var respuesta = await fabrica.CreateClient().GetAsync(RutaFallo, ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(cuerpo);
        var raiz = json.RootElement;
        Assert.Equal(["type", "title", "status", "codigo"], raiz.EnumerateObject().Select(p => p.Name));
        Assert.Equal("https://kryon.app/errores/error-interno", raiz.GetProperty("type").GetString());
        Assert.Equal("Error interno", raiz.GetProperty("title").GetString());
        Assert.Equal(500, raiz.GetProperty("status").GetInt32());
        Assert.Equal("error-interno", raiz.GetProperty("codigo").GetString());

        string[] prohibidos =
        [
            MensajeOriginal, ConsultaSql, "SELECT", "dbo.Usuarios", "marcador-sql", RutaServidor, @"C:\", "servidor",
            "UsuariosRepositorio", ".cs", TipoInterno, "KryonDbContext", "Kryon.Infrastructure", "Persistencia",
            "Fallo deliberado", nameof(InvalidOperationException), "System.", "Exception", "at ", "StackTrace",
            "ExcepcionInesperadaTests", "EndpointsDeSondeo", "line ",
        ];
        Assert.All(prohibidos, prohibido => Assert.DoesNotContain(prohibido, cuerpo, StringComparison.OrdinalIgnoreCase));
    }

    private static WebApplicationFactory<Program> Fabrica(string entorno) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(entorno);
            builder.UseSetting("ConnectionStrings:Kryon", "Server=no-se-usa;Database=no-se-usa");
            builder.ConfigureTestServices(servicios => servicios.AddSingleton<IStartupFilter, EndpointsDeSondeo>());
        });

    /// <summary>
    /// Endpoint de sondeo, solo en el ensamblado de pruebas, después del pipeline de <c>Program.cs</c>. Es anónimo
    /// porque la prueba es del manejo de excepciones, no de la autenticación.
    /// </summary>
    private sealed class EndpointsDeSondeo : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
                endpoints.MapGet(RutaFallo, IResult () => throw new InvalidOperationException(MensajeOriginal))
                    .AllowAnonymous());
        };
    }
}
