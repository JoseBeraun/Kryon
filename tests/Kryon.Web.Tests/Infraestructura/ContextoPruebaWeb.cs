using System.Net;
using System.Net.Http.Json;

using Bunit;

using Kryon.Contracts.Usuarios;
using Kryon.Web.Usuarios;

using Microsoft.Extensions.DependencyInjection;

namespace Kryon.Web.Tests.Infraestructura;

/// <summary>
/// Contexto de bUnit de las pruebas de Kryon.Web: registra el <see cref="ClienteUsuarios"/> <b>real</b> sobre un
/// <see cref="HttpClient"/> cuyo único punto sustituible es <see cref="Http"/>, un <see cref="HttpMessageHandler"/>
/// falso que devuelve lo que prepara cada prueba. No hay API, servidor, base de datos ni identidad: la URL base es
/// ficticia y nunca se resuelve.
/// </summary>
/// <remarks>
/// Cada instancia tiene su propio handler, su propio <see cref="HttpClient"/> y su propio <see cref="ClienteUsuarios"/>:
/// nada se comparte entre pruebas.
/// </remarks>
public sealed class ContextoPruebaWeb : BunitContext
{
    /// <summary>URL base solo de pruebas; nunca se abre ninguna conexión.</summary>
    public static readonly Uri UrlBase = new("https://kryon.test/");

    public ContextoPruebaWeb()
    {
        Services.AddSingleton(new ClienteUsuarios(new HttpClient(Http) { BaseAddress = UrlBase }));
    }

    /// <summary>Handler falso de este contexto: cada prueba asigna su <see cref="HttpFalso.Responder"/>.</summary>
    public HttpFalso Http { get; } = new();
}

/// <summary>
/// <see cref="HttpMessageHandler"/> falso: responde con <see cref="Responder"/> y registra cada solicitud recibida. Si
/// la prueba no preparó ninguna respuesta, falla en vez de inventar una.
/// </summary>
public sealed class HttpFalso : HttpMessageHandler
{
    private readonly List<SolicitudRecibida> _solicitudes = [];

    /// <summary>
    /// Respuesta de cada solicitud. Recibe la solicitud y el <see cref="CancellationToken"/> que llegó al handler.
    /// Para simular un fallo sin respuesta, lanza la excepción (por ejemplo <see cref="HttpRequestException"/>).
    /// </summary>
    public Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? Responder { get; set; }

    /// <summary>Solicitudes recibidas, en orden, con su cuerpo leído antes de responder.</summary>
    public IReadOnlyList<SolicitudRecibida> Solicitudes => _solicitudes;

    /// <summary>Respuesta JSON con el DTO indicado y, si se da, el encabezado <c>ETag</c>.</summary>
    public static HttpResponseMessage Json<T>(HttpStatusCode estado, T cuerpo, string? etag = null)
    {
        var respuesta = new HttpResponseMessage(estado) { Content = JsonContent.Create(cuerpo) };
        if (etag is not null)
        {
            respuesta.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(etag);
        }

        return respuesta;
    }

    /// <summary>Respuesta de error con el ProblemDetails del contrato (<c>application/problem+json</c>).</summary>
    public static HttpResponseMessage ConProblema(Problema problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        return new HttpResponseMessage((HttpStatusCode)problema.Status)
        {
            Content = JsonContent.Create(problema, mediaType: new("application/problem+json")),
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _solicitudes.Add(new SolicitudRecibida(request, cuerpo, cancellationToken));

        var responder = Responder
            ?? throw new InvalidOperationException("La prueba no preparó ninguna respuesta HTTP (HttpFalso.Responder).");
        return responder(request, cancellationToken);
    }
}

/// <summary>Solicitud que recibió <see cref="HttpFalso"/>, con su cuerpo como texto y el token recibido.</summary>
public sealed record SolicitudRecibida(HttpRequestMessage Solicitud, string? Cuerpo, CancellationToken CancellationToken);
