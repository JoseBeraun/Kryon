namespace Kryon.Web.Compartido;

/// <summary>
/// Mensaje de éxito que una página deja para la siguiente al navegar (por ejemplo, "Usuario {nombre} guardado." al
/// volver de la edición al listado). Vive solo en memoria de la sesión del navegador: no viaja en la query string ni se
/// guarda en el almacenamiento local, y se consume una sola vez.
/// </summary>
public sealed class AvisoNavegacion
{
    private string? pendiente;

    /// <summary>Deja el mensaje para la próxima página que lo consuma. Sustituye a uno anterior no consumido.</summary>
    public void Dejar(string mensaje)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mensaje);
        pendiente = mensaje;
    }

    /// <summary>Devuelve el mensaje pendiente, si hay, y lo borra.</summary>
    public string? Consumir()
    {
        var mensaje = pendiente;
        pendiente = null;
        return mensaje;
    }
}
