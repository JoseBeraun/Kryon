namespace Kryon.Core.Usuarios.Integraciones;

/// <summary>
/// Punto de integración de DEP-5 (decisión comercial). Informa si la empresa del contexto tiene un límite de
/// usuarios aplicable y si se alcanzó.
/// </summary>
/// <remarks>
/// Esta feature solo aplica la regla de FR-028: si existe un límite aplicable y se alcanzó, el registro se rechaza.
/// La existencia del límite, su cifra, a quién aplica y cómo se cuenta (por ejemplo, inactivos o reactivaciones) se
/// deciden fuera de esta feature. No hay valor sustituto: sin una implementación registrada, el registro no se entrega.
/// </remarks>
public interface IFuenteLimiteUsuarios
{
    /// <summary>Consulta la situación del límite para la empresa del contexto verificado.</summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<SituacionLimiteUsuarios> ConsultarAsync(CancellationToken cancellationToken);
}

/// <summary>Situación del límite de usuarios que informa la fuente de DEP-5.</summary>
public enum SituacionLimiteUsuarios
{
    /// <summary>No existe un límite aplicable a la empresa: no se rechaza por cantidad (EC-12).</summary>
    SinLimiteAplicable = 1,

    /// <summary>Existe un límite aplicable y todavía no se alcanzó.</summary>
    LimiteNoAlcanzado = 2,

    /// <summary>Existe un límite aplicable y se alcanzó: el registro se rechaza (FR-028).</summary>
    LimiteAlcanzado = 3,
}
