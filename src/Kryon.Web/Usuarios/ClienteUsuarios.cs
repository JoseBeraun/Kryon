using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Kryon.Contracts.Usuarios;

namespace Kryon.Web.Usuarios;

/// <summary>
/// Cliente tipado de <c>contracts/usuarios-api.openapi.yaml</c>: una operación pública por cada operación del
/// contrato, con los DTOs de <c>Kryon.Contracts</c>. Solo transporta: no aplica reglas de negocio, no elige la empresa
/// (la API la deriva de la identidad verificada) y no conoce ningún mecanismo de identidad; eso lo añade, si existe,
/// el <see cref="HttpClient"/> que recibe.
/// </summary>
/// <remarks>
/// Los errores HTTP no se lanzan: vuelven en <see cref="RespuestaApi{T}"/> con el ProblemDetails del contrato. Un
/// fallo sin respuesta (red, servidor inaccesible) o una cancelación sí se propagan como excepción
/// (<see cref="HttpRequestException"/>, <see cref="OperationCanceledException"/>).
/// </remarks>
public sealed class ClienteUsuarios(HttpClient http)
{
    private const string RutaUsuarios = "api/usuarios";

    /// <summary><c>listarUsuarios</c>: <c>GET /api/usuarios</c>. Los parámetros nulos no se envían.</summary>
    public Task<RespuestaApi<PaginaUsuarios>> ListarUsuariosAsync(
        string? busqueda,
        Guid? rolId,
        string? estado,
        int? pagina,
        int? tamanoPagina,
        CancellationToken cancellationToken)
    {
        var parametros = new List<string>();
        Agregar(parametros, "busqueda", busqueda);
        Agregar(parametros, "rolId", rolId?.ToString());
        Agregar(parametros, "estado", estado);
        Agregar(parametros, "pagina", pagina?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Agregar(parametros, "tamanoPagina", tamanoPagina?.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var ruta = parametros.Count == 0 ? RutaUsuarios : $"{RutaUsuarios}?{string.Join('&', parametros)}";
        return EnviarAsync<PaginaUsuarios>(new HttpRequestMessage(HttpMethod.Get, ruta), cancellationToken);
    }

    /// <summary><c>listarRolesAsignables</c>: <c>GET /api/usuarios/roles-asignables</c>.</summary>
    public Task<RespuestaApi<IReadOnlyList<RolResumen>>> ListarRolesAsignablesAsync(CancellationToken cancellationToken) =>
        EnviarAsync<IReadOnlyList<RolResumen>>(
            new HttpRequestMessage(HttpMethod.Get, $"{RutaUsuarios}/roles-asignables"),
            cancellationToken);

    /// <summary><c>obtenerUsuario</c>: <c>GET /api/usuarios/{id}</c>, con su <c>ETag</c>.</summary>
    public Task<RespuestaApi<DetalleUsuario>> ObtenerUsuarioAsync(Guid id, CancellationToken cancellationToken) =>
        EnviarAsync<DetalleUsuario>(new HttpRequestMessage(HttpMethod.Get, RutaUsuario(id)), cancellationToken);

    /// <summary><c>registrarUsuario</c>: <c>POST /api/usuarios</c> (201 con <c>ETag</c>).</summary>
    public Task<RespuestaApi<DetalleUsuario>> RegistrarUsuarioAsync(
        RegistrarUsuarioSolicitud solicitud,
        CancellationToken cancellationToken) =>
        EnviarAsync<DetalleUsuario>(
            new HttpRequestMessage(HttpMethod.Post, RutaUsuarios) { Content = JsonContent.Create(solicitud) },
            cancellationToken);

    /// <summary><c>editarUsuario</c>: <c>PUT /api/usuarios/{id}</c> con <c>If-Match</c>.</summary>
    /// <param name="version">ETag recibido (o <c>version</c> del usuario, que tiene el mismo valor), tal cual.</param>
    public Task<RespuestaApi<DetalleUsuario>> EditarUsuarioAsync(
        Guid id,
        EditarUsuarioSolicitud solicitud,
        string version,
        CancellationToken cancellationToken)
    {
        var solicitudHttp = new HttpRequestMessage(HttpMethod.Put, RutaUsuario(id)) { Content = JsonContent.Create(solicitud) };
        return EnviarAsync<DetalleUsuario>(ConIfMatch(solicitudHttp, version), cancellationToken);
    }

    /// <summary><c>desactivarUsuario</c>: <c>POST /api/usuarios/{id}/desactivacion</c> con <c>If-Match</c>.</summary>
    /// <param name="version">ETag recibido (o <c>version</c> del usuario, que tiene el mismo valor), tal cual.</param>
    public Task<RespuestaApi<DetalleUsuario>> DesactivarUsuarioAsync(Guid id, string version, CancellationToken cancellationToken) =>
        EnviarAsync<DetalleUsuario>(
            ConIfMatch(new HttpRequestMessage(HttpMethod.Post, $"{RutaUsuario(id)}/desactivacion"), version),
            cancellationToken);

    /// <summary><c>activarUsuario</c>: <c>POST /api/usuarios/{id}/activacion</c> con <c>If-Match</c>.</summary>
    /// <param name="version">ETag recibido (o <c>version</c> del usuario, que tiene el mismo valor), tal cual.</param>
    public Task<RespuestaApi<DetalleUsuario>> ActivarUsuarioAsync(Guid id, string version, CancellationToken cancellationToken) =>
        EnviarAsync<DetalleUsuario>(
            ConIfMatch(new HttpRequestMessage(HttpMethod.Post, $"{RutaUsuario(id)}/activacion"), version),
            cancellationToken);

    private static string RutaUsuario(Guid id) => $"{RutaUsuarios}/{id}";

    private static void Agregar(List<string> parametros, string nombre, string? valor)
    {
        if (valor is not null)
        {
            parametros.Add($"{nombre}={Uri.EscapeDataString(valor)}");
        }
    }

    private static HttpRequestMessage ConIfMatch(HttpRequestMessage solicitud, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        // El valor viaja tal cual se recibió en ETag (con sus comillas): no se reconstruye ni se recorta.
        if (!EntityTagHeaderValue.TryParse(version, out var etiqueta))
        {
            solicitud.Dispose();
            throw new ArgumentException("La versión no es un ETag HTTP válido.", nameof(version));
        }

        solicitud.Headers.IfMatch.Add(etiqueta);
        return solicitud;
    }

    private async Task<RespuestaApi<T>> EnviarAsync<T>(HttpRequestMessage solicitud, CancellationToken cancellationToken)
    {
        using (solicitud)
        using (var respuesta = await http.SendAsync(solicitud, cancellationToken))
        {
            if (respuesta.IsSuccessStatusCode)
            {
                var valor = await respuesta.Content.ReadFromJsonAsync<T>(cancellationToken)
                    ?? throw new JsonException("La respuesta correcta de la API no tiene cuerpo.");
                return RespuestaApi<T>.Correcta(respuesta.StatusCode, valor, respuesta.Headers.ETag?.ToString());
            }

            return RespuestaApi<T>.Fallida(respuesta.StatusCode, await LeerProblemaAsync(respuesta, cancellationToken));
        }
    }

    /// <summary>
    /// ProblemDetails del contrato (<see cref="Problema"/>, con <c>errors</c> o <c>actual</c> si vienen). Si el cuerpo
    /// no es un <c>Problema</c> válido (vacío o de otra forma), devuelve <c>null</c> y no inventa un código.
    /// </summary>
    private static async Task<Problema?> LeerProblemaAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        if (respuesta.Content.Headers.ContentType?.MediaType is not ("application/problem+json" or "application/json"))
        {
            return null;
        }

        try
        {
            return await respuesta.Content.ReadFromJsonAsync<Problema>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Resultado de una operación de <see cref="ClienteUsuarios"/>: el valor y su <c>ETag</c> si fue correcta, o el
/// ProblemDetails del contrato si la API respondió con un error.
/// </summary>
public sealed record RespuestaApi<T>
{
    private RespuestaApi(HttpStatusCode estadoHttp, T? valor, string? etag, Problema? problema)
    {
        EstadoHttp = estadoHttp;
        Valor = valor;
        ETag = etag;
        Problema = problema;
    }

    public HttpStatusCode EstadoHttp { get; }

    /// <summary>true si la API respondió 2xx; entonces <see cref="Valor"/> no es null.</summary>
    public bool EsCorrecta => (int)EstadoHttp is >= 200 and <= 299;

    public T? Valor { get; }

    /// <summary>Encabezado <c>ETag</c> tal cual llegó (con comillas), o null si la respuesta no lo trae.</summary>
    public string? ETag { get; }

    /// <summary>
    /// ProblemDetails del error, o null si la respuesta fue correcta o el error no trajo un <c>Problema</c> válido
    /// (por ejemplo, un cuerpo vacío); en ese caso solo queda <see cref="EstadoHttp"/>.
    /// </summary>
    public Problema? Problema { get; }

    /// <summary><c>Problema.codigo</c> del error (uno de <see cref="CodigosError"/>), o null.</summary>
    public string? CodigoError => Problema?.Codigo;

    internal static RespuestaApi<T> Correcta(HttpStatusCode estadoHttp, T valor, string? etag) => new(estadoHttp, valor, etag, null);

    internal static RespuestaApi<T> Fallida(HttpStatusCode estadoHttp, Problema? problema) => new(estadoHttp, default, null, problema);
}
