using Kryon.Core.Seguridad;

namespace Kryon.Core.Usuarios;

/// <summary>
/// Caso de uso del listado (FR-009 a FR-020): delega la búsqueda en <see cref="IConsultaUsuarios"/>, calcula con
/// <see cref="CalculadoraAcciones"/> las acciones y <c>esCuentaPropia</c> de cada usuario y decide
/// <see cref="ResultadoListarUsuarios.PuedeRegistrar"/>. El actor sale solo del <see cref="IContextoSolicitud"/>
/// verificado; la empresa la aplica la consulta.
/// </summary>
/// <remarks>
/// Las capacidades llegan ya evaluadas por la capa que conoce los nombres de permiso configurables (research §R4), y el
/// estado del gate <c>RegistroUsuarios</c> llega como dato: este caso de uso no consulta ni ejecuta los puntos de
/// integración DEP-2, DEP-3 ni DEP-5. La forma del contrato HTTP (estado como texto, versión como ETag) la decide la API.
/// </remarks>
public sealed class ListarUsuarios(IConsultaUsuarios consulta, IContextoSolicitud contexto)
{
    /// <param name="criterios">Búsqueda, filtros y página, ya validados por quien llama.</param>
    /// <param name="capacidades">Capacidades del actor para calcular las acciones.</param>
    /// <param name="capacidadRegistrar">El actor tiene la capacidad de registrar usuarios.</param>
    /// <param name="registroUsuariosAbierto">El gate de entrega <c>RegistroUsuarios</c> está abierto.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    public async Task<ResultadoListarUsuarios> EjecutarAsync(
        CriteriosConsultaUsuarios criterios,
        CapacidadesActor capacidades,
        bool capacidadRegistrar,
        bool registroUsuariosAbierto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        ArgumentNullException.ThrowIfNull(capacidades);

        var pagina = await consulta.BuscarAsync(criterios, cancellationToken);

        var usuarios = pagina.Elementos
            .Select(fila => new UsuarioListado(
                fila.Usuario,
                fila.Rol,
                CalculadoraAcciones.Calcular(fila.Usuario, contexto.UsuarioId, capacidades)))
            .ToArray();

        return new ResultadoListarUsuarios(
            usuarios,
            pagina.Total,
            pagina.Pagina,
            pagina.TamanoPagina,
            PuedeRegistrar: capacidadRegistrar && registroUsuariosAbierto);
    }
}

/// <summary>Resultado del listado: una página de usuarios con sus acciones y si el actor puede registrar.</summary>
/// <param name="Usuarios">Usuarios de la página, en el orden de la consulta.</param>
/// <param name="Total">Coincidencias con los mismos criterios en la empresa actual.</param>
/// <param name="Pagina">Página devuelta.</param>
/// <param name="TamanoPagina">Tamaño de página aplicado.</param>
/// <param name="PuedeRegistrar">Capacidad de registrar y gate <c>RegistroUsuarios</c> abierto (FR-020).</param>
public sealed record ResultadoListarUsuarios(
    IReadOnlyList<UsuarioListado> Usuarios,
    int Total,
    int Pagina,
    int TamanoPagina,
    bool PuedeRegistrar);

/// <summary>Un usuario del listado con su rol y las acciones calculadas para el actor.</summary>
/// <param name="Usuario">El usuario.</param>
/// <param name="Rol">Su rol; null si no tiene un rol reconocido (EC-10).</param>
/// <param name="Acciones">Acciones permitidas y <c>esCuentaPropia</c>, de <see cref="CalculadoraAcciones"/>.</param>
public sealed record UsuarioListado(Usuario Usuario, RolReferencia? Rol, AccionesUsuario Acciones);
