namespace Kryon.Contracts.Usuarios;

/// <summary>Esquema <c>PaginaUsuarios</c> del contrato.</summary>
public sealed record PaginaUsuarios
{
    public required IReadOnlyList<UsuarioResumen> Elementos { get; init; }

    /// <summary>Solo usuarios de la empresa actual que cumplen los criterios.</summary>
    public required int Total { get; init; }

    public required int Pagina { get; init; }

    public required int TamanoPagina { get; init; }

    /// <summary>Capacidad de registrar (nombre provisional <c>usuarios.registrar</c>; FR-020, US5).</summary>
    public required bool PuedeRegistrar { get; init; }
}
