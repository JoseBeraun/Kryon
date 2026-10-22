using System.Diagnostics;

using Kryon.Contracts.Usuarios;

using Microsoft.AspNetCore.Diagnostics;

namespace Kryon.Api.Errores;

/// <summary>
/// Último recurso ante excepciones no controladas: responde <c>500</c> con la respuesta común <c>ErrorInterno</c>
/// del contrato, idéntica en todos los entornos y sin ningún detalle técnico (FR-041, SC-004).
/// </summary>
/// <remarks>
/// El detalle va solo al log del servidor y sin el mensaje de la excepción, que puede contener SQL, rutas, cadenas
/// de conexión o datos personales: se registran los tipos de la cadena de excepciones y los marcos de la pila sin
/// rutas de archivo, el método, la ruta sin query string y el identificador de traza.
/// </remarks>
public sealed partial class ManejadorExcepcionesInesperadas(ILogger<ManejadorExcepcionesInesperadas> logger)
    : IExceptionHandler
{
    private static readonly Problema ErrorInterno = new()
    {
        Type = "https://kryon.app/errores/" + CodigosError.ErrorInterno,
        Title = "Error interno",
        Status = StatusCodes.Status500InternalServerError,
        Codigo = CodigosError.ErrorInterno,
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        RegistrarFallo(
            logger,
            TiposDe(exception),
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? string.Empty,
            httpContext.TraceIdentifier,
            new StackTrace(exception, fNeedFileInfo: false).ToString());

        // Si ya se enviaron cabeceras o cuerpo, escribir otra respuesta la corrompería: la excepción sigue su curso.
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        await TypedResults
            .Json(ErrorInterno, options: null, contentType: "application/problem+json", statusCode: ErrorInterno.Status)
            .ExecuteAsync(httpContext);
        return true;
    }

    private static string TiposDe(Exception exception)
    {
        var tipos = new List<string>();
        for (Exception? actual = exception; actual is not null; actual = actual.InnerException)
        {
            tipos.Add(actual.GetType().FullName ?? actual.GetType().Name);
        }

        return string.Join(" <- ", tipos);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Excepción no controlada {Tipos} en {Metodo} {Ruta} (traza {TraceId}).\n{Pila}")]
    private static partial void RegistrarFallo(
        ILogger logger,
        string tipos,
        string metodo,
        string ruta,
        string traceId,
        string pila);
}
