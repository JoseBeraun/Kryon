using System.Reflection;

using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;
using Kryon.Core.Usuarios.Eventos;

namespace Kryon.Core.Tests.Usuarios;

/// <summary>
/// Reglas de cambio de estado (FR-034, FR-036, EC-2; data-model.md → <i>Transiciones de estado</i>) y publicación de
/// <see cref="UsuarioDesactivado"/> (DEP-1, T065).
/// </summary>
/// <remarks>
/// Test-first: <c>Usuario.Desactivar</c>/<c>Activar</c> (T061), <c>ReglaUltimoAdministrador</c> (T062) y
/// <c>CambiarEstadoUsuario</c> (T065) aún no existen, así que se localizan en tiempo de ejecución para que el proyecto
/// compile; mientras falten, cada prueba falla diciendo qué tarea lo aporta. Contrato mínimo que fijan estas pruebas:
/// <list type="bullet">
/// <item><c>Usuario.Desactivar(Guid actorUsuarioId)</c> y <c>Usuario.Activar()</c>: lanzan <see cref="ErrorUsuarios"/>
/// (<c>no-puede-desactivarse-a-si-mismo</c> sobre la propia cuenta; <c>usuario-modificado</c> si el usuario no está en
/// el estado de origen, data-model.md precondición 5 / EC-2).</item>
/// <item><c>ReglaUltimoAdministrador.Verificar(int administradoresActivosRestantes)</c>: lanza
/// <see cref="ErrorUsuarios"/> <c>ultimo-administrador</c> si el cambio deja cero.</item>
/// <item><c>CambiarEstadoUsuario</c>: un único constructor público con dependencias de Core y un método público
/// <c>DesactivarAsync</c> cuyos parámetros son el id del usuario, su versión (<c>byte[]</c>) y el token.</item>
/// </list>
/// </remarks>
public sealed class ReglasEstadoTests
{
    private static readonly Guid EmpresaA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid RolAdministracion = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid Actor = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Momento = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Desactivar_LaPropiaCuenta_SeRechaza()
    {
        var propia = NuevoUsuario(EstadoUsuario.Activo, Actor);

        var desactivar = Metodo("Desactivar", typeof(Guid));

        var error = Assert.Throws<ErrorUsuarios>(() => Invocar(desactivar, propia, Actor));

        Assert.Equal(CodigoErrorUsuarios.NoPuedeDesactivarseASiMismo, error.Codigo);
        Assert.Equal(EstadoUsuario.Activo, propia.Estado);
    }

    [Fact]
    public void Desactivar_UnUsuarioYaInactivo_SeRechaza()
    {
        var inactivo = NuevoUsuario(EstadoUsuario.Inactivo);

        var desactivar = Metodo("Desactivar", typeof(Guid));

        var error = Assert.Throws<ErrorUsuarios>(() => Invocar(desactivar, inactivo, Actor));

        Assert.Equal(CodigoErrorUsuarios.UsuarioModificado, error.Codigo);
        Assert.Equal(EstadoUsuario.Inactivo, inactivo.Estado);
    }

    [Fact]
    public void Activar_UnUsuarioYaActivo_SeRechaza()
    {
        var activo = NuevoUsuario(EstadoUsuario.Activo);

        var activar = Metodo("Activar");

        var error = Assert.Throws<ErrorUsuarios>(() => Invocar(activar, activo));

        Assert.Equal(CodigoErrorUsuarios.UsuarioModificado, error.Codigo);
        Assert.Equal(EstadoUsuario.Activo, activo.Estado);
    }

    [Fact]
    public void Desactivar_OtroUsuarioActivo_LoDejaInactivo_YActivarLoDevuelveAActivo()
    {
        var otro = NuevoUsuario(EstadoUsuario.Activo);

        Invocar(Metodo("Desactivar", typeof(Guid)), otro, Actor);
        Assert.Equal(EstadoUsuario.Inactivo, otro.Estado);

        Invocar(Metodo("Activar"), otro);
        Assert.Equal(EstadoUsuario.Activo, otro.Estado);
    }

    [Fact]
    public void ReglaUltimoAdministrador_RechazaDejarCeroAdministradoresActivos()
    {
        var verificar = MetodoEstatico("ReglaUltimoAdministrador", "Verificar", "T062");

        var error = Assert.Throws<ErrorUsuarios>(() => InvocarEstatico(verificar, 0));
        Assert.Equal(CodigoErrorUsuarios.UltimoAdministrador, error.Codigo);

        // Si queda al menos uno, el cambio se admite.
        InvocarEstatico(verificar, 1);
    }

