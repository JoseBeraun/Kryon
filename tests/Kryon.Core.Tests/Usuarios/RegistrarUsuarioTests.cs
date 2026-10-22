using System.Reflection;

using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;
using Kryon.Core.Usuarios.Integraciones;

namespace Kryon.Core.Tests.Usuarios;

/// <summary>
/// Registro de usuarios (T087) contra dobles de los puntos de integración: el estado inicial lo decide
/// <see cref="IPoliticaAltaUsuario"/> sin suponer ninguno (DEP-2); el formato y la unicidad del identificador,
/// <see cref="IPoliticaIdentificadorAcceso"/> (DEP-3, FR-027); el límite, <see cref="IFuenteLimiteUsuarios"/> (DEP-5,
/// FR-028, EC-12); la empresa sale siempre del contexto (FR-023); y todo ocurre en una transacción con auditoría
/// <c>Alta</c> (FR-043).
/// </summary>
/// <remarks>
/// Los dobles no representan las reglas reales: cada prueba elige su respuesta. Test-first, como
/// <c>EditarUsuarioTests</c>: <c>RegistrarUsuario</c> se localiza en tiempo de ejecución y, mientras falte, cada prueba
/// falla diciendo qué tarea lo aporta. Contrato mínimo: un único constructor público con dependencias de Core y
/// <c>RegistrarAsync(string nombreCompleto, string identificadorAcceso, Guid rolId, CancellationToken)</c> que devuelve
/// el <see cref="Usuario"/> creado.
/// </remarks>
public sealed class RegistrarUsuarioTests
{
    private static readonly Guid EmpresaA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid Actor = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Ahora = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    private static readonly RolReferencia RolVentas =
        new(Guid.Parse("a1000000-0000-0000-0000-000000000003"), EmpresaA, "Ventas", []);

    /// <summary>Rol de otra empresa: la consulta de la empresa actual no lo encuentra.</summary>
    private static readonly Guid RolDeOtraEmpresa = Guid.Parse("b1000000-0000-0000-0000-000000000002");

    private static readonly OpcionesUsuarios Opciones = new()
    {
        Validacion = new() { LongitudMaximaNombre = 50, LongitudMaximaIdentificador = 30 },
    };

    // ---- Estado inicial (DEP-2) ----

    [Theory]
    [InlineData(EstadoUsuario.Activo)]
    [InlineData(EstadoUsuario.Inactivo)]
    public async Task ElEstadoInicial_EsElQueInformaLaPoliticaDeAlta(EstadoUsuario estado)
    {
        var dobles = new Dobles { EstadoInicial = estado };

        var usuario = await RegistrarAsync(dobles, "  Ana Pérez  ", "Ana.Perez", RolVentas.Id);

        Assert.Equal(estado, usuario.Estado);
        var alta = Assert.Single(dobles.AltasConsultadas);
        Assert.Equal(new DatosAltaUsuario("Ana Pérez", "Ana.Perez", RolVentas.Id), alta);
    }

    // ---- Empresa del contexto, transacción, credencial y auditoría ----

    [Fact]
    public async Task AltaCorrecta_EnUnaTransaccion_ConEmpresaDelContexto_CredencialYAuditoriaAlta()
    {
        var dobles = new Dobles();

        var usuario = await RegistrarAsync(dobles, "Ana Pérez", " Ana.Perez ", RolVentas.Id);

        Assert.Equal(EmpresaA, usuario.EmpresaId);
        Assert.Equal("Ana Pérez", usuario.NombreCompleto);

        // El identificador se guarda tal cual: sin recortar ni cambiar mayúsculas (DEP-3 no decidida).
        Assert.Equal(" Ana.Perez ", usuario.IdentificadorAcceso);
        Assert.Equal(RolVentas.Id, usuario.RolId);
        Assert.Same(usuario, Assert.Single(dobles.Agregados));
        Assert.Same(usuario, Assert.Single(dobles.CredencialesGestionadas));

        Assert.Equal(
            ["Iniciar", "ValidarFormato", "EstaEnUso", "ConsultarLimite", "EstadoInicial", "Agregar", "Credencial", "Auditar", "Confirmar"],
            dobles.Pasos);

        var registro = Assert.Single(dobles.Auditorias);
        Assert.Equal(TipoOperacion.Alta, registro.TipoOperacion);
        Assert.Equal(EmpresaA, registro.EmpresaId);
        Assert.Equal(Actor, registro.ActorUsuarioId);
        Assert.Equal(usuario.Id, registro.UsuarioAfectadoId);
        Assert.Equal(Ahora, registro.OcurridoEn);
        Assert.Null(registro.OperacionSolicitada);
        Assert.Null(registro.CambiosJson);
    }

