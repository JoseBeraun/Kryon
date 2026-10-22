using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Api.Usuarios;
using Kryon.Contracts.Usuarios;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// Autorización de cada operación (US5-2, US5-3, EC-7, FR-004 a FR-006): en los 7 endpoints de la feature, una
/// identidad sin la capacidad de su política recibe 403 <c>sin-permiso</c> sin que se ejecute la operación, sin cambios
/// y sin datos del usuario objetivo; y un permiso retirado entre dos solicitudes deja la segunda en 403.
/// </summary>
/// <remarks>
/// Sobre <see cref="ApiEnMemoria"/>, sin Docker: la persistencia en memoria registra cada llamada, así que "no se
/// ejecutó" se comprueba como "no hubo ninguna llamada". El gate <c>RegistroUsuarios</c> está abierto (dobles de
/// DEP-2, DEP-3 y DEP-5) para que el 403 de <c>POST /api/usuarios</c> sea de autorización y no de un endpoint ausente.
/// Las peticiones con <c>If-Match</c> llevan la versión vigente, así que el único motivo de rechazo es el permiso.
/// </remarks>
public sealed class AutorizacionOperacionesTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Operaciones: nombre, capacidad que exige su política y usuario objetivo.</summary>
    public static TheoryData<string> Operaciones =>
        ["listar", "roles-asignables", "detalle", "editar", "activar", "desactivar", "registrar"];

    [Theory]
    [MemberData(nameof(Operaciones))]
    public async Task SinLaCapacidadDeSuPolitica_Responde403SinPermiso_SinEjecutarNiDevolverDatos(string operacion)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ApiEnMemoria();
        var antes = api.Almacen.Instantanea();
        var objetivo = Objetivo(operacion);
        var version = VersionEtag.Desde(api.Almacen[objetivo.Id].Version);

        // Todas las capacidades salvo la que exige la política de esta operación.
        var capacidades = SemillaDatos.TodasLasCapacidades.Where(c => c != Capacidad(operacion));
        var respuesta = await EnviarAsync(api.ClienteComo(SemillaDatos.Ana, capacidades), operacion, objetivo, version, ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        Assert.Equal(CodigosError.SinPermiso, JsonSerializer.Deserialize<Problema>(cuerpo, Json)!.Codigo);
        foreach (var dato in new[] { objetivo.Id.ToString(), objetivo.NombreCompleto, objetivo.IdentificadorAcceso, SemillaDatos.EmpresaA.ToString(), SemillaDatos.EmpresaB.ToString() })
        {
            Assert.DoesNotContain(dato, cuerpo, StringComparison.OrdinalIgnoreCase);
        }

        // Ni la consulta, ni la transacción, ni la auditoría, ni los puntos de integración: la política cortó antes.
        Assert.Empty(api.Almacen.Llamadas);
        Assert.Empty(api.Alta.AltasConsultadas);
        Assert.Empty(api.Identificador.UsosConsultados);
        Assert.Equal(antes, api.Almacen.Instantanea());
    }

    /// <summary>
    /// La autorización se evalúa en cada solicitud con las capacidades de esa solicitud (US5-3, EC-7): la misma
    /// identidad (usuario y empresa) pasa con la capacidad y, retirada, la siguiente solicitud es 403 sin ejecutarse.
    /// </summary>
    [Theory]
    [InlineData("editar")]
    [InlineData("activar")]
    [InlineData("desactivar")]
    [InlineData("registrar")]
    public async Task PermisoRetiradoEntreDosSolicitudes_LaSegundaEs403SinEjecutarse(string operacion)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ApiEnMemoria();
        var objetivo = Objetivo(operacion);

        var primera = await EnviarAsync(
            api.ClienteComo(SemillaDatos.Ana, SemillaDatos.TodasLasCapacidades),
            operacion,
            objetivo,
            VersionEtag.Desde(api.Almacen[objetivo.Id].Version),
            ct);
        Assert.True(primera.IsSuccessStatusCode, $"La primera solicitud debía ejecutarse: {(int)primera.StatusCode}.");

        var llamadas = api.Almacen.Llamadas.Count;
        var auditorias = api.Almacen.Auditorias.Count;
        var antes = api.Almacen.Instantanea();
        var sinCapacidad = SemillaDatos.TodasLasCapacidades.Where(c => c != Capacidad(operacion));

        var segunda = await EnviarAsync(
            api.ClienteComo(SemillaDatos.Ana, sinCapacidad),
            operacion,
            objetivo,
            VersionEtag.Desde(api.Almacen[objetivo.Id].Version),
            ct);

        Assert.Equal(HttpStatusCode.Forbidden, segunda.StatusCode);
        Assert.Equal(CodigosError.SinPermiso, (await segunda.Content.ReadFromJsonAsync<Problema>(Json, ct))!.Codigo);
        Assert.Equal(llamadas, api.Almacen.Llamadas.Count);
        Assert.Equal(auditorias, api.Almacen.Auditorias.Count);
        Assert.Equal(antes, api.Almacen.Instantanea());
    }

    private static string Capacidad(string operacion) => operacion switch
    {
        "listar" or "roles-asignables" or "detalle" => SemillaDatos.Administrar,
        "editar" => SemillaDatos.Editar,
        "activar" => SemillaDatos.Activar,
        "desactivar" => SemillaDatos.Desactivar,
        "registrar" => SemillaDatos.Registrar,
        _ => throw new ArgumentOutOfRangeException(nameof(operacion), operacion, null),
    };

    /// <summary>Usuario sobre el que actúa la operación: dario (Inactivo) para activar, carla (Activa) para el resto.</summary>
    private static UsuarioSembrado Objetivo(string operacion) => operacion == "activar" ? SemillaDatos.Dario : SemillaDatos.Carla;

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string operacion, UsuarioSembrado objetivo, string version, CancellationToken ct)
    {
        var solicitud = operacion switch
        {
            "listar" => new HttpRequestMessage(HttpMethod.Get, "/api/usuarios"),
            "roles-asignables" => new HttpRequestMessage(HttpMethod.Get, "/api/usuarios/roles-asignables"),
            "detalle" => new HttpRequestMessage(HttpMethod.Get, $"/api/usuarios/{objetivo.Id}"),
            "editar" => new HttpRequestMessage(HttpMethod.Put, $"/api/usuarios/{objetivo.Id}")
            {
                Content = JsonContent.Create(new EditarUsuarioSolicitud { NombreCompleto = $"{objetivo.NombreCompleto} Editada", RolId = objetivo.Rol.Id }),
            },
            "activar" => new HttpRequestMessage(HttpMethod.Post, $"/api/usuarios/{objetivo.Id}/activacion"),
            "desactivar" => new HttpRequestMessage(HttpMethod.Post, $"/api/usuarios/{objetivo.Id}/desactivacion"),
            "registrar" => new HttpRequestMessage(HttpMethod.Post, "/api/usuarios")
            {
                Content = JsonContent.Create(new RegistrarUsuarioSolicitud { NombreCompleto = "Elena Soto", IdentificadorAcceso = $"elena.{Guid.NewGuid():N}", RolId = SemillaDatos.RolSinGestionA.Id }),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(operacion), operacion, null),
        };

        if (operacion is "editar" or "activar" or "desactivar")
        {
            solicitud.Headers.TryAddWithoutValidation("If-Match", version);
        }

        return cliente.SendAsync(solicitud, ct);
    }
}
