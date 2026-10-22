using System.Text.Json.Serialization;

namespace Kryon.Contracts.Usuarios;

/// <summary>
/// Esquemas <c>Problema</c>, <c>ProblemaValidacion</c> y <c>ProblemaConflictoVersion</c> del contrato
/// (application/problem+json). <see cref="Errors"/> y <see cref="Actual"/> solo aparecen en sus variantes.
/// </summary>
public sealed record Problema
{
    public required string Type { get; init; }

    public required string Title { get; init; }

    public required int Status { get; init; }

    /// <summary>Uno de los valores de <see cref="CodigosError"/>.</summary>
    public required string Codigo { get; init; }

    /// <summary>Solo en <c>ProblemaValidacion</c> (<c>validacion</c>): errores por campo.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>Solo en <c>ProblemaConflictoVersion</c> (<c>usuario-modificado</c>): datos vigentes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DetalleUsuario? Actual { get; init; }
}