    // ---- Formato y unicidad del identificador (DEP-3) ----

    [Fact]
    public async Task FormatoInvalidoSegunLaPolitica_EsErrorDelCampoIdentificador_ConSusMensajes()
    {
        var dobles = new Dobles { ProblemasDeFormato = ["Problema de formato A.", "Problema de formato B."] };

        var error = await Assert.ThrowsAsync<ErrorUsuarios>(Registro(dobles, "Ana", "ana", RolVentas.Id));

        Assert.Equal(CodigoErrorUsuarios.Validacion, error.Codigo);
        Assert.Equal([ValidadorUsuario.CampoIdentificadorAcceso], error.Errores.Keys);
        Assert.Equal(["Problema de formato A.", "Problema de formato B."], error.Errores[ValidadorUsuario.CampoIdentificadorAcceso]);
        SinAlta(dobles);
    }

    [Fact]
    public async Task IdentificadorEnUso_EsIdentificadorEnUso_SinCrearNiAuditar()
    {
        var dobles = new Dobles { EnUso = true };

        var error = await Assert.ThrowsAsync<ErrorUsuarios>(Registro(dobles, "Ana", "ana", RolVentas.Id));

        Assert.Equal(CodigoErrorUsuarios.IdentificadorEnUso, error.Codigo);
        Assert.Empty(error.Errores);

        // La política solo responde sí o no: el error no puede llevar datos de otra empresa ni del otro usuario.
        Assert.DoesNotContain("ana", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("empresa", error.Message, StringComparison.OrdinalIgnoreCase);
        SinAlta(dobles);
    }

    // ---- Límite de usuarios (DEP-5) ----

    [Fact]
    public async Task LimiteAlcanzado_EsLimiteUsuariosAlcanzado_SinCrearNada()
    {
        var dobles = new Dobles { Limite = SituacionLimiteUsuarios.LimiteAlcanzado };

        var error = await Assert.ThrowsAsync<ErrorUsuarios>(Registro(dobles, "Ana", "ana", RolVentas.Id));

        Assert.Equal(CodigoErrorUsuarios.LimiteUsuariosAlcanzado, error.Codigo);
        Assert.Empty(dobles.AltasConsultadas);
        SinAlta(dobles);
    }

    [Theory]
    [InlineData(SituacionLimiteUsuarios.SinLimiteAplicable)]
    [InlineData(SituacionLimiteUsuarios.LimiteNoAlcanzado)]
    public async Task SinLimiteAlcanzado_NoSeRechazaPorCantidad(SituacionLimiteUsuarios situacion)
    {
        var dobles = new Dobles { Limite = situacion };

        var usuario = await RegistrarAsync(dobles, "Ana", "ana", RolVentas.Id);

        Assert.Same(usuario, Assert.Single(dobles.Agregados));
        Assert.Equal("Confirmar", dobles.Pasos[^1]);
    }

    // ---- Datos y rol (ValidadorUsuario, T078) ----

    [Fact]
    public async Task RolDeOtraEmpresa_EsRolNoValido_SinConsultarLasIntegraciones()
    {
        var dobles = new Dobles();

        var error = await Assert.ThrowsAsync<ErrorUsuarios>(Registro(dobles, "Ana", "ana", RolDeOtraEmpresa));

        Assert.Equal(CodigoErrorUsuarios.RolNoValido, error.Codigo);
        Assert.Equal(["Iniciar", "Revertir"], dobles.Pasos);
        SinAlta(dobles);
    }

    [Theory]
    [InlineData("", "ana", "nombreCompleto")]
    [InlineData("Ana", " ", "identificadorAcceso")]
    [InlineData("Ana", "ana", "rolId")]
    public async Task DatosInvalidos_SonErroresPorCampo(string nombre, string identificador, string campo)
    {
        var dobles = new Dobles();
        var rol = campo == "rolId" ? Guid.Empty : RolVentas.Id;

        var error = await Assert.ThrowsAsync<ErrorUsuarios>(Registro(dobles, nombre, identificador, rol));

        Assert.Equal(CodigoErrorUsuarios.Validacion, error.Codigo);
        Assert.Equal([campo], error.Errores.Keys);
        Assert.Equal(["Iniciar", "Revertir"], dobles.Pasos);
        SinAlta(dobles);
    }

    /// <summary>Un fallo después de agregar (aquí, en la credencial) revierte todo: no queda un alta parcial.</summary>
    [Fact]
    public async Task FalloAlGestionarLaCredencial_RevierteSinAuditoria()
    {
        var dobles = new Dobles { FallaCredencial = true };

        await Assert.ThrowsAsync<InvalidOperationException>(Registro(dobles, "Ana", "ana", RolVentas.Id));

        Assert.Empty(dobles.Auditorias);
        Assert.DoesNotContain("Confirmar", dobles.Pasos);
        Assert.Equal("Revertir", dobles.Pasos[^1]);
    }

    private static void SinAlta(Dobles dobles)
    {
        Assert.Empty(dobles.Agregados);
        Assert.Empty(dobles.CredencialesGestionadas);
        Assert.Empty(dobles.Auditorias);
        Assert.DoesNotContain("Confirmar", dobles.Pasos);
        Assert.Equal("Revertir", dobles.Pasos[^1]);
    }

    private static async Task<Usuario> RegistrarAsync(Dobles dobles, string nombre, string identificador, Guid rolId)
    {
        Usuario? usuario = null;
        await Registro(dobles, nombre, identificador, rolId, u => usuario = u)();
        return usuario!;
    }

    /// <summary>
    /// Prepara el registro: localiza <c>RegistrarUsuario</c> ya, fuera de <c>Assert.ThrowsAsync</c>, para que su ausencia
    /// se informe como tal y no como una excepción de otro tipo.
    /// </summary>
    private static Func<Task> Registro(Dobles dobles, string nombre, string identificador, Guid rolId, Action<Usuario>? alTerminar = null)
    {
        var tipo = typeof(Usuario).Assembly.GetType("Kryon.Core.Usuarios.RegistrarUsuario")
            ?? throw new Xunit.Sdk.XunitException("RegistrarUsuario aún no está implementado (T087).");
        var constructor = Assert.Single(tipo.GetConstructors());
        var caso = constructor.Invoke([.. constructor.GetParameters().Select(p => dobles.Para(p.ParameterType))]);
        var registrar = tipo.GetMethod("RegistrarAsync", [typeof(string), typeof(string), typeof(Guid), typeof(CancellationToken)])
            ?? throw new Xunit.Sdk.XunitException("RegistrarUsuario no tiene RegistrarAsync(string, string, Guid, CancellationToken) (T087).");

        return async () =>
        {
            try
            {
                // Se espera siempre: con alTerminar nulo, "?.Invoke(await …)" no evaluaría el argumento.
                var usuario = await (Task<Usuario>)registrar.Invoke(caso, [nombre, identificador, rolId, CancellationToken.None])!;
                alTerminar?.Invoke(usuario);
            }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            {
                throw e.InnerException;
            }
        };
    }

    /// <summary>Reloj fijo para comprobar el instante de la auditoría.</summary>
    private sealed class RelojFijo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    /// <summary>
    /// Dobles de Core en memoria para la empresa A: transacción, repositorio, auditoría, consulta de roles y los tres
    /// puntos de integración. Registra el orden de los pasos.
    /// </summary>
    private sealed class Dobles
        : IUnidadDeTrabajo, IRepositorioUsuarios, IRegistroAuditoria, IConsultaUsuarios, IContextoSolicitud,
          IPoliticaAltaUsuario, IPoliticaIdentificadorAcceso, IFuenteLimiteUsuarios
    {
        public EstadoUsuario EstadoInicial { get; init; } = EstadoUsuario.Activo;

        public IReadOnlyList<string> ProblemasDeFormato { get; init; } = [];

        public bool EnUso { get; init; }

        public SituacionLimiteUsuarios Limite { get; init; } = SituacionLimiteUsuarios.SinLimiteAplicable;

        public bool FallaCredencial { get; init; }

        public List<string> Pasos { get; } = [];

        public List<Usuario> Agregados { get; } = [];

        public List<Usuario> CredencialesGestionadas { get; } = [];

        public List<DatosAltaUsuario> AltasConsultadas { get; } = [];

        public List<AuditoriaUsuario> Auditorias { get; } = [];

        public Guid UsuarioId => Actor;

        public Guid EmpresaId => EmpresaA;

        public IReadOnlyCollection<string> Capacidades => ["usuarios.registrar"];

        /// <summary>DEP-4 no interviene en el registro: si se consultara, la prueba fallaría.</summary>
        public bool PermiteEdicion => throw new InvalidOperationException("El registro no debe consultar PermiteEdicion (DEP-4).");

        public object Para(Type tipo) => tipo switch
        {
            _ when tipo == typeof(TimeProvider) => new RelojFijo(),
            _ when tipo == typeof(ValidadorUsuario) => new ValidadorUsuario(Opciones, this),
            _ when tipo.IsInstanceOfType(this) => this,
            _ => throw new Xunit.Sdk.XunitException($"Dependencia de RegistrarUsuario sin doble en T085: {tipo}."),
        };

        public Task IniciarAsync(CancellationToken cancellationToken) => Paso("Iniciar");

        public Task ConfirmarAsync(CancellationToken cancellationToken) => Paso("Confirmar");

        public Task RevertirAsync(CancellationToken cancellationToken)
        {
            Pasos.Add("Revertir");
            Agregados.Clear();
            Auditorias.Clear();
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> ValidarFormato(string identificadorAcceso)
        {
            Pasos.Add("ValidarFormato");
            return ProblemasDeFormato;
        }

        public Task<bool> EstaEnUsoAsync(string identificadorAcceso, CancellationToken cancellationToken)
        {
            Pasos.Add("EstaEnUso");
            return Task.FromResult(EnUso);
        }

        public Task<SituacionLimiteUsuarios> ConsultarAsync(CancellationToken cancellationToken)
        {
            Pasos.Add("ConsultarLimite");
            return Task.FromResult(Limite);
        }

        public Task<EstadoUsuario> ObtenerEstadoInicialAsync(DatosAltaUsuario datos, CancellationToken cancellationToken)
        {
            Pasos.Add("EstadoInicial");
            AltasConsultadas.Add(datos);
            return Task.FromResult(EstadoInicial);
        }

        public Task GestionarCredencialInicialAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            Pasos.Add("Credencial");
            if (FallaCredencial)
            {
                throw new InvalidOperationException("Fallo simulado al gestionar la credencial.");
            }

            CredencialesGestionadas.Add(usuario);
            return Task.CompletedTask;
        }

        public Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            Pasos.Add("Agregar");
            Agregados.Add(usuario);
            return Task.CompletedTask;
        }

        public Task RegistrarAsync(AuditoriaUsuario registro, CancellationToken cancellationToken)
        {
            Pasos.Add("Auditar");
            Auditorias.Add(registro);
            return Task.CompletedTask;
        }

        public Task<RolReferencia?> ObtenerRolAsync(Guid rolId, CancellationToken cancellationToken) =>
            Task.FromResult(rolId == RolVentas.Id ? RolVentas : null);

        Task<Usuario?> IRepositorioUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("El registro no carga usuarios existentes.");

        public Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("El registro no cuenta administradores.");

        public Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("El registro no actualiza usuarios.");

        public Task<PaginaConsultaUsuarios> BuscarAsync(CriteriosConsultaUsuarios criterios, CancellationToken cancellationToken) =>
            throw new InvalidOperationException();

        Task<UsuarioConRol?> IConsultaUsuarios.ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException();

        public Task<IReadOnlyList<RolReferencia>> ListarRolesAsignablesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException();

        private Task Paso(string paso)
        {
            Pasos.Add(paso);
            return Task.CompletedTask;
        }
    }
}
