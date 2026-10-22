using System.Net;
using System.Text.Json;

using Microsoft.Playwright;

namespace Kryon.E2E.Tests.Infraestructura;

/// <summary>
/// Comprobación de humo de la infraestructura E2E (T100, T101): la API real responde sobre el SQL Server temporal con
/// el dataset y la RLS por empresa, y la interfaz real (build E2E, sin identidad propia) carga el listado a través de la
/// API con la identidad de ana enviada solo por el contexto del navegador. No prueba ningún flujo de usuario: eso
/// corresponde a T102 a T105.
/// </summary>
public sealed class EntornosE2ETests(EntornoWebE2E entorno) : IClassFixture<EntornoWebE2E>
{
    [Fact]
    public async Task Api_SinIdentidadResponde401_YComoAnaSoloVeLaEmpresaA()
    {
        var ct = TestContext.Current.CancellationToken;
        using var cliente = new HttpClient { BaseAddress = entorno.Api.BaseAddress };

        using (var anonima = await cliente.GetAsync("api/usuarios", ct))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonima.StatusCode);
        }

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "api/usuarios?busqueda=Ana");
        foreach (var (clave, valor) in DatasetE2E.Ana.EncabezadosIdentidad)
        {
            solicitud.Headers.Add(clave, valor);
        }

        using var respuesta = await cliente.SendAsync(solicitud, ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        var nombres = cuerpo.RootElement.GetProperty("elementos").EnumerateArray().Select(e => e.GetProperty("nombreCompleto").GetString()).ToArray();
        Assert.Equal([DatasetE2E.Ana.NombreCompleto], nombres);
    }

    /// <summary>
    /// La interfaz real (build E2E, en <see cref="EntornoWebE2E.OrigenPermitidoPorCors"/>) arranca sin sección
    /// <c>IdentidadPrueba</c>: pide <c>appsettings.E2E.json</c> y nunca <c>appsettings.Development.json</c>. Sus llamadas a
    /// la API llevan los encabezados de identidad que pone el contexto de Playwright y el listado muestra datos reales de
    /// la empresa A, sin los de B.
    /// </summary>
    [Fact]
    public async Task Interfaz_ArrancaEnE2E_YCargaElListadoConLaIdentidadDelNavegador()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        var llamadasApi = new System.Collections.Concurrent.ConcurrentQueue<IRequest>();
        pagina.Request += (_, solicitud) =>
        {
            if (solicitud.Url.StartsWith(entorno.Api.BaseAddress.ToString(), StringComparison.Ordinal))
            {
                llamadasApi.Enqueue(solicitud);
            }
        };

        try
        {
            await pagina.GotoAsync("usuarios");

            await Assertions.Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Usuarios", Level = 1 })).ToBeVisibleAsync();
            var tabla = pagina.GetByRole(AriaRole.Table);
            await Assertions.Expect(tabla).ToContainTextAsync(DatasetE2E.Carla.IdentificadorAcceso);
            await Assertions.Expect(tabla).Not.ToContainTextAsync(DatasetE2E.Berta.IdentificadorAcceso);

            // El cliente arrancó en E2E: solo pidió su configuración, nunca la de Development.
            Assert.Contains(entorno.ConfiguracionesSolicitadas, c => c.EndsWith("/appsettings.E2E.json", StringComparison.Ordinal));
            Assert.DoesNotContain(entorno.ConfiguracionesSolicitadas, c => c.EndsWith("/appsettings.Development.json", StringComparison.Ordinal));

            // La identidad viaja en los encabezados del contexto del navegador, con los valores de ana.
            var listado = Assert.Single(llamadasApi, s => s.Method == "GET" && new Uri(s.Url).AbsolutePath == "/api/usuarios");
            var encabezados = await listado.AllHeadersAsync();
            foreach (var (clave, valor) in DatasetE2E.Ana.EncabezadosIdentidad)
            {
                Assert.Equal(valor, encabezados[clave.ToLowerInvariant()]);
            }

            Assert.Equal(200, (await listado.ResponseAsync())!.Status);
        }
        finally
        {
            await pagina.CloseAsync();
        }
    }
}
