namespace Kryon.Core.Usuarios.Integraciones;

/// <summary>
/// Punto de integración de DEP-3 (formato y unicidad del identificador de acceso) y DEP-4 (si es editable), que
/// definirá la futura spec de autenticación.
/// </summary>
/// <remarks>
/// Esta feature no decide el formato, el ámbito de la unicidad (por empresa o en todo Kryon) ni la editabilidad.
/// No tiene valores sustitutos: sin una implementación registrada, el registro y la edición del identificador no
/// se entregan.
/// </remarks>
public interface IPoliticaIdentificadorAcceso
{
    /// <summary>Indica si el identificador puede editarse después del registro (DEP-4).</summary>
    bool PermiteEdicion { get; }

    /// <summary>Valida el formato del identificador según las reglas de DEP-3.</summary>
    /// <param name="identificadorAcceso">Identificador propuesto.</param>
    /// <returns>Los problemas de formato para mostrar junto al campo; vacía si el formato es válido.</returns>
    IReadOnlyList<string> ValidarFormato(string identificadorAcceso);

    /// <summary>Indica si el identificador ya está en uso, en el ámbito que defina DEP-3.</summary>
    /// <param name="identificadorAcceso">Identificador propuesto.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// Solo sí o no. La firma no permite revelar en qué empresa ni en qué usuario existe el identificador
    /// (FR-027, Principio I).
    /// </returns>
    Task<bool> EstaEnUsoAsync(string identificadorAcceso, CancellationToken cancellationToken);
}
