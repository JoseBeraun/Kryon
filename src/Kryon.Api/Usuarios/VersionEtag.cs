namespace Kryon.Api.Usuarios;

/// <summary>
/// Representación HTTP de <c>Usuario.Version</c> (<c>rowversion</c>): un ETag fuerte con los bytes en Base64 entre
/// comillas, por ejemplo <c>"AAAAAAAAB9E="</c>. Es el mismo valor en el campo <c>version</c> del contrato y en los
/// encabezados <c>ETag</c> e <c>If-Match</c> (FR-038, FR-039).
/// </summary>
/// <remarks>
/// Es una decisión técnica de representación, no una regla de negocio: el valor es opaco para el cliente, que solo lo
/// devuelve tal cual. Es determinista (los mismos bytes dan siempre el mismo ETag).
/// </remarks>
public static class VersionEtag
{
    /// <summary>ETag de la versión de un usuario.</summary>
    /// <param name="version">Valor de <c>rowversion</c> leído de la base de datos.</param>
    public static string Desde(byte[] version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Length == 0)
        {
            // Un usuario leído de la base de datos siempre tiene versión: sin ella no se puede proteger la concurrencia.
            throw new InvalidOperationException("El usuario no tiene versión.");
        }

        return $"\"{Convert.ToBase64String(version)}\"";
    }

    /// <summary>
    /// Versión recibida en <c>If-Match</c>: la inversa de <see cref="Desde"/>. Un valor con otro formato (débil, varios
    /// ETag, <c>*</c> o Base64 inválido) no puede coincidir con ninguna versión: devuelve una versión vacía, que el caso
    /// de uso rechaza como <c>usuario-modificado</c>.
    /// </summary>
    /// <param name="ifMatch">Valor del encabezado, ya comprobado que existe.</param>
    public static byte[] Leer(string ifMatch)
    {
        var valor = ifMatch.Trim();
        if (valor.Length < 3 || valor[0] != '"' || valor[^1] != '"')
        {
            return [];
        }

        var bytes = new byte[valor.Length];
        return Convert.TryFromBase64String(valor[1..^1], bytes, out var escritos) ? bytes[..escritos] : [];
    }
}
