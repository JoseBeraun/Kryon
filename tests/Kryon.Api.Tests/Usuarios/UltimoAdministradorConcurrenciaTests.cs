using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Contracts.Usuarios;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// Concurrencia de la regla del último administrador (FR-036, SC-006, quickstart V7): con <c>ana</c> y <c>beto</c> como
/// únicos administradores activos de A, las desactivaciones cruzadas lanzadas en paralelo dejan siempre exactamente un
/// 200 y un 409 <c>ultimo-administrador</c>, y al menos un administrador activo.
/// </summary>
/// <remarks>
/// <c>beto</c> tiene el rol "Sin desactivar" de la semilla (usado por las pruebas de acciones visibles, V6), que conserva
/// <c>usuarios.administrar</c>: es administrador, pero su rol no incluye <c>usuarios.desactivar</c>. Para que su
/// desactivación compita con la de ana, su identidad de prueba recibe esa capacidad; el recuento de administradores
/// sigue saliendo de los roles guardados. Entre repeticiones se reactiva a quien quedó Inactivo.
/// </remarks>
public sealed class UltimoAdministradorConcurrenciaTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    private const int Repeticiones = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DesactivacionesCruzadasEnParalelo_SiempreUnaGanaYOtraEsRechazada()
    {
        var ct = TestContext.Current.CancellationToken;
        var capacidades = new[] { SemillaDatos.Administrar, SemillaDatos.Desactivar, SemillaDatos.Activar };
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad, capacidades);
        var beto = fabrica.ClienteComo(SemillaDatos.Beto.Identidad, capacidades);

        for (var repeticion = 1; repeticion <= Repeticiones; repeticion++)
        {
            var pagina = await ListarAsync(ana, ct);
            var versionAna = Version(pagina, SemillaDatos.Ana.Id);
            var versionBeto = Version(pagina, SemillaDatos.Beto.Id);

            var respuestas = await Task.WhenAll(
                DesactivarAsync(ana, SemillaDatos.Beto.Id, versionBeto, ct),
                DesactivarAsync(beto, SemillaDatos.Ana.Id, versionAna, ct));

            var estados = respuestas.Select(r => r.StatusCode).ToArray();
            Assert.True(
                estados.Count(e => e == HttpStatusCode.OK) == 1 && estados.Count(e => e == HttpStatusCode.Conflict) == 1,
                $"Repetición {repeticion}: se esperaba un 200 y un 409, pero fue [{string.Join(", ", estados)}].");
            var rechazada = respuestas.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            var problema = JsonSerializer.Deserialize<Problema>(await rechazada.Content.ReadAsStringAsync(ct), Json)!;
            Assert.Equal(CodigosError.UltimoAdministrador, problema.Codigo);

            // Queda al menos un administrador activo: exactamente uno de los dos sigue Activo.
            var despues = await ListarAsync(ana, ct);
            var activos = new[] { SemillaDatos.Ana.Id, SemillaDatos.Beto.Id }.Count(id => Estado(despues, id) == "activo");
            Assert.True(activos == 1, $"Repetición {repeticion}: quedaron {activos} administradores activos de 2.");

            // Se reactiva al desactivado con el otro administrador, que sigue activo.
            var desactivado = Estado(despues, SemillaDatos.Ana.Id) == "inactivo" ? SemillaDatos.Ana.Id : SemillaDatos.Beto.Id;
            var reactivador = desactivado == SemillaDatos.Ana.Id ? beto : ana;
            var reactivacion = await ActivarAsync(reactivador, desactivado, Version(despues, desactivado), ct);
            Assert.True(reactivacion.StatusCode == HttpStatusCode.OK, $"Repetición {repeticion}: no se pudo reactivar ({reactivacion.StatusCode}).");
        }
    }

    private static async Task<PaginaUsuarios> ListarAsync(HttpClient cliente, CancellationToken ct)
    {
        var respuesta = await cliente.GetAsync("/api/usuarios", ct);
        return JsonSerializer.Deserialize<PaginaUsuarios>(await respuesta.Content.ReadAsStringAsync(ct), Json)!;
    }

    private static string Version(PaginaUsuarios pagina, Guid id) => pagina.Elementos.Single(u => u.Id == id).Version;

    private static string Estado(PaginaUsuarios pagina, Guid id) => pagina.Elementos.Single(u => u.Id == id).Estado;

    private static Task<HttpResponseMessage> DesactivarAsync(HttpClient cliente, Guid id, string version, CancellationToken ct) =>
        Enviar(cliente, $"/api/usuarios/{id}/desactivacion", version, ct);

    private static Task<HttpResponseMessage> ActivarAsync(HttpClient cliente, Guid id, string version, CancellationToken ct) =>
        Enviar(cliente, $"/api/usuarios/{id}/activacion", version, ct);

    private static Task<HttpResponseMessage> Enviar(HttpClient cliente, string ruta, string version, CancellationToken ct)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
        solicitud.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(version));
        return cliente.SendAsync(solicitud, ct);
    }
}
