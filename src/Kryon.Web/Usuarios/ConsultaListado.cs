namespace Kryon.Web.Usuarios;

/// <summary>
/// Query string del listado (<c>busqueda</c>, <c>rolId</c>, <c>estado</c>, <c>pagina</c>, <c>tamanoPagina</c>), para
/// llevarla al detalle y devolverla con "Volver al listado" sin perder la búsqueda, los filtros ni la página
/// (FR-018, FR-022, US3-2). Solo copia esos parámetros; los vacíos se omiten.
/// </summary>
public static class ConsultaListado
{
    /// <summary>La query string (<c>?…</c>) con los criterios no vacíos, o vacía si no hay ninguno.</summary>
    public static string Construir(string? busqueda, string? rolId, string? estado, string? pagina, string? tamanoPagina)
    {
        var parametros = new (string Nombre, string? Valor)[]
        {
            ("busqueda", busqueda),
            ("rolId", rolId),
            ("estado", estado),
            ("pagina", pagina),
            ("tamanoPagina", tamanoPagina),
        };

        var presentes = parametros
            .Where(p => !string.IsNullOrEmpty(p.Valor))
            .Select(p => $"{p.Nombre}={Uri.EscapeDataString(p.Valor!)}")
            .ToArray();
        return presentes.Length == 0 ? string.Empty : "?" + string.Join('&', presentes);
    }
}
