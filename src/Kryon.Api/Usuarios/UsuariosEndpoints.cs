using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

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
/// <see cref="PoliticasUsuarios.Acceder"/>), <c>POST /api/usuarios</c> (política <see cref="PoliticasUsuarios.Registrar"/>,
/// solo con el gate <see cref="GateRegistroUsuarios"/> abierto), <c>PUT /api/usuarios/{id}</c> (política <see cref="PoliticasUsuarios.Editar"/>,
/// con <c>If-Match</c> obligatorio), y
/// <c>POST /api/usuarios/{id}/desactivacion</c> y <c>/activacion</c> (políticas <see cref="PoliticasUsuarios.Desactivar"/>
/// y <see cref="PoliticasUsuarios.Activar"/>, con <c>If-Match</c> obligatorio). La empresa sale siempre del contexto
/// verificado: ningún parámetro la indica.
/// </summary>
public static class UsuariosEndpoints
{
    /// <summary>Clave de <c>errors</c> cuando el cuerpo entero no se puede leer y no hay un campo al que asociar el error.</summary>
    private const string CampoCuerpo = "cuerpo";

    /// <summary>
    /// Mapea los 7 endpoints, cada uno con su política (SC-005). Las acciones que anuncia la API salen de esas mismas
    /// políticas: <see cref="CapacidadesAsync"/> evalúa <c>Acceder</c>, <c>Editar</c>, <c>Activar</c> y
    /// <c>Desactivar</c> y <see cref="CalculadoraAcciones"/> las traduce a <c>acciones</c>; <c>puedeRegistrar</c> sale de
    /// <c>Registrar</c> y del gate <see cref="GateRegistroUsuarios"/>, las mismas condiciones con las que se mapea y se
    /// autoriza el registro. Así una acción visible no puede divergir de una permitida (comprobado en
    /// <c>CoherenciaAccionesTests</c>).
    /// </summary>
    public static IEndpointRouteBuilder MapUsuariosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // GET listado → Usuarios.Acceder. puedeRegistrar = Usuarios.Registrar y gate RegistroUsuarios abierto.
        endpoints.MapGet("/api/usuarios", ListarAsync)
            .RequireAuthorization(PoliticasUsuarios.Acceder);

        // GET roles asignables → Usuarios.Acceder.
        endpoints.MapGet("/api/usuarios/roles-asignables", ListarRolesAsignablesAsync)
            .RequireAuthorization(PoliticasUsuarios.Acceder);

        // GET detalle → Usuarios.Acceder; acciones.verDetalle = Usuarios.Acceder.
        endpoints.MapGet("/api/usuarios/{id:guid}", ObtenerAsync)
            .RequireAuthorization(PoliticasUsuarios.Acceder)
            .RegistrarUsuarioNoAccesible(OperacionSolicitada.Consulta);

        // POST registro → Usuarios.Registrar, y solo con el gate RegistroUsuarios abierto (tasks.md → Feature / release
        // gating): sin los puntos de integración de DEP-2, DEP-3 y DEP-5 registrados, el endpoint no existe.
        if (GateRegistroUsuarios.EstaAbierto(endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>()))
        {
            endpoints.MapPost("/api/usuarios", RegistrarAsync)
                .RequireAuthorization(PoliticasUsuarios.Registrar);
        }

        // PUT edición → Usuarios.Editar; acciones.editar = Usuarios.Editar (cambiarRol, además, no en la cuenta propia).
        endpoints.MapPut("/api/usuarios/{id:guid}", EditarAsync)
            .RequireAuthorization(PoliticasUsuarios.Editar)
            .RegistrarUsuarioNoAccesible(OperacionSolicitada.Edicion);

        // POST desactivación → Usuarios.Desactivar; acciones.desactivar = Usuarios.Desactivar, usuario Activo y no propio.
        endpoints.MapPost("/api/usuarios/{id:guid}/desactivacion", DesactivarAsync)
            .RequireAuthorization(PoliticasUsuarios.Desactivar)
            .RegistrarUsuarioNoAccesible(OperacionSolicitada.Desactivacion);

        // POST activación → Usuarios.Activar; acciones.activar = Usuarios.Activar y usuario Inactivo.
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
        ModificarAsync(id, http, cambiarEstado.DesactivarAsync, consulta, autorizacion, contexto, cancellationToken);

    /// <summary><c>activarUsuario</c>: 200 con el usuario y su nuevo <c>ETag</c>.</summary>
    private static Task<IResult> ActivarAsync(
        Guid id,
        HttpContext http,
        CambiarEstadoUsuario cambiarEstado,
        IConsultaUsuarios consulta,
        IAuthorizationService autorizacion,
        IContextoSolicitud contexto,
        CancellationToken cancellationToken) =>
        ModificarAsync(id, http, cambiarEstado.ActivarAsync, consulta, autorizacion, contexto, cancellationToken);

