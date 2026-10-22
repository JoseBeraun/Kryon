namespace Kryon.Core.Usuarios;

/// <summary>
/// Lecturas de la Gestión de Usuarios para los casos de uso. Todas se limitan a la empresa del contexto verificado:
/// ningún método recibe una empresa, y un usuario o un rol de otra empresa se comporta como inexistente.
/// </summary>
public interface IConsultaUsuarios
{
    /// <summary>Busca usuarios con búsqueda parcial, filtros combinados con AND y paginación (FR-014 a FR-018).</summary>
    /// <param name="criterios">Criterios de búsqueda.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<PaginaConsultaUsuarios> BuscarAsync(CriteriosConsultaUsuarios criterios, CancellationToken cancellationToken);

    /// <summary>Obtiene un usuario de la empresa actual con su rol.</summary>
    /// <param name="usuarioId">Identificador solicitado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>null si no existe o no es accesible; ambos casos son indistinguibles (FR-003).</returns>
    Task<UsuarioConRol?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Roles de la empresa actual, asignables en el formulario y usados en el filtro (FR-015, FR-026).</summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<IReadOnlyList<RolReferencia>> ListarRolesAsignablesAsync(CancellationToken cancellationToken);

    /// <summary>Obtiene un rol de la empresa actual.</summary>
    /// <param name="rolId">Identificador del rol.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>null si no existe o no pertenece a la empresa actual.</returns>
    Task<RolReferencia?> ObtenerRolAsync(Guid rolId, CancellationToken cancellationToken);
}

/// <summary>Criterios de búsqueda del listado. No incluyen empresa.</summary>
/// <param name="Texto">Búsqueda parcial sobre nombre completo e identificador de acceso; null o vacío si no se busca.</param>
/// <param name="RolId">Filtro por rol; null para todos.</param>
/// <param name="Estado">Filtro por estado; null para todos.</param>
/// <param name="Pagina">Página solicitada, empezando en 1.</param>
/// <param name="TamanoPagina">Tamaño solicitado; null para el valor por defecto configurado.</param>
public sealed record CriteriosConsultaUsuarios(
    string? Texto,
    Guid? RolId,
    EstadoUsuario? Estado,
    int Pagina,
    int? TamanoPagina);

/// <summary>Una página de resultados de la búsqueda.</summary>
/// <param name="Elementos">Usuarios de la página, con su rol.</param>
/// <param name="Total">Coincidencias en la empresa actual con los mismos criterios.</param>
/// <param name="Pagina">Página devuelta.</param>
/// <param name="TamanoPagina">Tamaño de página aplicado.</param>
public sealed record PaginaConsultaUsuarios(
    IReadOnlyList<UsuarioConRol> Elementos,
    int Total,
    int Pagina,
    int TamanoPagina);

/// <summary>Un usuario con su rol.</summary>
/// <param name="Usuario">El usuario.</param>
/// <param name="Rol">Su rol; null si no tiene un rol reconocido (EC-10).</param>
public sealed record UsuarioConRol(Usuario Usuario, RolReferencia? Rol);
