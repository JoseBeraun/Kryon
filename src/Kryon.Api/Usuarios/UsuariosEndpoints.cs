using System.Globalization;
using System.Security.Claims;

using Kryon.Api.Seguridad;
using Kryon.Contracts.Usuarios;
using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kryon.Api.Usuarios;

/// <summary>
/// Endpoints de la Gestión de Usuarios (contracts/usuarios-api.openapi.yaml): <c>GET /api/usuarios</c>,
/// <c>GET /api/usuarios/roles-asignables</c> y <c>GET /api/usuarios/{id}</c> (política
/// <see cref="PoliticasUsuarios.Acceder"/>), y
/// <c>POST /api/usuarios/{id}/desactivacion</c> y <c>/activacion</c> (políticas <see cref="PoliticasUsuarios.Desactivar"/>
/// y <see cref="PoliticasUsuarios.Activar"/>, con <c>If-Match</c> obligatorio). La empresa sale siempre del contexto
/// verificado: ningún parámetro la indica.
/// </summary>
public static class UsuariosEndpoints
{
    public static IEndpointRouteBuilder MapUsuariosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/usuarios", ListarAsync)
            .RequireAuthorization(PoliticasUsuarios.Acceder);
        endpoints.MapGet("/api/usuarios/roles-asignables", ListarRolesAsignablesAsync)
            .RequireAuthorization(PoliticasUsuarios.Acceder);
        endpoints.MapGet("/api/usuarios/{id:guid}", ObtenerAsync)
            .RequireAuthorization(PoliticasUsuarios.Acceder)
            .RegistrarUsuarioNoAccesible(OperacionSolicitada.Consulta);
        endpoints.MapPost("/api/usuarios/{id:guid}/desactivacion", DesactivarAsync)
            .RequireAuthorization(PoliticasUsuarios.Desactivar)
            .RegistrarUsuarioNoAccesible(OperacionSolicitada.Desactivacion);
        endpoints.MapPost("/api/usuarios/{id:guid}/activacion", ActivarAsync)
            .RequireAuthorization(PoliticasUsuarios.Activar)
            .RegistrarUsuarioNoAccesible(OperacionSolicitada.Activacion);

