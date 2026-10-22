using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Contracts.Usuarios;
using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// <c>PUT /api/usuarios/{id}</c> con <c>identificadorAcceso</c> mientras el gate <c>EdicionIdentificador</c> está
/// cerrado (tasks.md → <i>Feature / release gating</i>: "la edición no acepta <c>identificadorAcceso</c>"). El campo no
/// se ignora, ni siquiera con <c>null</c> explícito: la solicitud se rechaza con 400 <c>validacion</c> y la clave <c>identificadorAcceso</c>, sin llegar al
/// caso de uso, así que no cambia ningún dato ni se audita nada. No decide si el identificador es editable (DEP-4,
/// T079/T080) ni responde <c>identificador-no-editable</c>.
/// </summary>
/// <remarks>
/// No necesita base de datos: la persistencia y la consulta son dobles en memoria que registran cada llamada, y la
/// cadena de conexión es ficticia y nunca se abre.
/// </remarks>
public sealed class EditarIdentificadorGateCerradoTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Basta con que la propiedad esté presente: con un valor o con <c>null</c> explícito.</summary>
    [Theory]
    [InlineData("\"carla.nueva\"")]
    [InlineData("null")]
    public async Task ConIdentificadorAcceso_Responde400Validacion_SinTocarDatosNiAuditar(string valorJson)
    {
        var ct = TestContext.Current.CancellationToken;
        var espia = new Espia();
        await using var fabrica = Fabrica(espia);

        var respuesta = await EditarAsync(fabrica, $", \"identificadorAcceso\": {valorJson}", ct);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = (await respuesta.Content.ReadFromJsonAsync<Problema>(Json, ct))!;
        Assert.Equal(CodigosError.Validacion, problema.Codigo);
        Assert.NotNull(problema.Errors);
        Assert.Equal(["identificadorAcceso"], problema.Errors.Keys);
        Assert.All(problema.Errors.Values, mensajes => Assert.NotEmpty(mensajes));

        // No se abrió transacción, no se cargó ni guardó el usuario y no hubo auditoría de ningún tipo.
        Assert.Empty(espia.Llamadas);
    }

    /// <summary>Sin la propiedad, la edición de nombre y rol sigue adelante y llega al caso de uso.</summary>
    [Fact]
    public async Task SinIdentificadorAcceso_LaEdicionContinua()
    {
        var ct = TestContext.Current.CancellationToken;
        var espia = new Espia();
        await using var fabrica = Fabrica(espia);

        var respuesta = await EditarAsync(fabrica, string.Empty, ct);

        // El doble no tiene usuarios: el caso de uso se ejecuta y responde usuario-no-encontrado.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(["IniciarAsync", "Repositorio.ObtenerAsync"], espia.Llamadas.Take(2));
    }

    /// <summary>PUT de carla como ana, con nombre y rol válidos y <paramref name="extra"/> añadido al cuerpo JSON.</summary>
    private static Task<HttpResponseMessage> EditarAsync(WebApplicationFactory<Program> fabrica, string extra, CancellationToken ct)
    {
        var cuerpo = $"{{\"nombreCompleto\": \"Carla Ruiz\", \"rolId\": \"{SemillaDatos.RolAdministracionA.Id}\"{extra}}}";
        var solicitud = new HttpRequestMessage(HttpMethod.Put, $"/api/usuarios/{SemillaDatos.Carla.Id}")
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
        };
        solicitud.Headers.IfMatch.Add(EntityTagHeaderValue.Parse("\"AAAAAAAAB9E=\""));
        return fabrica.ClienteComo(SemillaDatos.Ana.Identidad).SendAsync(solicitud, ct);
    }

    private static WebApplicationFactory<Program> Fabrica(Espia espia) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:Kryon", "Server=no-se-usa;Database=no-se-usa");
            builder.ConfigureTestServices(servicios =>
            {
                servicios.AddSingleton<IUnidadDeTrabajo>(espia);
                servicios.AddSingleton<IBloqueoAdministradoresEmpresa>(espia);
                servicios.AddSingleton<IRepositorioUsuarios>(espia);
                servicios.AddSingleton<IRegistroAuditoria>(espia);
                servicios.AddSingleton<IConsultaUsuarios>(espia);
            });
        });

    /// <summary>Dobles de persistencia y consulta que solo registran que se los llamó.</summary>
    private sealed class Espia : IUnidadDeTrabajo, IBloqueoAdministradoresEmpresa, IRepositorioUsuarios, IRegistroAuditoria, IConsultaUsuarios
    {
        public List<string> Llamadas { get; } = [];

        public Task IniciarAsync(CancellationToken cancellationToken) => Registrar(nameof(IniciarAsync));

        public Task ConfirmarAsync(CancellationToken cancellationToken) => Registrar(nameof(ConfirmarAsync));

        public Task RevertirAsync(CancellationToken cancellationToken) => Registrar(nameof(RevertirAsync));

        public Task AdquirirAsync(CancellationToken cancellationToken) => Registrar(nameof(AdquirirAsync));

        Task<Usuario?> IRepositorioUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            Registrar<Usuario?>("Repositorio.ObtenerAsync", null);

        public Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken) =>
            Registrar(nameof(ContarAdministradoresActivosAsync), 0);

        public Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken) => Registrar(nameof(AgregarAsync));

        public Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken) => Registrar(nameof(ActualizarAsync));

        public Task RegistrarAsync(AuditoriaUsuario registro, CancellationToken cancellationToken) =>
            Registrar($"{nameof(RegistrarAsync)} {registro.TipoOperacion}");

        public Task<PaginaConsultaUsuarios> BuscarAsync(CriteriosConsultaUsuarios criterios, CancellationToken cancellationToken) =>
            Registrar<PaginaConsultaUsuarios>(nameof(BuscarAsync), null!);

        Task<UsuarioConRol?> IConsultaUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            Registrar<UsuarioConRol?>("Consulta.ObtenerAsync", null);

        public Task<IReadOnlyList<RolReferencia>> ListarRolesAsignablesAsync(CancellationToken cancellationToken) =>
            Registrar<IReadOnlyList<RolReferencia>>(nameof(ListarRolesAsignablesAsync), []);

        public Task<RolReferencia?> ObtenerRolAsync(Guid rolId, CancellationToken cancellationToken) =>
            Registrar<RolReferencia?>(nameof(ObtenerRolAsync), null);

        private Task Registrar(string llamada)
        {
            Llamadas.Add(llamada);
            return Task.CompletedTask;
        }

        private Task<T> Registrar<T>(string llamada, T resultado)
        {
            Llamadas.Add(llamada);
            return Task.FromResult(resultado);
        }
    }
}
