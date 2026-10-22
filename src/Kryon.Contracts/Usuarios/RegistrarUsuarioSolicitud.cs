using System.Text.Json.Serialization;

namespace Kryon.Contracts.Usuarios;

/// <summary>
/// Esquema <c>RegistrarUsuario</c> del contrato (<c>additionalProperties: false</c>). No tiene <c>empresaId</c>:
/// la empresa sale siempre del contexto de la solicitud.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegistrarUsuarioSolicitud
{
    public required string NombreCompleto { get; init; }

    /// <summary>Su formato depende de DEP-3.</summary>
    public required string IdentificadorAcceso { get; init; }

    public required Guid RolId { get; init; }
}
