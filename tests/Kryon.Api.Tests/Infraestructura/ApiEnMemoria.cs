using Kryon.Api.Seguridad;
using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;
using Kryon.Core.Usuarios.Integraciones;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Api.Tests.Infraestructura;

/// <summary>
/// API real (pipeline, autenticación <c>IdentidadPrueba</c>, políticas, endpoints y casos de uso de Core) sobre una
/// persistencia en memoria de la empresa A, para pruebas de autorización y de coherencia que no necesitan SQL Server ni
/// Docker. La cadena de conexión es ficticia y nunca se abre.
/// </summary>
/// <remarks>
/// No sustituye a las pruebas de integración con SQL Server real (aislamiento, RLS, concurrencia): solo evita la base de
/// datos donde lo que se prueba es la autorización y la forma de las respuestas. Con <c>conIntegraciones</c> registra los
/// dobles de <see cref="DoblesIntegraciones"/> configurados para un alta correcta, lo que abre el gate
/// <c>RegistroUsuarios</c>; no representan las reglas reales de DEP-2, DEP-3 ni DEP-5.
/// </remarks>
public sealed class ApiEnMemoria : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> fabrica;

    public ApiEnMemoria(bool conIntegraciones = true)
    {
        fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:Kryon", "Server=no-se-usa;Database=no-se-usa");
            builder.ConfigureTestServices(servicios =>
            {
                servicios.AddSingleton<IUnidadDeTrabajo>(Almacen);
                servicios.AddSingleton<IBloqueoAdministradoresEmpresa>(Almacen);
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
    }

    public AlmacenEmpresaA Almacen { get; } = new();

    public PoliticaAltaUsuarioDoble Alta { get; } = new() { EstadoInicial = EstadoUsuario.Activo };

    public PoliticaIdentificadorAccesoDoble Identificador { get; } = new() { ProblemasDeFormato = [], EnUso = false };

    public FuenteLimiteUsuariosDoble Limite { get; } = new() { Situacion = SituacionLimiteUsuarios.SinLimiteAplicable };

    /// <summary>Cliente con la identidad de <paramref name="usuario"/> y exactamente esas capacidades.</summary>
    public HttpClient ClienteComo(UsuarioSembrado usuario, IEnumerable<string> capacidades) =>
        fabrica.ClienteComo(usuario.Identidad, capacidades);

    public ValueTask DisposeAsync() => fabrica.DisposeAsync();
}

/// <summary>
/// Persistencia y consulta en memoria de la empresa A con sus usuarios y roles semilla. Registra cada llamada en
/// <see cref="Llamadas"/> y cada versión nueva como haría <c>rowversion</c>; al revertir restaura los usuarios y descarta
/// lo agregado y lo auditado en la transacción.
/// </summary>
public sealed class AlmacenEmpresaA : IUnidadDeTrabajo, IBloqueoAdministradoresEmpresa, IRepositorioUsuarios, IRegistroAuditoria, IConsultaUsuarios
{
    private readonly Dictionary<Guid, RolReferencia> roles = SemillaDatos.Roles
        .Where(r => r.EmpresaId == SemillaDatos.EmpresaA)
        .ToDictionary(r => r.Id, r => new RolReferencia(r.Id, r.EmpresaId, r.Nombre, r.Capacidades));

    private readonly Dictionary<Guid, Usuario> usuarios = [];
    private Dictionary<Guid, EstadoGuardado>? copia;
    private int auditoriasAlIniciar;
    private long version = 2000;

    public AlmacenEmpresaA()
    {
        foreach (var semilla in SemillaDatos.Usuarios.Where(u => u.Rol.EmpresaId == SemillaDatos.EmpresaA))
        {
            var usuario = new Usuario(SemillaDatos.EmpresaA, semilla.NombreCompleto, semilla.IdentificadorAcceso, semilla.Rol.Id, semilla.Estado, SemillaDatos.Momento);
            typeof(Usuario).GetProperty(nameof(Usuario.Id))!.SetValue(usuario, semilla.Id);
            NuevaVersion(usuario);
            usuarios[usuario.Id] = usuario;
        }
    }

    /// <summary>Cada llamada recibida, en orden.</summary>
    public List<string> Llamadas { get; } = [];

    public List<AuditoriaUsuario> Auditorias { get; } = [];

    public Usuario this[Guid id] => usuarios[id];

    /// <summary>Estado guardado de todos los usuarios, para comprobar que una solicitud no cambió nada.</summary>
    public IReadOnlyDictionary<Guid, EstadoGuardado> Instantanea() => usuarios.ToDictionary(p => p.Key, p => EstadoGuardado.De(p.Value));

    public Task IniciarAsync(CancellationToken cancellationToken)
    {
        Llamadas.Add("Iniciar");
        copia = new Dictionary<Guid, EstadoGuardado>(Instantanea());
        auditoriasAlIniciar = Auditorias.Count;
        return Task.CompletedTask;
    }

    public Task ConfirmarAsync(CancellationToken cancellationToken)
    {
        Llamadas.Add("Confirmar");
        copia = null;
        return Task.CompletedTask;
    }

    public Task RevertirAsync(CancellationToken cancellationToken)
    {
        Llamadas.Add("Revertir");
        foreach (var id in usuarios.Keys.Where(id => copia?.ContainsKey(id) != true).ToList())
        {
            usuarios.Remove(id);
        }

        foreach (var (id, guardado) in copia ?? [])
        {
            guardado.Restaurar(usuarios[id]);
        }

        Auditorias.RemoveRange(auditoriasAlIniciar, Auditorias.Count - auditoriasAlIniciar);
        copia = null;
        return Task.CompletedTask;
    }

    public Task AdquirirAsync(CancellationToken cancellationToken)
    {
        Llamadas.Add("Bloquear");
        return Task.CompletedTask;
    }

    Task<Usuario?> IRepositorioUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        Llamadas.Add("Repositorio.Obtener");
        return Task.FromResult(usuarios.GetValueOrDefault(usuarioId));
    }

    public Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken)
    {
        Llamadas.Add("ContarAdministradores");
        return Task.FromResult(usuarios.Values.Count(u => ReglaUltimoAdministrador.EsAdministradorActivo(u, roles.GetValueOrDefault(u.RolId), SemillaDatos.Administrar)));
    }

    public Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        Llamadas.Add("Agregar");
        NuevaVersion(usuario);
        usuarios[usuario.Id] = usuario;
        return Task.CompletedTask;
    }

    public Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        Llamadas.Add("Actualizar");
        NuevaVersion(usuario);
        return Task.CompletedTask;
    }

    public Task RegistrarAsync(AuditoriaUsuario registro, CancellationToken cancellationToken)
    {
        Llamadas.Add($"Auditar {registro.TipoOperacion}");
        Auditorias.Add(registro);
        return Task.CompletedTask;
    }

    public Task<PaginaConsultaUsuarios> BuscarAsync(CriteriosConsultaUsuarios criterios, CancellationToken cancellationToken)
    {
        Llamadas.Add("Consulta.Buscar");
        var todos = usuarios.Values.OrderBy(u => u.NombreCompleto, StringComparer.Ordinal).ThenBy(u => u.Id).ToList();
        var tamano = criterios.TamanoPagina ?? 25;
        var pagina = todos.Skip((criterios.Pagina - 1) * tamano).Take(tamano).Select(ConRol).ToList();
        return Task.FromResult(new PaginaConsultaUsuarios(pagina, todos.Count, criterios.Pagina, tamano));
    }

    Task<UsuarioConRol?> IConsultaUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        Llamadas.Add("Consulta.Obtener");
        return Task.FromResult(usuarios.TryGetValue(usuarioId, out var usuario) ? ConRol(usuario) : null);
    }

    public Task<IReadOnlyList<RolReferencia>> ListarRolesAsignablesAsync(CancellationToken cancellationToken)
    {
        Llamadas.Add("Consulta.Roles");
        return Task.FromResult<IReadOnlyList<RolReferencia>>([.. roles.Values]);
    }

    public Task<RolReferencia?> ObtenerRolAsync(Guid rolId, CancellationToken cancellationToken)
    {
        Llamadas.Add("Consulta.Rol");
        return Task.FromResult(roles.GetValueOrDefault(rolId));
    }

    private UsuarioConRol ConRol(Usuario usuario) => new(usuario, roles.GetValueOrDefault(usuario.RolId));

    private void NuevaVersion(Usuario usuario) =>
        typeof(Usuario).GetProperty(nameof(Usuario.Version))!.SetValue(usuario, BitConverter.GetBytes(version++).Reverse().ToArray());
}

/// <summary>Datos guardados de un usuario que una operación puede cambiar.</summary>
public sealed record EstadoGuardado(string NombreCompleto, string IdentificadorAcceso, Guid RolId, EstadoUsuario Estado, string Version)
{
    public static EstadoGuardado De(Usuario usuario) =>
        new(usuario.NombreCompleto, usuario.IdentificadorAcceso, usuario.RolId, usuario.Estado, Convert.ToBase64String(usuario.Version));

    public void Restaurar(Usuario usuario)
    {
        typeof(Usuario).GetProperty(nameof(Usuario.NombreCompleto))!.SetValue(usuario, NombreCompleto);
        typeof(Usuario).GetProperty(nameof(Usuario.RolId))!.SetValue(usuario, RolId);
        typeof(Usuario).GetProperty(nameof(Usuario.Estado))!.SetValue(usuario, Estado);
        typeof(Usuario).GetProperty(nameof(Usuario.Version))!.SetValue(usuario, Convert.FromBase64String(Version));
    }
}
