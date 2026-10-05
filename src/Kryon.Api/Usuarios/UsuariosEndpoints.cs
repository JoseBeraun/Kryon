using System.Globalization;
using System.Security.Claims;

using Kryon.Api.Seguridad;
using Kryon.Contracts.Usuarios;
using Kryon.Core.Usuarios;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kryon.Api.Usuarios;

/// <summary>
/// Endpoints de la Gestión de Usuarios de US1 (contracts/usuarios-api.openapi.yaml): <c>GET /api/usuarios</c> y
/// <c>GET /api/usuarios/roles-asignables</c>, ambos con la política <see cref="PoliticasUsuarios.Acceder"/>. La empresa
/// sale siempre del contexto verificado: ningún parámetro la indica.
/// </summary>
public static class UsuariosEndpoints
{
    public static IEndpointRouteBuilder MapUsuariosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/usuarios", ListarAsync)
            .RequireAuthorization(PoliticasUsuarios.Acceder);
        endpoints.MapGet("/api/usuarios/roles-asignables", ListarRolesAsignablesAsync)
            .RequireAuthorization(PoliticasUsuarios.Acceder);

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

        // Mismas políticas que protegen cada operación (T008): las acciones anunciadas coinciden con lo que la API
        // autoriza (SC-005).
        var capacidades = new CapacidadesActor(
            Acceder: await CumpleAsync(autorizacion, usuario, PoliticasUsuarios.Acceder),
            Editar: await CumpleAsync(autorizacion, usuario, PoliticasUsuarios.Editar),
            Activar: await CumpleAsync(autorizacion, usuario, PoliticasUsuarios.Activar),
            Desactivar: await CumpleAsync(autorizacion, usuario, PoliticasUsuarios.Desactivar));
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

    private static async Task<bool> CumpleAsync(IAuthorizationService autorizacion, ClaimsPrincipal usuario, string politica) =>
        (await autorizacion.AuthorizeAsync(usuario, resource: null, politica)).Succeeded;

    private static UsuarioResumen AUsuarioResumen(UsuarioListado listado) => new()
    {
        Id = listado.Usuario.Id,
        NombreCompleto = listado.Usuario.NombreCompleto,
        IdentificadorAcceso = listado.Usuario.IdentificadorAcceso,
        Rol = listado.Rol is null ? null : ARolResumen(listado.Rol),
        Estado = AEstado(listado.Usuario.Estado),
        EsCuentaPropia = listado.Acciones.EsCuentaPropia,
        Version = VersionEtag.Desde(listado.Usuario.Version),
        Acciones = new AccionesPermitidas
        {
            VerDetalle = listado.Acciones.VerDetalle,
            Editar = listado.Acciones.Editar,
            CambiarRol = listado.Acciones.CambiarRol,
            EditarIdentificador = listado.Acciones.EditarIdentificador,
            Activar = listado.Acciones.Activar,
            Desactivar = listado.Acciones.Desactivar,
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
