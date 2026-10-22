using Kryon.Core.Usuarios;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kryon.Infrastructure.Persistencia.Configuraciones;

/// <summary>Tabla <c>Usuarios</c> (data-model.md).</summary>
public sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    /// <summary>
    /// Intercalación sin distinguir mayúsculas ni acentos para la búsqueda (research §R6). El nombre concreto es una
    /// decisión técnica de implementación: los artefactos solo exigen <c>_CI_AI</c>.
    /// </summary>
    public const string IntercalacionBusqueda = "Latin1_General_100_CI_AI";

    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");

        builder.HasKey(u => u.Id);

        // Lo genera el dominio al crear el usuario.
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.EmpresaId).IsRequired();

        // Tamaños de columna provisionales (plan, Technical Context); la longitud funcional la valida OpcionesUsuarios.
        builder.Property(u => u.NombreCompleto)
            .IsRequired()
            .HasMaxLength(200)
            .UseCollation(IntercalacionBusqueda);

        builder.Property(u => u.IdentificadorAcceso)
            .IsRequired()
            .HasMaxLength(256)
            .UseCollation(IntercalacionBusqueda);

        builder.Property(u => u.RolId).IsRequired();

        // EstadoUsuario tiene byte como tipo subyacente: se guarda como tinyint (Activo = 1, Inactivo = 2).
        builder.Property(u => u.Estado).IsRequired();

        builder.Property(u => u.Version).IsRowVersion();

        builder.Property(u => u.CreadoEn).IsRequired();
        builder.Property(u => u.ModificadoEn).IsRequired();

        builder.HasIndex(u => new { u.EmpresaId, u.NombreCompleto, u.Id });

        // No es único: el ámbito de la unicidad del identificador es DEP-3.
        builder.HasIndex(u => new { u.EmpresaId, u.IdentificadorAcceso });

        builder.HasIndex(u => new { u.EmpresaId, u.Estado, u.RolId });
    }
}
