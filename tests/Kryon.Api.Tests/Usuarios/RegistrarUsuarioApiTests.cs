using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Api.Usuarios;
using Kryon.Contracts.Usuarios;
using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;
using Kryon.Core.Usuarios.Integraciones;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// <c>POST /api/usuarios</c> sobre el pipeline real de la API (quickstart V10, V11; US4-2, US4-3, SC-002, FR-023,
/// FR-026 a FR-028, FR-043): 201 con <c>Location</c>, <c>ETag</c> y auditoría <c>Alta</c>; 400 por campo; 409
/// <c>identificador-en-uso</c>, <c>limite-usuarios-alcanzado</c> y <c>rol-no-valido</c>; 403 sin la capacidad de
/// registrar; y el gate <c>RegistroUsuarios</c>: cerrado, el endpoint no existe.
/// </summary>
/// <remarks>
/// <para>
/// <b>Solo validan la conexión</b> de la feature con sus puntos de integración externos (DEP-2, DEP-3, DEP-5) mediante
/// los dobles de <see cref="DoblesIntegraciones"/>: <b>no</b> prueban las reglas reales del estado inicial, la
/// credencial, el formato, la unicidad ni el límite, que definirán la futura spec de autenticación y la decisión
/// comercial (T090–T094). Registrar los dobles abre el gate, igual que lo harán los adaptadores reales.
/// </para>
/// <para>
/// No necesitan Docker: la persistencia y la consulta son dobles en memoria de la empresa A con los datos semilla, y la
/// cadena de conexión es ficticia y nunca se abre. La persistencia real del alta (SQL, RLS) la cubren las pruebas con
/// SQL Server de las demás historias.
/// </para>
/// </remarks>
public sealed class RegistrarUsuarioApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AltaCorrecta_Responde201ConLocationETagYDetalle_YAuditaAlta()
    {
        var ct = TestContext.Current.CancellationToken;
        var entorno = new Entorno();
        await using var fabrica = entorno.Fabrica();

        var respuesta = await RegistrarAsync(fabrica.ClienteComo(SemillaDatos.Ana.Identidad), "Elena Soto", "elena.soto", SemillaDatos.RolSinGestionA.Id, ct);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var detalle = (await respuesta.Content.ReadFromJsonAsync<DetalleUsuario>(Json, ct))!;
        var creado = Assert.Single(entorno.Almacen.Agregados);
        Assert.Equal(creado.Id, detalle.Id);
        Assert.Equal("Elena Soto", detalle.NombreCompleto);
        Assert.Equal("elena.soto", detalle.IdentificadorAcceso);
        Assert.Equal(SemillaDatos.RolSinGestionA.Id, detalle.Rol?.Id);
        Assert.Equal("inactivo", detalle.Estado);
        Assert.False(detalle.EsCuentaPropia);

        Assert.Equal($"/api/usuarios/{creado.Id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(VersionEtag.Desde(creado.Version), respuesta.Headers.ETag?.ToString());
        Assert.Equal(detalle.Version, respuesta.Headers.ETag?.ToString());

        // La empresa sale de la identidad verificada; el estado, de la política de alta (doble).
        Assert.Equal(SemillaDatos.EmpresaA, creado.EmpresaId);
        Assert.Equal(EstadoUsuario.Inactivo, creado.Estado);
        Assert.Same(creado, Assert.Single(entorno.Alta.CredencialesGestionadas));

        var registro = Assert.Single(entorno.Almacen.Auditorias);
        Assert.Equal(TipoOperacion.Alta, registro.TipoOperacion);
        Assert.Equal(SemillaDatos.Ana.Id, registro.ActorUsuarioId);
        Assert.Equal(SemillaDatos.EmpresaA, registro.EmpresaId);
        Assert.Equal(creado.Id, registro.UsuarioAfectadoId);
        Assert.Null(registro.CambiosJson);
        Assert.Equal(["Iniciar", "Agregar", "Auditar Alta", "Confirmar"], entorno.Almacen.Pasos);
    }

    [Theory]
    [InlineData("", "elena.soto", "nombreCompleto")]
    [InlineData("Elena Soto", "", "identificadorAcceso")]
    [InlineData("Elena Soto", "elena.soto", "rolId")]
    public async Task DatosInvalidos_Responden400ValidacionPorCampo_SinAlta(string nombre, string identificador, string campo)
    {
        var ct = TestContext.Current.CancellationToken;
        var entorno = new Entorno();
        await using var fabrica = entorno.Fabrica();
        var rol = campo == "rolId" ? Guid.Empty : SemillaDatos.RolSinGestionA.Id;

        var respuesta = await RegistrarAsync(fabrica.ClienteComo(SemillaDatos.Ana.Identidad), nombre, identificador, rol, ct);

        var problema = await ProblemaAsync(respuesta, HttpStatusCode.BadRequest, CodigosError.Validacion, ct);
        Assert.Equal([campo], problema.Errors!.Keys);
        entorno.SinAlta();
    }

    [Fact]
    public async Task FormatoInvalidoSegunLaPolitica_Responde400ConErrorDelIdentificador()
    {
        var ct = TestContext.Current.CancellationToken;
        var entorno = new Entorno();
        entorno.Identificador.ProblemasDeFormato = ["Problema de formato del doble."];
        await using var fabrica = entorno.Fabrica();

        var respuesta = await RegistrarAsync(fabrica.ClienteComo(SemillaDatos.Ana.Identidad), "Elena Soto", "elena.soto", SemillaDatos.RolSinGestionA.Id, ct);

        var problema = await ProblemaAsync(respuesta, HttpStatusCode.BadRequest, CodigosError.Validacion, ct);
        Assert.Equal(["Problema de formato del doble."], Assert.Single(problema.Errors!, e => e.Key == "identificadorAcceso").Value);
        entorno.SinAlta();
    }

    [Fact]
    public async Task IdentificadorEnUso_Responde409_SinDatosDeOtraEmpresa()
    {
        var ct = TestContext.Current.CancellationToken;
        var entorno = new Entorno();
        entorno.Identificador.EnUso = true;
        await using var fabrica = entorno.Fabrica();

        // El identificador de berta (empresa B): la respuesta no puede revelar dónde está en uso.
        var berta = SemillaDatos.Berta;
        var respuesta = await RegistrarAsync(fabrica.ClienteComo(SemillaDatos.Ana.Identidad), "Elena Soto", berta.IdentificadorAcceso, SemillaDatos.RolSinGestionA.Id, ct);

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        await ProblemaAsync(respuesta, HttpStatusCode.Conflict, CodigosError.IdentificadorEnUso, ct);
        foreach (var dato in new[] { berta.Id.ToString(), SemillaDatos.EmpresaB.ToString(), berta.NombreCompleto, berta.IdentificadorAcceso })
        {
            Assert.DoesNotContain(dato, cuerpo, StringComparison.OrdinalIgnoreCase);
        }

        entorno.SinAlta();
    }

    [Fact]
    public async Task LimiteAlcanzado_Responde409LimiteUsuariosAlcanzado()
    {
        var ct = TestContext.Current.CancellationToken;
        var entorno = new Entorno();
        entorno.Limite.Situacion = SituacionLimiteUsuarios.LimiteAlcanzado;
        await using var fabrica = entorno.Fabrica();

        var respuesta = await RegistrarAsync(fabrica.ClienteComo(SemillaDatos.Ana.Identidad), "Elena Soto", "elena.soto", SemillaDatos.RolSinGestionA.Id, ct);

        await ProblemaAsync(respuesta, HttpStatusCode.Conflict, CodigosError.LimiteUsuariosAlcanzado, ct);
        entorno.SinAlta();
    }

    [Fact]
    public async Task RolDeOtraEmpresa_Responde409RolNoValido_SinDatosDeB()
    {
        var ct = TestContext.Current.CancellationToken;
        var entorno = new Entorno();
        await using var fabrica = entorno.Fabrica();

        var respuesta = await RegistrarAsync(fabrica.ClienteComo(SemillaDatos.Ana.Identidad), "Elena Soto", "elena.soto", SemillaDatos.RolSinGestionB.Id, ct);

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        await ProblemaAsync(respuesta, HttpStatusCode.Conflict, CodigosError.RolNoValido, ct);
        Assert.DoesNotContain(SemillaDatos.EmpresaB.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SemillaDatos.RolSinGestionB.Id.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        entorno.SinAlta();
    }

    [Fact]
    public async Task CuerpoConEmpresaUOtrosCampos_Responde400Validacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var entorno = new Entorno();
        await using var fabrica = entorno.Fabrica();
        var cuerpo = $"{{\"nombreCompleto\":\"Elena\",\"identificadorAcceso\":\"elena\",\"rolId\":\"{SemillaDatos.RolSinGestionA.Id}\",\"empresaId\":\"{SemillaDatos.EmpresaB}\"}}";

        var respuesta = await fabrica.ClienteComo(SemillaDatos.Ana.Identidad)
            .PostAsync("/api/usuarios", new StringContent(cuerpo, Encoding.UTF8, "application/json"), ct);

        await ProblemaAsync(respuesta, HttpStatusCode.BadRequest, CodigosError.Validacion, ct);
        entorno.SinAlta();
    }

    /// <summary>Con el gate abierto: el 403 es de autorización, no de un endpoint que no existe.</summary>
    [Fact]
    public async Task SinLaCapacidadDeRegistrar_Responde403_ConElGateAbierto()
    {
        var ct = TestContext.Current.CancellationToken;
        var entorno = new Entorno();
        await using var fabrica = entorno.Fabrica();
        Assert.True(PostMapeado(fabrica));

        var respuesta = await RegistrarAsync(fabrica.ClienteComo(SemillaDatos.Carla.Identidad), "Elena Soto", "elena.soto", SemillaDatos.RolSinGestionA.Id, ct);

        await ProblemaAsync(respuesta, HttpStatusCode.Forbidden, CodigosError.SinPermiso, ct);
        Assert.Empty(entorno.Alta.AltasConsultadas);
        entorno.SinAlta();
    }

    /// <summary>Sin los tres puntos de integración registrados, el endpoint no se entrega (tasks.md → gating).</summary>
    [Fact]
    public async Task GateCerrado_ElEndpointNoSeMapea()
    {
        var ct = TestContext.Current.CancellationToken;
        var entorno = new Entorno();
        await using var fabrica = entorno.Fabrica(conIntegraciones: false);

        Assert.False(PostMapeado(fabrica));
        var respuesta = await RegistrarAsync(fabrica.ClienteComo(SemillaDatos.Ana.Identidad), "Elena Soto", "elena.soto", SemillaDatos.RolSinGestionA.Id, ct);
        Assert.NotEqual(HttpStatusCode.Created, respuesta.StatusCode);
        entorno.SinAlta();
    }

    private static bool PostMapeado(WebApplicationFactory<Program> fabrica) =>
        fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Any(e => e.RoutePattern.RawText == "/api/usuarios"
                && e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true);

    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, string nombre, string identificador, Guid rolId, CancellationToken ct) =>
        cliente.PostAsJsonAsync("/api/usuarios", new RegistrarUsuarioSolicitud { NombreCompleto = nombre, IdentificadorAcceso = identificador, RolId = rolId }, ct);

    private static async Task<Problema> ProblemaAsync(HttpResponseMessage respuesta, HttpStatusCode estado, string codigo, CancellationToken ct)
    {
        Assert.Equal(estado, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = (await respuesta.Content.ReadFromJsonAsync<Problema>(Json, ct))!;
        Assert.Equal(codigo, problema.Codigo);
        return problema;
    }

    /// <summary>Dobles de una prueba: integraciones configuradas para un alta correcta y almacén en memoria.</summary>
    private sealed class Entorno
    {
        public PoliticaAltaUsuarioDoble Alta { get; } = new() { EstadoInicial = EstadoUsuario.Inactivo };

        public PoliticaIdentificadorAccesoDoble Identificador { get; } = new() { ProblemasDeFormato = [], EnUso = false };

        public FuenteLimiteUsuariosDoble Limite { get; } = new() { Situacion = SituacionLimiteUsuarios.SinLimiteAplicable };

        public AlmacenEmpresaA Almacen { get; } = new();

        public WebApplicationFactory<Program> Fabrica(bool conIntegraciones = true) =>
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.UseSetting("ConnectionStrings:Kryon", "Server=no-se-usa;Database=no-se-usa");
                builder.ConfigureTestServices(servicios =>
                {
                    servicios.AddSingleton<IUnidadDeTrabajo>(Almacen);
                    servicios.AddSingleton<IRepositorioUsuarios>(Almacen);
                    servicios.AddSingleton<IRegistroAuditoria>(Almacen);
                    servicios.AddSingleton<IConsultaUsuarios>(Almacen);
                    if (conIntegraciones)
                    {
                        servicios.AddSingleton<IPoliticaAltaUsuario>(Alta);
                        servicios.AddSingleton<IPoliticaIdentificadorAcceso>(Identificador);
                        servicios.AddSingleton<IFuenteLimiteUsuarios>(Limite);
                    }
                });
            });

        public void SinAlta()
        {
            Assert.Empty(Almacen.Agregados);
            Assert.Empty(Almacen.Auditorias);
            Assert.Empty(Alta.CredencialesGestionadas);
            Assert.DoesNotContain("Confirmar", Almacen.Pasos);
        }
    }

    /// <summary>
    /// Persistencia y consulta en memoria de la empresa A (la de la identidad de ana) con sus roles semilla. Al agregar
    /// asigna una versión, como haría <c>rowversion</c>; al revertir descarta lo agregado y lo auditado.
    /// </summary>
    private sealed class AlmacenEmpresaA : IUnidadDeTrabajo, IRepositorioUsuarios, IRegistroAuditoria, IConsultaUsuarios
    {
        private readonly Dictionary<Guid, RolReferencia> roles = SemillaDatos.Roles
            .Where(r => r.EmpresaId == SemillaDatos.EmpresaA)
            .ToDictionary(r => r.Id, r => new RolReferencia(r.Id, r.EmpresaId, r.Nombre, r.Capacidades));

        public List<string> Pasos { get; } = [];

        public List<Usuario> Agregados { get; } = [];

        public List<AuditoriaUsuario> Auditorias { get; } = [];

        public Task IniciarAsync(CancellationToken cancellationToken) => Paso("Iniciar");

        public Task ConfirmarAsync(CancellationToken cancellationToken) => Paso("Confirmar");

        public Task RevertirAsync(CancellationToken cancellationToken)
        {
            Pasos.Add("Revertir");
            Agregados.Clear();
            Auditorias.Clear();
            return Task.CompletedTask;
        }

        public Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            Pasos.Add("Agregar");
            typeof(Usuario).GetProperty(nameof(Usuario.Version))!.SetValue(usuario, new byte[] { 0, 0, 0, 0, 0, 0, 7, 0xE1 });
            Agregados.Add(usuario);
            return Task.CompletedTask;
        }

        public Task RegistrarAsync(AuditoriaUsuario registro, CancellationToken cancellationToken)
        {
            Pasos.Add($"Auditar {registro.TipoOperacion}");
            Auditorias.Add(registro);
            return Task.CompletedTask;
        }

        public Task<RolReferencia?> ObtenerRolAsync(Guid rolId, CancellationToken cancellationToken) =>
            Task.FromResult(roles.GetValueOrDefault(rolId));

        Task<UsuarioConRol?> IConsultaUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            Task.FromResult(Agregados.SingleOrDefault(u => u.Id == usuarioId) is { } u ? new UsuarioConRol(u, roles.GetValueOrDefault(u.RolId)) : null);

        public Task<IReadOnlyList<RolReferencia>> ListarRolesAsignablesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RolReferencia>>([.. roles.Values]);

        public Task<PaginaConsultaUsuarios> BuscarAsync(CriteriosConsultaUsuarios criterios, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("El registro no lista usuarios.");

        Task<Usuario?> IRepositorioUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("El registro no carga usuarios existentes.");

        public Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("El registro no cuenta administradores.");

        public Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("El registro no actualiza usuarios.");

        private Task Paso(string paso)
        {
            Pasos.Add(paso);
            return Task.CompletedTask;
        }
    }
}