    /// <summary>
    /// <c>editarUsuario</c>: nombre completo y rol; 200 con el usuario y su nuevo <c>ETag</c>. El identificador de acceso
    /// no se edita: mientras el gate <c>EdicionIdentificador</c> esté cerrado (tasks.md → <i>Feature / release gating</i>),
    /// la edición no lo acepta y responde 400 <c>validacion</c> sin llegar al caso de uso. Abrir el gate y decidir si es
    /// editable corresponde a T079/T080 (DEP-4).
    /// </summary>
    private static Task<IResult> EditarAsync(
        Guid id,
        HttpContext http,
        EditarUsuario editar,
        IConsultaUsuarios consulta,
        IAuthorizationService autorizacion,
        IContextoSolicitud contexto,
        CancellationToken cancellationToken) =>
        ModificarAsync(
            id,
            http,
            async (usuarioId, version, ct) =>
            {
                var (solicitud, traeIdentificador) = await LeerEdicionAsync(http.Request, ct);

                // Basta con que la propiedad esté presente, aunque sea null: el contrato la define como texto opcional,
                // no anulable, y con el gate cerrado la edición no la acepta.
                if (traeIdentificador)
                {
                    throw new ErrorUsuarios(
                        CodigoErrorUsuarios.Validacion,
                        "La edición no acepta el identificador de acceso.",
                        new Dictionary<string, IReadOnlyList<string>>
                        {
                            [ValidadorUsuario.CampoIdentificadorAcceso] = ["La edición del identificador de acceso no está disponible."],
                        });
                }

                return await editar.EditarAsync(usuarioId, version, solicitud.NombreCompleto, solicitud.RolId, ct);
            },
            consulta,
            autorizacion,
            contexto,
            cancellationToken);

    /// <summary>
    /// <c>registrarUsuario</c>: 201 con el usuario creado, <c>Location</c> y <c>ETag</c>. Solo se mapea con el gate
    /// <c>RegistroUsuarios</c> abierto. La empresa no viaja en el cuerpo: sale del contexto verificado (FR-023).
    /// </summary>
    private static async Task<IResult> RegistrarAsync(
        HttpContext http,
        RegistrarUsuario registrar,
        IConsultaUsuarios consulta,
        IAuthorizationService autorizacion,
        IContextoSolicitud contexto,
        CancellationToken cancellationToken)
    {
        Usuario usuario;
        try
        {
            var (solicitud, _) = await LeerCuerpoAsync<RegistrarUsuarioSolicitud>(
                http.Request,
                "Envía un objeto JSON con nombreCompleto, identificadorAcceso y rolId, sin otros campos.",
                cancellationToken);
            usuario = await registrar.RegistrarAsync(solicitud.NombreCompleto, solicitud.IdentificadorAcceso, solicitud.RolId, cancellationToken);
        }
        catch (ErrorUsuarios error)
        {
            return error.ARespuesta();
        }

        var detalle = await DetalleAsync(usuario.Id, consulta, autorizacion, http.User, contexto, cancellationToken)
            ?? throw new InvalidOperationException("El usuario recién registrado no es accesible en su propia empresa.");
        http.Response.Headers.ETag = detalle.Version;
        return TypedResults.Created($"/api/usuarios/{usuario.Id}", detalle);
    }

    /// <summary>
    /// Lee el cuerpo <c>EditarUsuario</c> e indica si trae la propiedad <c>identificadorAcceso</c>, con cualquier valor
    /// (incluido <c>null</c>).
    /// </summary>
    private static async Task<(EditarUsuarioSolicitud Solicitud, bool TraeIdentificador)> LeerEdicionAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var (solicitud, cuerpo) = await LeerCuerpoAsync<EditarUsuarioSolicitud>(
            request,
            "Envía un objeto JSON con nombreCompleto y rolId, sin otros campos.",
            cancellationToken);

        // Mismo criterio de nombres que el enlazado (sin distinguir mayúsculas con las opciones web).
        var opciones = OpcionesJson(request);
        var traeIdentificador = cuerpo.EnumerateObject().Any(propiedad => string.Equals(
            propiedad.Name,
            ValidadorUsuario.CampoIdentificadorAcceso,
            opciones.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        return (solicitud, traeIdentificador);
    }

    /// <summary>
    /// Lee un cuerpo JSON del contrato. Un cuerpo vacío, mal formado, con campos que el contrato no define o sin los
    /// obligatorios responde 400 <c>validacion</c> del contrato, no un error genérico del enlazado.
    /// </summary>
    private static async Task<(T Solicitud, JsonElement Cuerpo)> LeerCuerpoAsync<T>(
        HttpRequest request,
        string indicacion,
        CancellationToken cancellationToken)
        where T : class
    {
        var opciones = OpcionesJson(request);
        try
        {
            var cuerpo = await request.ReadFromJsonAsync<JsonElement>(opciones, cancellationToken);
            if (cuerpo.ValueKind == JsonValueKind.Object && cuerpo.Deserialize<T>(opciones) is { } solicitud)
            {
                return (solicitud, cuerpo);
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            // Se responde como cualquier otro cuerpo no válido.
        }

        throw new ErrorUsuarios(
            CodigoErrorUsuarios.Validacion,
            "El cuerpo de la solicitud no es válido.",
            new Dictionary<string, IReadOnlyList<string>> { [CampoCuerpo] = [indicacion] });
    }

    private static JsonSerializerOptions OpcionesJson(HttpRequest request) =>
        request.HttpContext.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;

    /// <summary>
    /// Flujo común: <c>If-Match</c> obligatorio (428 <c>falta-version</c>), ejecutar el caso de uso y responder el
    /// usuario vigente con su <c>ETag</c>. Ante <c>usuario-modificado</c> responde 412 con <c>actual</c> (FR-038, EC-2);
    /// ante <c>usuario-no-encontrado</c>, el mismo 404 para un id de otra empresa y uno inexistente (FR-003), que audita
    /// el filtro <see cref="RegistroUsuarioNoAccesible"/>.
    /// </summary>
    private static async Task<IResult> ModificarAsync(
        Guid id,
        HttpContext http,
        Func<Guid, byte[], CancellationToken, Task<Usuario>> modificar,
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
            await modificar(id, VersionEtag.Leer(ifMatch), cancellationToken);
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
    /// coinciden con lo que la API autoriza (SC-005). Registrar no está aquí porque no es una acción sobre un usuario:
    /// lo evalúa <see cref="ListarAsync"/> para <c>puedeRegistrar</c>, junto con el gate.
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
