using System.Reflection;
using System.Text.Json;

using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;

namespace Kryon.Core.Tests.Usuarios;

/// <summary>
/// Validador (T078) y edición de usuarios (T081): nombre completo obligatorio, recortado y con tope configurable
/// (FR-024, FR-025); rol de la empresa actual (FR-026, US4-9); el propio rol (FR-035); el último administrador
/// (FR-036, US4-11); y auditoría solo con los campos cambiados (FR-044).
/// </summary>
/// <remarks>
/// Test-first: <c>EditarUsuario</c> (T081) aún no existe, así que se localiza en tiempo de ejecución y, mientras falte,
/// esas pruebas fallan diciendo qué tarea lo aporta. Contrato mínimo que fijan: un único constructor público con
/// dependencias de Core (incluido <see cref="ValidadorUsuario"/>) y un método público <c>EditarAsync(Guid usuarioId,
/// byte[] version, string nombreCompleto, Guid rolId, CancellationToken)</c>. <c>CambiosJson</c> sigue el formato de
/// T027 (<c>[{"campo":…,"anterior":…,"nuevo":…}]</c>) con los nombres de campo del contrato (<c>nombreCompleto</c>,
/// <c>rolId</c>).
/// </remarks>
public sealed class EditarUsuarioTests
{
    private const string CapacidadAdministrar = "usuarios.administrar";

    private static readonly Guid EmpresaA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly DateTimeOffset Momento = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly RolReferencia RolAdministracion =
        new(Guid.Parse("a1000000-0000-0000-0000-000000000001"), EmpresaA, "Administración", [CapacidadAdministrar]);

    private static readonly RolReferencia RolSinGestion =
        new(Guid.Parse("a1000000-0000-0000-0000-000000000003"), EmpresaA, "Sin gestión", []);

    /// <summary>Rol de otra empresa: la consulta de la empresa actual no lo encuentra.</summary>
    private static readonly Guid RolDeOtraEmpresa = Guid.Parse("b1000000-0000-0000-0000-000000000002");

    private static readonly OpcionesUsuarios Opciones = new()
    {
        Validacion = new() { LongitudMaximaNombre = 20, LongitudMaximaIdentificador = 30 },
    };

    // ---- ValidadorUsuario (T078) ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NombreVacioOSoloEspacios_EsErrorDeCampo(string? nombre)
    {
        var error = await Assert.ThrowsAsync<ErrorUsuarios>(() => Validador().ValidarAsync(nombre, null, false, RolSinGestion.Id, CancellationToken.None));

        Assert.Equal(CodigoErrorUsuarios.Validacion, error.Codigo);
        Assert.Equal([ValidadorUsuario.CampoNombreCompleto], error.Errores.Keys);
    }

    [Fact]
    public async Task Nombre_SeRecortanLosEspacios()
    {
        var datos = await Validador().ValidarAsync("  Ana Pérez  ", null, false, RolSinGestion.Id, CancellationToken.None);

        Assert.Equal("Ana Pérez", datos.NombreCompleto);
        Assert.Equal(RolSinGestion, datos.Rol);
    }

    [Fact]
    public async Task NombrePorEncimaDelMaximoConfigurado_EsErrorDeCampo()
    {
        var justo = new string('n', Opciones.Validacion.LongitudMaximaNombre);
        Assert.Equal(justo, (await Validador().ValidarAsync(justo, null, false, RolSinGestion.Id, CancellationToken.None)).NombreCompleto);

        var error = await Assert.ThrowsAsync<ErrorUsuarios>(() =>
            Validador().ValidarAsync(justo + "n", null, false, RolSinGestion.Id, CancellationToken.None));
        Assert.Equal(CodigoErrorUsuarios.Validacion, error.Codigo);
        Assert.Equal([ValidadorUsuario.CampoNombreCompleto], error.Errores.Keys);
    }

