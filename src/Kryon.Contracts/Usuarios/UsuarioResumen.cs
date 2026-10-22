namespace Kryon.Contracts.Usuarios;

/// <summary>Esquema <c>UsuarioResumen</c> del contrato.</summary>
public record UsuarioResumen
{
    public required Guid Id { get; init; }

    public required string NombreCompleto { get; init; }

    public required string IdentificadorAcceso { get; init; }

    /// <summary>Siempre presente; null si el usuario no tiene un rol reconocido (EC-10).</summary>
    public required RolResumen? Rol { get; init; }

    /// <summary>Esquema <c>EstadoUsuario</c>: <c>activo</c> o <c>inactivo</c>. Los estados adicionales los define DEP-2.</summary>
    public required string Estado { get; init; }

    public required bool EsCuentaPropia { get; init; }

    /// <summary>Mismo valor que el ETag.</summary>
    public required string Version { get; init; }

    public required AccionesPermitidas Acciones { get; init; }
}
