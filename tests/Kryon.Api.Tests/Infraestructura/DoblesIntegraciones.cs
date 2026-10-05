using Kryon.Core.Usuarios;
using Kryon.Core.Usuarios.Integraciones;

namespace Kryon.Api.Tests.Infraestructura;

// Dobles de prueba de los puntos de integración de DEP-2, DEP-3, DEP-4 y DEP-5 (quickstart.md, prerrequisitos).
// NO representan las reglas reales: el estado inicial, la credencial, el formato y la unicidad del identificador, si
// es editable y el límite de usuarios los definirán la futura spec de autenticación y la decisión comercial. Cada
// prueba elige la respuesta; un doble sin configurar lanza una excepción en vez de suponer un valor.

/// <summary>Doble de <see cref="IPoliticaAltaUsuario"/> (DEP-2).</summary>
public sealed class PoliticaAltaUsuarioDoble : IPoliticaAltaUsuario
{
    private readonly List<DatosAltaUsuario> altasConsultadas = [];
    private readonly List<Usuario> credencialesGestionadas = [];

    /// <summary>Estado que la prueba quiere que devuelva el alta; obligatorio si la prueba registra usuarios.</summary>
    public EstadoUsuario? EstadoInicial { get; set; }

    /// <summary>Datos de cada alta para la que se pidió el estado inicial.</summary>
    public IReadOnlyList<DatosAltaUsuario> AltasConsultadas => altasConsultadas;

    /// <summary>Usuarios cuya credencial inicial se pidió gestionar (el doble no hace nada más con ellos).</summary>
    public IReadOnlyList<Usuario> CredencialesGestionadas => credencialesGestionadas;

    public Task<EstadoUsuario> ObtenerEstadoInicialAsync(DatosAltaUsuario datos, CancellationToken cancellationToken)
    {
        altasConsultadas.Add(datos);
        return Task.FromResult(EstadoInicial ?? throw SinConfigurar.Error(nameof(EstadoInicial)));
    }

    public Task GestionarCredencialInicialAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        credencialesGestionadas.Add(usuario);
        return Task.CompletedTask;
    }
}

/// <summary>Doble de <see cref="IPoliticaIdentificadorAcceso"/> (DEP-3 y DEP-4).</summary>
public sealed class PoliticaIdentificadorAccesoDoble : IPoliticaIdentificadorAcceso
{
    private readonly List<string> formatosValidados = [];
    private readonly List<string> usosConsultados = [];

    /// <summary>DEP-4: si el identificador puede editarse.</summary>
    public bool? PermiteEdicionConfigurado { get; set; }

    /// <summary>DEP-3: problemas de formato que se devuelven; vacío para un formato válido.</summary>
    public IReadOnlyList<string>? ProblemasDeFormato { get; set; }

    /// <summary>DEP-3: si el identificador está en uso.</summary>
    public bool? EnUso { get; set; }

    public IReadOnlyList<string> FormatosValidados => formatosValidados;

    public IReadOnlyList<string> UsosConsultados => usosConsultados;

    public bool PermiteEdicion => PermiteEdicionConfigurado ?? throw SinConfigurar.Error(nameof(PermiteEdicionConfigurado));

    public IReadOnlyList<string> ValidarFormato(string identificadorAcceso)
    {
        formatosValidados.Add(identificadorAcceso);
        return ProblemasDeFormato ?? throw SinConfigurar.Error(nameof(ProblemasDeFormato));
    }

    public Task<bool> EstaEnUsoAsync(string identificadorAcceso, CancellationToken cancellationToken)
    {
        usosConsultados.Add(identificadorAcceso);
        return Task.FromResult(EnUso ?? throw SinConfigurar.Error(nameof(EnUso)));
    }
}

/// <summary>Doble de <see cref="IFuenteLimiteUsuarios"/> (DEP-5).</summary>
public sealed class FuenteLimiteUsuariosDoble : IFuenteLimiteUsuarios
{
    /// <summary>Situación del límite que se devuelve.</summary>
    public SituacionLimiteUsuarios? Situacion { get; set; }

    public int Consultas { get; private set; }

    public Task<SituacionLimiteUsuarios> ConsultarAsync(CancellationToken cancellationToken)
    {
        Consultas++;
        return Task.FromResult(Situacion ?? throw SinConfigurar.Error(nameof(Situacion)));
    }
}

internal static class SinConfigurar
{
    public static InvalidOperationException Error(string propiedad) =>
        new($"El doble de prueba no tiene configurado '{propiedad}': la prueba debe elegir la respuesta (no hay regla real).");
}
