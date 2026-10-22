using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Contracts.Usuarios;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// Coherencia entre lo que la API anuncia y lo que permite (SC-005, US5-1, FR-007): para las 16 combinaciones de las
/// capacidades de registrar, editar, activar y desactivar (con acceso siempre presente), <c>acciones</c> y
/// <c>puedeRegistrar</c> del listado y del detalle coinciden con lo que cada endpoint deja ejecutar.
/// </summary>
/// <remarks>
/// <para>
/// "Deja ejecutar" se mide solo como autorización: con la bandera en <c>false</c> el endpoint responde 403; con la
/// bandera en <c>true</c>, cualquier respuesta distinta de 403 (200, 201, 409...) demuestra que la política dejó pasar.
/// No se confunde la autorización con el éxito funcional.
/// </para>
/// <para>
/// Sobre <see cref="ApiEnMemoria"/>, sin Docker, con el gate <c>RegistroUsuarios</c> abierto. Objetivos: carla (Activa,
/// sin gestión, no es el actor) para editar y desactivar; dario (Inactivo) para activar; ana, el actor, para la cuenta
/// propia. <c>editarIdentificador</c> es siempre <c>false</c>: el gate <c>EdicionIdentificador</c> sigue cerrado
/// (T079/T080).
/// </para>
/// </remarks>
public sealed class CoherenciaAccionesTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Las 16 combinaciones de registrar, editar, activar y desactivar.</summary>
    public static TheoryData<bool, bool, bool, bool> Combinaciones()
    {
        var datos = new TheoryData<bool, bool, bool, bool>();
        for (var i = 0; i < 16; i++)
        {
            datos.Add((i & 8) != 0, (i & 4) != 0, (i & 2) != 0, (i & 1) != 0);
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Combinaciones))]
    public async Task AccionesYPuedeRegistrar_CoincidenConLoQueLaApiPermite(bool registrar, bool editar, bool activar, bool desactivar)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ApiEnMemoria();
        var capacidades = new List<string> { SemillaDatos.Administrar };
        if (registrar)
        {
            capacidades.Add(SemillaDatos.Registrar);
        }

        if (editar)
        {
            capacidades.Add(SemillaDatos.Editar);
        }

        if (activar)
        {
            capacidades.Add(SemillaDatos.Activar);
        }

        if (desactivar)
        {
            capacidades.Add(SemillaDatos.Desactivar);
        }

        var cliente = api.ClienteComo(SemillaDatos.Ana, capacidades);

        // Lo que se anuncia: listado y detalle dicen lo mismo.
        var pagina = (await cliente.GetFromJsonAsync<PaginaUsuarios>("/api/usuarios", Json, ct))!;
        Assert.Equal(registrar, pagina.PuedeRegistrar);
        var carla = Fila(pagina, SemillaDatos.Carla.Id);
        var dario = Fila(pagina, SemillaDatos.Dario.Id);
        var propia = Fila(pagina, SemillaDatos.Ana.Id);
        Assert.Equal(carla.Acciones, (await DetalleAsync(cliente, SemillaDatos.Carla.Id, ct)).Acciones);
        Assert.Equal(dario.Acciones, (await DetalleAsync(cliente, SemillaDatos.Dario.Id, ct)).Acciones);

        Assert.All(new[] { carla, dario, propia }, u =>
        {
            Assert.True(u.Acciones.VerDetalle);
            Assert.Equal(editar, u.Acciones.Editar);
            Assert.False(u.Acciones.EditarIdentificador);
        });
        Assert.Equal(editar, carla.Acciones.CambiarRol);
        Assert.Equal(desactivar, carla.Acciones.Desactivar);
        Assert.False(carla.Acciones.Activar);
        Assert.Equal(activar, dario.Acciones.Activar);
        Assert.False(dario.Acciones.Desactivar);

        // Cuenta propia: nunca cambiar el propio rol ni desactivarse, aunque se tengan las capacidades (FR-034, FR-035).
        Assert.True(propia.EsCuentaPropia);
        Assert.False(propia.Acciones.CambiarRol);
        Assert.False(propia.Acciones.Desactivar);

        // Lo que se permite: cada bandera coincide con si la política deja pasar al endpoint.
        await AutorizaAsync(carla.Acciones.Editar, Editar(cliente, carla, ct));
        await AutorizaAsync(carla.Acciones.Desactivar, CambiarEstado(cliente, carla, "desactivacion", ct));
        await AutorizaAsync(dario.Acciones.Activar, CambiarEstado(cliente, dario, "activacion", ct));
        await AutorizaAsync(pagina.PuedeRegistrar, Registrar(cliente, ct));
    }

    private static async Task AutorizaAsync(bool anunciada, Task<HttpResponseMessage> envio)
    {
        var respuesta = await envio;
        if (anunciada)
        {
            Assert.NotEqual(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }
    }

    private static UsuarioResumen Fila(PaginaUsuarios pagina, Guid id) => Assert.Single(pagina.Elementos, u => u.Id == id);

    private static async Task<DetalleUsuario> DetalleAsync(HttpClient cliente, Guid id, CancellationToken ct) =>
        (await cliente.GetFromJsonAsync<DetalleUsuario>($"/api/usuarios/{id}", Json, ct))!;

    private static Task<HttpResponseMessage> Editar(HttpClient cliente, UsuarioResumen usuario, CancellationToken ct)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Put, $"/api/usuarios/{usuario.Id}")
        {
            Content = JsonContent.Create(new EditarUsuarioSolicitud { NombreCompleto = usuario.NombreCompleto, RolId = usuario.Rol!.Id }),
        };
        solicitud.Headers.TryAddWithoutValidation("If-Match", usuario.Version);
        return cliente.SendAsync(solicitud, ct);
    }

    private static Task<HttpResponseMessage> CambiarEstado(HttpClient cliente, UsuarioResumen usuario, string operacion, CancellationToken ct)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, $"/api/usuarios/{usuario.Id}/{operacion}");
        solicitud.Headers.TryAddWithoutValidation("If-Match", usuario.Version);
        return cliente.SendAsync(solicitud, ct);
    }

    private static Task<HttpResponseMessage> Registrar(HttpClient cliente, CancellationToken ct) =>
        cliente.PostAsJsonAsync(
            "/api/usuarios",
            new RegistrarUsuarioSolicitud { NombreCompleto = "Elena Soto", IdentificadorAcceso = "elena.soto", RolId = SemillaDatos.RolSinGestionA.Id },
            ct);
}