    [Fact]
    public async Task Identificador_ObligatorioYConMaximo_SoloCuandoLaOperacionLoRecibe()
    {
        var obligatorio = await Assert.ThrowsAsync<ErrorUsuarios>(() => Validador().ValidarAsync("Ana", " ", true, RolSinGestion.Id, CancellationToken.None));
        Assert.Equal([ValidadorUsuario.CampoIdentificadorAcceso], obligatorio.Errores.Keys);

        var largo = await Assert.ThrowsAsync<ErrorUsuarios>(() =>
            Validador().ValidarAsync("Ana", new string('i', Opciones.Validacion.LongitudMaximaIdentificador + 1), true, RolSinGestion.Id, CancellationToken.None));
        Assert.Equal([ValidadorUsuario.CampoIdentificadorAcceso], largo.Errores.Keys);

        // Sin formato ni unicidad (DEP-3): cualquier texto dentro del tope se acepta tal cual, sin normalizar.
        Assert.Equal(" Ana.X@Y ", (await Validador().ValidarAsync("Ana", " Ana.X@Y ", true, RolSinGestion.Id, CancellationToken.None)).IdentificadorAcceso);

        // La edición no recibe el identificador (DEP-4): no se valida.
        Assert.Null((await Validador().ValidarAsync("Ana", null, false, RolSinGestion.Id, CancellationToken.None)).IdentificadorAcceso);
    }

    [Fact]
    public async Task RolDeOtraEmpresa_EsRolNoValido()
    {
        var error = await Assert.ThrowsAsync<ErrorUsuarios>(() => Validador().ValidarAsync("Ana", null, false, RolDeOtraEmpresa, CancellationToken.None));

        Assert.Equal(CodigoErrorUsuarios.RolNoValido, error.Codigo);
    }

    [Fact]
    public async Task RolVacio_EsErrorDeCampo()
    {
        var error = await Assert.ThrowsAsync<ErrorUsuarios>(() => Validador().ValidarAsync("Ana", null, false, Guid.Empty, CancellationToken.None));

        Assert.Equal([ValidadorUsuario.CampoRolId], error.Errores.Keys);
    }

    // ---- EditarUsuario (T081) ----

    [Fact]
    public async Task CambiarElPropioRol_SeRechaza()
    {
        var dobles = new Dobles();
        var actor = dobles.Agregar("Ana", RolAdministracion, id: Dobles.Actor);
        dobles.Agregar("Beto", RolAdministracion);

        var error = await Assert.ThrowsAsync<ErrorUsuarios>(EditorDe(dobles, actor, actor.NombreCompleto, RolSinGestion.Id));

        Assert.Equal(CodigoErrorUsuarios.NoPuedeCambiarSuPropioRol, error.Codigo);
        Assert.Empty(dobles.Auditorias);
    }

    [Fact]
    public async Task QuitarElRolAlUltimoAdministradorActivo_SeRechaza()
    {
        var dobles = new Dobles();
        dobles.Agregar("Carla", RolSinGestion, id: Dobles.Actor);
        var unico = dobles.Agregar("Ana", RolAdministracion);

        var error = await Assert.ThrowsAsync<ErrorUsuarios>(EditorDe(dobles, unico, unico.NombreCompleto, RolSinGestion.Id));

        Assert.Equal(CodigoErrorUsuarios.UltimoAdministrador, error.Codigo);
        Assert.Empty(dobles.Auditorias);
        Assert.Contains("Revertir", dobles.Pasos);
    }

    [Fact]
    public async Task RolDeOtraEmpresa_EnLaEdicion_EsRolNoValido()
    {
        var dobles = new Dobles();
        dobles.Agregar("Ana", RolAdministracion, id: Dobles.Actor);
        var carla = dobles.Agregar("Carla", RolSinGestion);

        var error = await Assert.ThrowsAsync<ErrorUsuarios>(EditorDe(dobles, carla, carla.NombreCompleto, RolDeOtraEmpresa));

        Assert.Equal(CodigoErrorUsuarios.RolNoValido, error.Codigo);
        Assert.Equal(RolSinGestion.Id, carla.RolId);
    }

