using System.Text.Json.Serialization;

namespace Kryon.Contracts.Usuarios;

/// <summary>
/// Esquema <c>EditarUsuario</c> del contrato (<c>additionalProperties: false</c>). No tiene <c>empresaId</c>:
/// la empresa sale siempre del contexto de la solicitud.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditarUsuarioSolicitud
{
    public required string NombreCompleto { get; init; }

    public required Guid RolId { get; init; }

    /// <summary>
    /// Opcional. Previsto para cuando DEP-4 se resuelva; se acepta o rechaza según esa regla. Si falta, no se envía.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdentificadorAcceso { get; init; }
}
