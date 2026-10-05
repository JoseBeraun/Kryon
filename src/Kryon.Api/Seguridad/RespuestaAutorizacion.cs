using Kryon.Api.Usuarios;
using Kryon.Core.Usuarios;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Kryon.Api.Seguridad;

/// <summary>
/// Da cuerpo a las denegaciones del middleware de autorización: sin él, ASP.NET Core responde 401 y 403 vacíos. Primero
/// deja que el comportamiento por defecto haga el desafío o la prohibición de cada esquema; después, si la respuesta no
/// ha empezado, escribe el ProblemDetails del contrato: <c>no-autenticado</c> (401) o <c>sin-permiso</c> (403).
/// </summary>
/// <remarks>
/// El cuerpo es siempre el mismo para cada código y no incluye datos del recurso ni de la política. El endpoint no
/// llega a ejecutarse.
/// </remarks>
public sealed class RespuestaAutorizacion : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler porDefecto = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        await porDefecto.HandleAsync(next, context, policy, authorizeResult);

        var codigo = authorizeResult switch
        {
            { Challenged: true } => CodigoErrorUsuarios.NoAutenticado,
            { Forbidden: true } => CodigoErrorUsuarios.SinPermiso,
            _ => null,
        };

        if (codigo is null || context.Response.HasStarted)
        {
            return;
        }

        await new ErrorUsuarios(codigo, "Solicitud denegada por la política de autorización.")
            .ARespuesta()
            .ExecuteAsync(context);
    }
}