    [Fact]
    public async Task CambiosAuditados_SoloLosCamposModificados()
    {
        var dobles = new Dobles();
        dobles.Agregar("Ana", RolAdministracion, id: Dobles.Actor);
        var carla = dobles.Agregar("Carla", RolSinGestion);

        await EditarAsync(dobles, carla, "  Carla Ruiz  ", RolSinGestion.Id);

        var registro = Assert.Single(dobles.Auditorias);
        Assert.Equal(TipoOperacion.Modificacion, registro.TipoOperacion);
        Assert.Equal(carla.Id, registro.UsuarioAfectadoId);
        Assert.Equal(Dobles.Actor, registro.ActorUsuarioId);
        Assert.Equal(EmpresaA, registro.EmpresaId);

        var cambios = Cambios(registro);
        var cambio = Assert.Single(cambios);
        Assert.Equal(ValidadorUsuario.CampoNombreCompleto, cambio.GetProperty("campo").GetString());
        Assert.Equal("Carla", cambio.GetProperty("anterior").GetString());
        Assert.Equal("Carla Ruiz", cambio.GetProperty("nuevo").GetString());
        Assert.Equal("Carla Ruiz", carla.NombreCompleto);

        await EditarAsync(dobles, carla, "Carla Ruiz", RolAdministracion.Id);

        var soloRol = Assert.Single(Cambios(dobles.Auditorias[^1]));
        Assert.Equal(ValidadorUsuario.CampoRolId, soloRol.GetProperty("campo").GetString());
        Assert.Equal(RolAdministracion.Id, carla.RolId);
    }

    private static ValidadorUsuario Validador() => new(Opciones, new Dobles());

