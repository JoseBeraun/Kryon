namespace Kryon.Contracts.Usuarios;

/// <summary>Esquema <c>RolResumen</c> del contrato.</summary>
public sealed record RolResumen
{
    public required Guid Id { get; init; }

    public required string Nombre { get; init; }
}
