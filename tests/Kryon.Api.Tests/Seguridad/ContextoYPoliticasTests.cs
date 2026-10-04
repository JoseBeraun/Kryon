using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

using Kryon.Api.Seguridad;
using Kryon.Api.Tests.Infraestructura;
using Kryon.Core.Seguridad;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Api.Tests.Seguridad;

/// <summary>
/// Contexto de solicitud y políticas de autorización (T007, T008, T009) sobre la API real. No necesita base de
/// datos: los endpoints de sondeo solo leen <see cref="IContextoSolicitud"/>, así que no se usa
/// <c>KryonApiFactory</c> ni Docker; la cadena de conexión es ficticia y nunca se abre.
/// </summary>
public sealed class ContextoYPoliticasTests
{
    private const string RutaContexto = "/prueba/seguridad/contexto";
    private const string RutaRegistrar = "/prueba/seguridad/registrar";

    private static readonly UsuarioSemilla Ana = SemillaDatos.Ana.Identidad;

    [Fact]
    public async Task IdentidadDePrueba_SinEmpresaId_Responde401()
    {
        await using var fabrica = Fabrica("Test");
        var cliente = fabrica.ClienteComo(Ana);
        cliente.DefaultRequestHeaders.Remove(IdentidadPrueba.EncabezadoEmpresaId);

        var respuesta = await cliente.GetAsync(RutaContexto, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public void ContextoDesdeClaims_SinClaimDeEmpresa_NoSeConstruye()
    {
        var identidad = new ClaimsIdentity(
            [new Claim(ClaimsContextoSolicitud.UsuarioId, Ana.UsuarioId.ToString())],
            authenticationType: "prueba");

        Assert.False(ContextoSolicitudDesdeClaims.TryCrear(new ClaimsPrincipal(identidad), out var contexto));
        Assert.Null(contexto);
    }

    [Fact]
    public async Task PoliticaSinAsociacionConfigurada_Responde403AunqueElActorTengaElPermiso()
    {
        await using var fabrica = Fabrica("Test", ($"{PoliticasUsuarios.SeccionPermisos}:{PoliticasUsuarios.Registrar}", string.Empty));
        var cliente = fabrica.ClienteComo(Ana);

        var respuesta = await cliente.GetAsync(RutaRegistrar, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task PoliticaConfigurada_PermiteConLaCapacidad_YDeniegaSinElla()
    {
        await using var fabrica = Fabrica("Test");
        var ct = TestContext.Current.CancellationToken;

        var conCapacidad = await fabrica.ClienteComo(Ana).GetAsync(RutaRegistrar, ct);
        var sinCapacidad = await fabrica.ClienteComo(SemillaDatos.Carla.Identidad).GetAsync(RutaRegistrar, ct);

        Assert.Equal(HttpStatusCode.OK, conCapacidad.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sinCapacidad.StatusCode);
    }

    [Fact]
    public async Task EmpresaIdEnQuery_SeIgnora()
    {
        await using var fabrica = Fabrica("Test");

        var contexto = await fabrica.ClienteComo(Ana).GetFromJsonAsync<ContextoLeido>(
            $"{RutaContexto}?empresaId={SemillaDatos.EmpresaB}",
            TestContext.Current.CancellationToken);

        Assert.NotNull(contexto);
        Assert.Equal(Ana.EmpresaId, contexto.EmpresaId);
        Assert.Equal(Ana.UsuarioId, contexto.UsuarioId);
    }

    [Fact]
    public async Task EmpresaIdEnCuerpo_SeIgnora()
    {
        await using var fabrica = Fabrica("Test");
        var ct = TestContext.Current.CancellationToken;

        var respuesta = await fabrica.ClienteComo(Ana).PostAsJsonAsync(RutaContexto, new CuerpoConEmpresa(SemillaDatos.EmpresaB), ct);
        var contexto = await respuesta.Content.ReadFromJsonAsync<ContextoLeido>(ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(contexto);
        Assert.Equal(SemillaDatos.EmpresaB, contexto.EmpresaDelCuerpo);
        Assert.Equal(Ana.EmpresaId, contexto.EmpresaId);
        Assert.Equal(Ana.UsuarioId, contexto.UsuarioId);
    }

    [Fact]
    public async Task EnProduction_LosEncabezadosDePruebaSeIgnoran_YResponde401()
    {
        await using var fabrica = Fabrica("Production");

        var respuesta = await fabrica.ClienteComo(Ana).GetAsync(RutaContexto, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    private static WebApplicationFactory<Program> Fabrica(string entorno, params (string Clave, string Valor)[] configuracion) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(entorno);
            builder.UseSetting("ConnectionStrings:Kryon", "Server=no-se-usa;Database=no-se-usa");
            foreach (var (clave, valor) in configuracion)
            {
                builder.UseSetting(clave, valor);
            }

            builder.ConfigureTestServices(servicios => servicios.AddSingleton<IStartupFilter, EndpointsDeSondeo>());
        });

    private sealed record CuerpoConEmpresa(Guid EmpresaId);

    private sealed record ContextoLeido(Guid UsuarioId, Guid EmpresaId, Guid? EmpresaDelCuerpo);

    /// <summary>
    /// Endpoints de sondeo, solo en el ensamblado de pruebas, protegidos con las políticas reales de T008 y
    /// evaluados por el middleware real de autorización, después del pipeline de <c>Program.cs</c>.
    /// </summary>
    private sealed class EndpointsDeSondeo : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet(RutaContexto, (IContextoSolicitud contexto) =>
                        new ContextoLeido(contexto.UsuarioId, contexto.EmpresaId, null))
                    .RequireAuthorization(PoliticasUsuarios.Acceder);
                endpoints.MapPost(RutaContexto, (CuerpoConEmpresa cuerpo, IContextoSolicitud contexto) =>
                        new ContextoLeido(contexto.UsuarioId, contexto.EmpresaId, cuerpo.EmpresaId))
                    .RequireAuthorization(PoliticasUsuarios.Acceder);
                endpoints.MapGet(RutaRegistrar, () => TypedResults.Ok())
                    .RequireAuthorization(PoliticasUsuarios.Registrar);
            });
        };
    }
}
