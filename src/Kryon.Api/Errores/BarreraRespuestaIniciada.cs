namespace Kryon.Api.Errores;

/// <summary>
/// Complemento de <see cref="ManejadorExcepcionesInesperadas"/> para el único caso que este no puede atender: una
/// excepción cuando la respuesta ya empezó a enviarse. Va justo después de <c>UseExceptionHandler</c>.
/// </summary>
/// <remarks>
/// Si la respuesta no ha empezado, no intercepta nada: la excepción sigue hasta <c>UseExceptionHandler</c> y el
/// manejador responde el 500 genérico. Si ya empezó, no se puede escribir otra respuesta: registra solo datos
/// seguros y aborta la conexión. No relanza la excepción, para que <c>ExceptionHandlerMiddleware</c> y, en
/// Development, <c>DeveloperExceptionPage</c> no la registren con su mensaje, que puede contener datos sensibles.
/// </remarks>
public sealed partial class BarreraRespuestaIniciada(RequestDelegate next, ILogger<BarreraRespuestaIniciada> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (context.Response.HasStarted)
        {
            RegistrarFallo(
                logger,
                exception.GetType().FullName ?? exception.GetType().Name,
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                context.TraceIdentifier);

            context.Abort();
        }
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Excepción no controlada {Tipo} con la respuesta ya iniciada en {Metodo} {Ruta} (traza {TraceId}); se abortó la conexión.")]
    private static partial void RegistrarFallo(ILogger logger, string tipo, string metodo, string ruta, string traceId);
}
