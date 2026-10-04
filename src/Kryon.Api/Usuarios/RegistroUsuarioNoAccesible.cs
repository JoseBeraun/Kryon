using Kryon.Contracts.Usuarios;
using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;

namespace Kryon.Api.Usuarios;

/// <summary>
/// Filtro de endpoint para las operaciones sobre <c>{id}</c>: si el endpoint termina en <c>usuario-no-encontrado</c>,
/// registra en la auditoría un <see cref="TipoOperacion.UsuarioNoAccesible"/> (FR-046) y devuelve la misma respuesta.
/// </summary>
/// <remarks>
/// No consulta ni infiere la empresa propietaria del recurso: con RLS, un usuario de otra empresa y uno inexistente
/// son indistinguibles (Principio I). El registro guarda solo el actor, la empresa del actor, el id solicitado, la
/// operación y el momento. Si la auditoría falla, la excepción no se oculta: la atiende el manejador global (T030).
/// </remarks>
/// <param name="operacion">Operación que intentaba el endpoint al que se aplica el filtro.</param>
public sealed class RegistroUsuarioNoAccesible(OperacionSolicitada operacion) : IEndpointFilter
{
    /// <summary>Nombre del parámetro de ruta con el id solicitado.</summary>
    public const string ParametroId = "id";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var resultado = await next(context);

        if (EsUsuarioNoEncontrado(resultado) && TryObtenerIdSolicitado(context.HttpContext, out var idSolicitado))
        {
            var servicios = context.HttpContext.RequestServices;
            var contextoSolicitud = servicios.GetRequiredService<IContextoSolicitud>();
            var ahora = (servicios.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();

            var registro = new AuditoriaUsuario(
                empresaId: contextoSolicitud.EmpresaId,
                actorUsuarioId: contextoSolicitud.UsuarioId,
                ocurridoEn: ahora,
                tipoOperacion: TipoOperacion.UsuarioNoAccesible,
                usuarioAfectadoId: idSolicitado,
                operacionSolicitada: operacion,
                cambiosJson: null);

            // Sin RequestAborted: que el cliente cierre la conexión no debe cancelar un registro de auditoría de seguridad.
            await servicios.GetRequiredService<IRegistroAuditoria>()
                .RegistrarAsync(registro, CancellationToken.None);
        }

        return resultado;
    }

    /// <summary>
    /// Reconoce por su código, no por el estado 404, la respuesta que produce <see cref="MapeoErrores"/> (también
    /// dentro de un resultado compuesto <c>Results&lt;…&gt;</c>).
    /// </summary>
    private static bool EsUsuarioNoEncontrado(object? resultado)
    {
        while (resultado is INestedHttpResult anidado)
        {
            resultado = anidado.Result;
        }

        return resultado is IValueHttpResult<Problema> { Value.Codigo: CodigoErrorUsuarios.UsuarioNoEncontrado };
    }

    private static bool TryObtenerIdSolicitado(HttpContext httpContext, out Guid idSolicitado)
    {
        idSolicitado = Guid.Empty;
        return httpContext.GetRouteValue(ParametroId) is { } valor
            && Guid.TryParse(valor.ToString(), out idSolicitado);
    }
}

/// <summary>Aplicación del filtro <see cref="RegistroUsuarioNoAccesible"/> a un endpoint.</summary>
public static class RegistroUsuarioNoAccesibleExtensiones
{
    /// <summary>
    /// Registra en la auditoría los <c>usuario-no-encontrado</c> de este endpoint como intentos de
    /// <paramref name="operacion"/>.
    /// </summary>
    public static TBuilder RegistrarUsuarioNoAccesible<TBuilder>(this TBuilder builder, OperacionSolicitada operacion)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new RegistroUsuarioNoAccesible(operacion));
}
