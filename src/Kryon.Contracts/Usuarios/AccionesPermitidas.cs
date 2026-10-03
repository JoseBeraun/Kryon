namespace Kryon.Contracts.Usuarios;

/// <summary>Esquema <c>AccionesPermitidas</c> del contrato.</summary>
public sealed record AccionesPermitidas
{
    public required bool VerDetalle { get; init; }

    public required bool Editar { get; init; }

    /// <summary>false para la propia cuenta (FR-035).</summary>
    public required bool CambiarRol { get; init; }

    /// <summary>Lo determina la regla de DEP-4 (futura spec de autenticación); este contrato no fija su valor.</summary>
    public required bool EditarIdentificador { get; init; }

    public required bool Activar { get; init; }

    /// <summary>false para la propia cuenta (FR-034).</summary>
    public required bool Desactivar { get; init; }
}