    private static JsonElement[] Cambios(AuditoriaUsuario registro)
    {
        Assert.NotNull(registro.CambiosJson);
        using var documento = JsonDocument.Parse(registro.CambiosJson);
        return [.. documento.RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    /// <summary>Construye <c>EditarUsuario</c> (T081) con los dobles y edita <paramref name="usuario"/> con su versión vigente.</summary>
    private static Task EditarAsync(Dobles dobles, Usuario usuario, string nombreCompleto, Guid rolId) =>
        EditorDe(dobles, usuario, nombreCompleto, rolId)();

    /// <summary>
    /// Prepara la edición: localiza <c>EditarUsuario</c> ya, fuera de <c>Assert.ThrowsAsync</c>, para que su ausencia se
    /// informe como tal y no como una excepción de otro tipo.
    /// </summary>
    private static Func<Task> EditorDe(Dobles dobles, Usuario usuario, string nombreCompleto, Guid rolId)
    {
        var tipo = typeof(Usuario).Assembly.GetType("Kryon.Core.Usuarios.EditarUsuario")
            ?? throw new Xunit.Sdk.XunitException("EditarUsuario aún no está implementado (T081).");
        var constructor = Assert.Single(tipo.GetConstructors());
        var caso = constructor.Invoke([.. constructor.GetParameters().Select(p => dobles.Para(p.ParameterType))]);
        var editar = tipo.GetMethod("EditarAsync", [typeof(Guid), typeof(byte[]), typeof(string), typeof(Guid), typeof(CancellationToken)])
            ?? throw new Xunit.Sdk.XunitException("EditarUsuario no tiene EditarAsync(Guid, byte[], string, Guid, CancellationToken) (T081).");

        return async () =>
        {
            try
            {
                await (Task)editar.Invoke(caso, [usuario.Id, usuario.Version, nombreCompleto, rolId, CancellationToken.None])!;
            }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            {
                throw e.InnerException;
            }
        };
    }

    /// <summary>
    /// Dobles de Core en memoria para la empresa A: usuarios, roles de A, transacción con reversión, bloqueo y auditoría.
    /// Los administradores activos se cuentan con la capacidad de acceso, como el repositorio real.
    /// </summary>
    private sealed class Dobles
        : IUnidadDeTrabajo, IRepositorioUsuarios, IBloqueoAdministradoresEmpresa, IRegistroAuditoria, IConsultaUsuarios, IContextoSolicitud
    {
        public static readonly Guid Actor = Guid.Parse("a2000000-0000-0000-0000-000000000001");

        private readonly Dictionary<Guid, Usuario> usuarios = [];
        private readonly Dictionary<Guid, RolReferencia> roles = new() { [RolAdministracion.Id] = RolAdministracion, [RolSinGestion.Id] = RolSinGestion };
        private Dictionary<Guid, (string Nombre, Guid Rol)>? copia;
        private long version = 100;

        public List<string> Pasos { get; } = [];

        public List<AuditoriaUsuario> Auditorias { get; } = [];

        public Guid UsuarioId => Actor;

        public Guid EmpresaId => EmpresaA;

        public IReadOnlyCollection<string> Capacidades => [CapacidadAdministrar, "usuarios.editar"];

        public Usuario Agregar(string nombre, RolReferencia rol, Guid? id = null)
        {
            var usuario = new Usuario(EmpresaA, nombre, nombre.ToLowerInvariant(), rol.Id, EstadoUsuario.Activo, Momento);
            typeof(Usuario).GetProperty(nameof(Usuario.Id))!.SetValue(usuario, id ?? Guid.NewGuid());
            NuevaVersion(usuario);
            usuarios[usuario.Id] = usuario;
            return usuario;
        }

        public object Para(Type tipo) => tipo switch
        {
            _ when tipo == typeof(TimeProvider) => TimeProvider.System,
            _ when tipo == typeof(OpcionesUsuarios) => Opciones,
            _ when tipo == typeof(ValidadorUsuario) => new ValidadorUsuario(Opciones, this),
            _ when tipo.IsInstanceOfType(this) => this,
            _ => throw new Xunit.Sdk.XunitException($"Dependencia de EditarUsuario sin doble en T073: {tipo}."),
        };

        public Task IniciarAsync(CancellationToken cancellationToken)
        {
            Pasos.Add("Iniciar");
            copia = usuarios.ToDictionary(p => p.Key, p => (p.Value.NombreCompleto, p.Value.RolId));
            return Task.CompletedTask;
        }

        public Task ConfirmarAsync(CancellationToken cancellationToken)
        {
            Pasos.Add("Confirmar");
            copia = null;
            return Task.CompletedTask;
        }

        public Task RevertirAsync(CancellationToken cancellationToken)
        {
            Pasos.Add("Revertir");
            foreach (var (id, (nombre, rol)) in copia ?? [])
            {
                typeof(Usuario).GetProperty(nameof(Usuario.NombreCompleto))!.SetValue(usuarios[id], nombre);
                typeof(Usuario).GetProperty(nameof(Usuario.RolId))!.SetValue(usuarios[id], rol);
            }

            return Task.CompletedTask;
        }

        public Task AdquirirAsync(CancellationToken cancellationToken)
        {
            Pasos.Add("Bloquear");
            return Task.CompletedTask;
        }

        Task<Usuario?> IRepositorioUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            Task.FromResult(usuarios.GetValueOrDefault(usuarioId));

        public Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken) =>
            Task.FromResult(usuarios.Values.Count(u => ReglaUltimoAdministrador.EsAdministradorActivo(u, roles.GetValueOrDefault(u.RolId), CapacidadAdministrar)));

        public Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken) => throw new InvalidOperationException();

        public Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            Pasos.Add("Guardar");
            NuevaVersion(usuario);
            return Task.CompletedTask;
        }

        public Task RegistrarAsync(AuditoriaUsuario registro, CancellationToken cancellationToken)
        {
            Pasos.Add("Auditar");
            Auditorias.Add(registro);
            return Task.CompletedTask;
        }

        public Task<PaginaConsultaUsuarios> BuscarAsync(CriteriosConsultaUsuarios criterios, CancellationToken cancellationToken) =>
            throw new InvalidOperationException();

        Task<UsuarioConRol?> IConsultaUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            Task.FromResult(usuarios.TryGetValue(usuarioId, out var u) ? new UsuarioConRol(u, roles.GetValueOrDefault(u.RolId)) : null);

        public Task<IReadOnlyList<RolReferencia>> ListarRolesAsignablesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RolReferencia>>([.. roles.Values]);

        public Task<RolReferencia?> ObtenerRolAsync(Guid rolId, CancellationToken cancellationToken) =>
            Task.FromResult(roles.GetValueOrDefault(rolId));

        private void NuevaVersion(Usuario usuario) =>
            typeof(Usuario).GetProperty(nameof(Usuario.Version))!.SetValue(usuario, BitConverter.GetBytes(version++));
    }
}
