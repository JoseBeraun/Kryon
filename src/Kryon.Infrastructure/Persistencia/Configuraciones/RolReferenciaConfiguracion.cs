using System.Text.Json;

using Kryon.Core.Usuarios;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kryon.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Tabla <c>Roles</c> de referencia (data-model.md). Esta feature solo la lee: el catálogo de roles, que la
/// administra, está fuera de alcance.
/// </summary>
public sealed class RolReferenciaConfiguracion : IEntityTypeConfiguration<RolReferencia>
{
    public void Configure(EntityTypeBuilder<RolReferencia> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        // Lo asigna el catálogo de roles.
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.EmpresaId).IsRequired();

        // Tipo y longitud los define el catálogo de roles; sin tope propio de esta feature.
        builder.Property(r => r.Nombre).IsRequired();

        // Decisión técnica de almacenamiento: los nombres de capacidad se guardan como un arreglo JSON en una sola
        // columna. No define ningún catálogo de permisos.
        builder.Property(r => r.Capacidades)
            .IsRequired()
            .HasConversion(
                capacidades => JsonSerializer.Serialize(capacidades, (JsonSerializerOptions?)null),
                json => DeserializarCapacidades(json),
                new ValueComparer<IReadOnlyCollection<string>>(
                    (a, b) => a!.SequenceEqual(b!),
                    capacidades => capacidades.Aggregate(0, (hash, capacidad) => HashCode.Combine(hash, capacidad)),
                    capacidades => DeserializarCapacidades(JsonSerializer.Serialize(capacidades, (JsonSerializerOptions?)null))));
    }

    private static IReadOnlyCollection<string> DeserializarCapacidades(string json) =>
        (JsonSerializer.Deserialize<string[]>(json) ?? []).AsReadOnly();
}
