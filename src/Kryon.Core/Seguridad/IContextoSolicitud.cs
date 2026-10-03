namespace Kryon.Core.Seguridad;

/// <summary>
/// Contexto confiable de la solicitud: identifica al usuario, su empresa y sus capacidades.
/// Es la única fuente de empresa y actor; nunca se construye a partir de datos enviados por el cliente.
/// </summary>
public interface IContextoSolicitud
{
    /// <summary>Identificador del usuario que realiza la solicitud.</summary>
    Guid UsuarioId { get; }

    /// <summary>Identificador de la empresa del usuario que realiza la solicitud.</summary>
    Guid EmpresaId { get; }

    /// <summary>Nombres de las capacidades (permisos) del usuario en su empresa.</summary>
    IReadOnlyCollection<string> Capacidades { get; }
}
