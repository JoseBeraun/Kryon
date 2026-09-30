namespace Kryon.Core.Usuarios;

/// <summary>
/// Referencia de solo lectura a un rol de una empresa. El catálogo de roles está fuera de alcance:
/// esta feature no crea, modifica ni elimina roles.
/// </summary>
public sealed class RolReferencia
{
    /// <param name="id">Identificador del rol.</param>
    /// <param name="empresaId">Empresa a la que pertenece el rol.</param>
    /// <param name="nombre">Nombre definido por el catálogo de roles.</param>
    /// <param name="capacidades">Nombres de las capacidades del rol; se copian al construir.</param>
    public RolReferencia(Guid id, Guid empresaId, string nombre, IEnumerable<string> capacidades)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(empresaId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(nombre);
        ArgumentNullException.ThrowIfNull(capacidades);

        Id = id;
        EmpresaId = empresaId;
        Nombre = nombre;
        Capacidades = capacidades.ToArray().AsReadOnly();
    }

    public Guid Id { get; }

    public Guid EmpresaId { get; }

    public string Nombre { get; }

    public IReadOnlyCollection<string> Capacidades { get; }
}