        return endpoints;
    }

    /// <summary>
    /// <c>listarUsuarios</c>. Los parámetros llegan como texto para que un valor mal formado responda 400
    /// <c>validacion</c> del contrato y no un error genérico del enlazado.
    /// </summary>
    private static async Task<IResult> ListarAsync(
        [FromQuery] string? busqueda,
        [FromQuery] string? rolId,
        [FromQuery] string? estado,
        [FromQuery] string? pagina,
        [FromQuery] string? tamanoPagina,
        ClaimsPrincipal usuario,
        ListarUsuarios listarUsuarios,
        IAuthorizationService autorizacion,
        IServiceProviderIsService servicios,
        IOptions<OpcionesUsuarios> opciones,
        CancellationToken cancellationToken)
    {
        var errores = new Dictionary<string, IReadOnlyList<string>>();
        var criterios = LeerCriterios(busqueda, rolId, estado, pagina, tamanoPagina, opciones.Value.Paginacion, errores);
        if (criterios is null)
        {
            return new ErrorUsuarios(CodigoErrorUsuarios.Validacion, "Los criterios del listado no son válidos.", errores).ARespuesta();
        }

        var capacidades = await CapacidadesAsync(autorizacion, usuario);
        var capacidadRegistrar = await CumpleAsync(autorizacion, usuario, PoliticasUsuarios.Registrar);

        var resultado = await listarUsuarios.EjecutarAsync(
            criterios,
            capacidades,
            capacidadRegistrar,
            GateRegistroUsuarios.EstaAbierto(servicios),
            cancellationToken);

        return TypedResults.Ok(new PaginaUsuarios
        {
            Elementos = [.. resultado.Usuarios.Select(AUsuarioResumen)],
            Total = resultado.Total,
            Pagina = resultado.Pagina,
            TamanoPagina = resultado.TamanoPagina,
            PuedeRegistrar = resultado.PuedeRegistrar,
        });
    }

    /// <summary>
    /// <c>obtenerUsuario</c>: el usuario de la empresa actual con sus acciones y su <c>ETag</c> (US3-1). Un id de otra
    /// empresa y uno inexistente responden el mismo 404 (FR-003), que audita el filtro
    /// <see cref="RegistroUsuarioNoAccesible"/> con <c>Consulta</c> (FR-046).
    /// </summary>
    private static async Task<IResult> ObtenerAsync(
        Guid id,
        HttpContext http,
        IConsultaUsuarios consulta,
        IAuthorizationService autorizacion,
        IContextoSolicitud contexto,
        CancellationToken cancellationToken)
    {
        var detalle = await DetalleAsync(id, consulta, autorizacion, http.User, contexto, cancellationToken);
        if (detalle is null)
        {
            return NoEncontrado();
        }

        http.Response.Headers.ETag = detalle.Version;
        return TypedResults.Ok(detalle);
    }

    /// <summary><c>desactivarUsuario</c>: 200 con el usuario y su nuevo <c>ETag</c>.</summary>
    private static Task<IResult> DesactivarAsync(
        Guid id,
        HttpContext http,
        CambiarEstadoUsuario cambiarEstado,
        IConsultaUsuarios consulta,
        IAuthorizationService autorizacion,
        IContextoSolicitud contexto,
        CancellationToken cancellationToken) =>
        CambiarEstadoAsync(id, http, cambiarEstado.DesactivarAsync, consulta, autorizacion, contexto, cancellationToken);

    /// <summary><c>activarUsuario</c>: 200 con el usuario y su nuevo <c>ETag</c>.</summary>
    private static Task<IResult> ActivarAsync(
        Guid id,
        HttpContext http,
        CambiarEstadoUsuario cambiarEstado,
        IConsultaUsuarios consulta,
        IAuthorizationService autorizacion,
        IContextoSolicitud contexto,
        CancellationToken cancellationToken) =>
        CambiarEstadoAsync(id, http, cambiarEstado.ActivarAsync, consulta, autorizacion, contexto, cancellationToken);

    /// <summary>
    /// Flujo común: <c>If-Match</c> obligatorio (428 <c>falta-version</c>), ejecutar el caso de uso y responder el
    /// usuario vigente con su <c>ETag</c>. Ante <c>usuario-modificado</c> responde 412 con <c>actual</c> (FR-038, EC-2);
    /// ante <c>usuario-no-encontrado</c>, el mismo 404 para un id de otra empresa y uno inexistente (FR-003), que audita
    /// el filtro <see cref="RegistroUsuarioNoAccesible"/>.
    /// </summary>
    private static async Task<IResult> CambiarEstadoAsync(
        Guid id,
        HttpContext http,
        Func<Guid, byte[], CancellationToken, Task<Usuario>> cambiar,
        IConsultaUsuarios consulta,
        IAuthorizationService autorizacion,
        IContextoSolicitud contexto,
        CancellationToken cancellationToken)
    {
        var ifMatch = http.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return new ErrorUsuarios(CodigoErrorUsuarios.FaltaVersion, "La operación requiere If-Match.").ARespuesta();
        }

        try
        {
            await cambiar(id, VersionEtag.Leer(ifMatch), cancellationToken);
        }
        catch (ErrorUsuarios error) when (error.Codigo == CodigoErrorUsuarios.UsuarioModificado)
        {
            var actual = await DetalleAsync(id, consulta, autorizacion, http.User, contexto, cancellationToken);
            return actual is null ? NoEncontrado() : error.ARespuesta(actual);
        }
        catch (ErrorUsuarios error)
        {
            return error.ARespuesta();
        }

        var detalle = await DetalleAsync(id, consulta, autorizacion, http.User, contexto, cancellationToken);
        if (detalle is null)
        {
            return NoEncontrado();
        }

        http.Response.Headers.ETag = detalle.Version;
        return TypedResults.Ok(detalle);
    }

    /// <summary>El usuario vigente con las acciones del actor, o null si no es accesible.</summary>
    private static async Task<DetalleUsuario?> DetalleAsync(
        Guid id,
        IConsultaUsuarios consulta,
        IAuthorizationService autorizacion,
        ClaimsPrincipal usuario,
        IContextoSolicitud contexto,
        CancellationToken cancellationToken)
    {
        var fila = await consulta.ObtenerAsync(id, cancellationToken);
        if (fila is null)
        {
            return null;
        }

        var acciones = CalculadoraAcciones.Calcular(fila.Usuario, contexto.UsuarioId, await CapacidadesAsync(autorizacion, usuario));
        return ADetalle(fila.Usuario, fila.Rol, acciones);
    }

    private static IResult NoEncontrado() =>
        new ErrorUsuarios(CodigoErrorUsuarios.UsuarioNoEncontrado, "El usuario no existe o no es accesible.").ARespuesta();

    /// <summary><c>listarRolesAsignables</c>: los roles de la empresa actual, solo con id y nombre.</summary>
    private static async Task<IResult> ListarRolesAsignablesAsync(IConsultaUsuarios consulta, CancellationToken cancellationToken)
    {
        var roles = await consulta.ListarRolesAsignablesAsync(cancellationToken);
        return TypedResults.Ok(roles.Select(ARolResumen).ToArray());
    }

    /// <summary>
    /// Valida los parámetros del listado con los límites del contrato y de <see cref="OpcionesUsuarios"/>. Devuelve
    /// null y rellena <paramref name="errores"/> por parámetro si alguno no es válido.
    /// </summary>
    private static CriteriosConsultaUsuarios? LeerCriterios(
        string? busqueda,
        string? rolId,
        string? estado,
        string? pagina,
        string? tamanoPagina,
        OpcionesPaginacionUsuarios paginacion,
        Dictionary<string, IReadOnlyList<string>> errores)
    {
        Guid? rol = null;
        if (rolId is not null)
        {
            if (Guid.TryParse(rolId, out var valor))
            {
                rol = valor;
            }
            else
            {
                errores[nameof(rolId)] = ["Debe ser un identificador de rol válido."];
            }
        }

        EstadoUsuario? estadoUsuario = estado switch
        {
            null => null,
            "activo" => EstadoUsuario.Activo,
            "inactivo" => EstadoUsuario.Inactivo,
            _ => null,
        };
        if (estado is not null && estadoUsuario is null)
        {
            errores[nameof(estado)] = ["Debe ser 'activo' o 'inactivo'."];
        }

        var numeroPagina = 1;
        if (pagina is not null && !(int.TryParse(pagina, NumberStyles.None, CultureInfo.InvariantCulture, out numeroPagina) && numeroPagina >= 1))
        {
            errores[nameof(pagina)] = ["Debe ser un número entero mayor o igual que 1."];
        }

        int? tamano = null;
        if (tamanoPagina is not null)
        {
            if (int.TryParse(tamanoPagina, NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
                && valor >= 1
                && valor <= paginacion.TamanoMaximo)
            {
                tamano = valor;
            }
            else
            {
                errores[nameof(tamanoPagina)] = [$"Debe ser un número entero entre 1 y {paginacion.TamanoMaximo}."];
            }
        }

        // Una página tan alta que su desplazamiento no se puede calcular no existe.
        if (errores.Count == 0 && (long)(numeroPagina - 1) * (tamano ?? paginacion.TamanoPorDefecto) > int.MaxValue)
        {
            errores[nameof(pagina)] = ["La página solicitada no existe."];
        }

        return errores.Count == 0
            ? new CriteriosConsultaUsuarios(busqueda, rol, estadoUsuario, numeroPagina, tamano)
            : null;
    }

    /// <summary>
    /// Capacidades del actor con las mismas políticas que protegen cada operación (T008): las acciones anunciadas
    /// coinciden con lo que la API autoriza (SC-005).
    /// </summary>
    private static async Task<CapacidadesActor> CapacidadesAsync(IAuthorizationService autorizacion, ClaimsPrincipal usuario) => new(
        Acceder: await CumpleAsync(autorizacion, usuario, PoliticasUsuarios.Acceder),
        Editar: await CumpleAsync(autorizacion, usuario, PoliticasUsuarios.Editar),
        Activar: await CumpleAsync(autorizacion, usuario, PoliticasUsuarios.Activar),
        Desactivar: await CumpleAsync(autorizacion, usuario, PoliticasUsuarios.Desactivar));

    private static async Task<bool> CumpleAsync(IAuthorizationService autorizacion, ClaimsPrincipal usuario, string politica) =>
        (await autorizacion.AuthorizeAsync(usuario, resource: null, politica)).Succeeded;

    private static UsuarioResumen AUsuarioResumen(UsuarioListado listado) => ADetalle(listado.Usuario, listado.Rol, listado.Acciones);

    /// <summary>Esquemas <c>UsuarioResumen</c> y <c>DetalleUsuario</c> (mismos campos): solo los del contrato.</summary>
    private static DetalleUsuario ADetalle(Usuario usuario, RolReferencia? rol, AccionesUsuario acciones) => new()
    {
        Id = usuario.Id,
        NombreCompleto = usuario.NombreCompleto,
        IdentificadorAcceso = usuario.IdentificadorAcceso,
        Rol = rol is null ? null : ARolResumen(rol),
        Estado = AEstado(usuario.Estado),
        EsCuentaPropia = acciones.EsCuentaPropia,
        Version = VersionEtag.Desde(usuario.Version),
        Acciones = new AccionesPermitidas
        {
            VerDetalle = acciones.VerDetalle,
            Editar = acciones.Editar,
            CambiarRol = acciones.CambiarRol,
            EditarIdentificador = acciones.EditarIdentificador,
            Activar = acciones.Activar,
            Desactivar = acciones.Desactivar,
        },
    };

    private static RolResumen ARolResumen(RolReferencia rol) => new() { Id = rol.Id, Nombre = rol.Nombre };

    /// <summary>Esquema <c>EstadoUsuario</c> del contrato. Un estado que el contrato no define es un error de programación.</summary>
    private static string AEstado(EstadoUsuario estado) => estado switch
    {
        EstadoUsuario.Activo => "activo",
        EstadoUsuario.Inactivo => "inactivo",
        _ => throw new InvalidOperationException($"Estado de usuario sin representación en el contrato: {estado}."),
    };
}