    [Fact]
    public async Task Desactivar_PublicaUsuarioDesactivado_DespuesDeConfirmar()
    {
        var tipo = TipoDeCore("CambiarEstadoUsuario", "T065");
        var afectado = NuevoUsuario(EstadoUsuario.Activo);
        var dobles = new Dobles(afectado);

        var constructor = Assert.Single(tipo.GetConstructors());
        var caso = constructor.Invoke([.. constructor.GetParameters().Select(p => dobles.Para(p.ParameterType))]);

        var desactivar = tipo.GetMethod("DesactivarAsync")
            ?? throw new Xunit.Sdk.XunitException("CambiarEstadoUsuario no tiene DesactivarAsync (T065).");
        var argumentos = desactivar.GetParameters().Select(p => p.ParameterType switch
        {
            var t when t == typeof(Guid) => (object)afectado.Id,
            var t when t == typeof(byte[]) => afectado.Version,
            var t when t == typeof(CancellationToken) => CancellationToken.None,
            var t => throw new Xunit.Sdk.XunitException($"Parámetro de DesactivarAsync no previsto por el contrato de T055: {t}."),
        });
        await (Task)desactivar.Invoke(caso, [.. argumentos])!;

        var evento = Assert.Single(dobles.Publicados);
        Assert.Equal(afectado.Id, evento.UsuarioId);
        Assert.Equal(EmpresaA, evento.EmpresaId);
        Assert.Equal(TimeSpan.Zero, evento.OcurridoEn.Offset);
        Assert.Equal(EstadoUsuario.Inactivo, afectado.Estado);

        // Se publica solo una desactivación ya confirmada (T065: confirmar → publicar).
        Assert.Equal(["Iniciar", "Confirmar", "Publicar"], dobles.Pasos.Where(p => p is "Iniciar" or "Confirmar" or "Revertir" or "Publicar"));
    }

    private static Usuario NuevoUsuario(EstadoUsuario estado, Guid? id = null)
    {
        var usuario = new Usuario(EmpresaA, "Usuario de prueba", "usuario.prueba", RolAdministracion, estado, Momento);
        if (id is { } fijo)
        {
            typeof(Usuario).GetProperty(nameof(Usuario.Id))!.SetValue(usuario, fijo);
        }

        typeof(Usuario).GetProperty(nameof(Usuario.Version))!.SetValue(usuario, new byte[] { 0, 0, 0, 0, 0, 0, 7, 209 });
        return usuario;
    }

    /// <summary>Método de instancia de <see cref="Usuario"/> que aporta T061.</summary>
    private static MethodInfo Metodo(string nombre, params Type[] parametros) =>
        typeof(Usuario).GetMethod(nombre, BindingFlags.Public | BindingFlags.Instance, parametros)
        ?? throw new Xunit.Sdk.XunitException($"Usuario.{nombre} aún no está implementado (T061).");

    /// <summary>Invoca el método y desenvuelve su excepción para que la prueba vea la de dominio.</summary>
    private static void Invocar(MethodInfo metodo, Usuario usuario, params object[] argumentos)
    {
        try
        {
            metodo.Invoke(usuario, argumentos);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }

    private static Type TipoDeCore(string nombre, string tarea) =>
        typeof(Usuario).Assembly.GetType($"Kryon.Core.Usuarios.{nombre}")
        ?? throw new Xunit.Sdk.XunitException($"{nombre} aún no está implementado ({tarea}).");

    private static MethodInfo MetodoEstatico(string tipo, string nombre, string tarea) =>
        TipoDeCore(tipo, tarea).GetMethod(nombre, BindingFlags.Public | BindingFlags.Static, [typeof(int)])
        ?? throw new Xunit.Sdk.XunitException($"{tipo}.{nombre}(int) aún no está implementado ({tarea}).");

    private static void InvocarEstatico(MethodInfo metodo, params object[] argumentos)
    {
        try
        {
            metodo.Invoke(null, argumentos);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }

    /// <summary>
    /// Dobles de las dependencias de Core del caso de uso: un usuario cargado, dos administradores activos (la regla no
    /// bloquea), y registro del orden de transacción y publicación.
    /// </summary>
    private sealed class Dobles(Usuario usuario)
        : IUnidadDeTrabajo, IRepositorioUsuarios, IBloqueoAdministradoresEmpresa, IRegistroAuditoria, IPublicadorEventos, IContextoSolicitud
    {
        public List<string> Pasos { get; } = [];

        public List<UsuarioDesactivado> Publicados { get; } = [];

        public Guid UsuarioId => Actor;

        public Guid EmpresaId => EmpresaA;

        public IReadOnlyCollection<string> Capacidades => ["usuarios.administrar", "usuarios.desactivar", "usuarios.activar"];

        public object Para(Type tipo) => tipo switch
        {
            _ when tipo == typeof(TimeProvider) => TimeProvider.System,
            _ when tipo.IsInstanceOfType(this) => this,
            _ => throw new Xunit.Sdk.XunitException($"Dependencia de CambiarEstadoUsuario sin doble en T055: {tipo}."),
        };

        public Task IniciarAsync(CancellationToken cancellationToken) => Paso("Iniciar");

        public Task ConfirmarAsync(CancellationToken cancellationToken) => Paso("Confirmar");

        public Task RevertirAsync(CancellationToken cancellationToken) => Paso("Revertir");

        public Task AdquirirAsync(CancellationToken cancellationToken) => Paso("Bloquear");

        public Task<Usuario?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            Task.FromResult(usuarioId == usuario.Id ? usuario : null);

        public Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken) => Task.FromResult(2);

        public Task AgregarAsync(Usuario nuevo, CancellationToken cancellationToken) => throw new InvalidOperationException();

        public Task ActualizarAsync(Usuario actualizado, CancellationToken cancellationToken) => Paso("Guardar");

        public Task RegistrarAsync(AuditoriaUsuario registro, CancellationToken cancellationToken) => Paso("Auditar");

        public Task PublicarAsync(UsuarioDesactivado evento, CancellationToken cancellationToken)
        {
            Publicados.Add(evento);
            return Paso("Publicar");
        }

        private Task Paso(string paso)
        {
            Pasos.Add(paso);
            return Task.CompletedTask;
        }
    }
}
