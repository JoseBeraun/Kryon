using Kryon.Core.Usuarios;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kryon.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Tabla <c>AuditoriaUsuarios</c> (data-model.md), conceptualmente de solo inserción. Los permisos de base de datos
/// que lo garantizan se crean en una migración propia, no en esta configuración.
/// </summary>
public sealed class AuditoriaUsuarioConfiguracion : IEntityTypeConfiguration<AuditoriaUsuario>
{
    public void Configure(EntityTypeBuilder<AuditoriaUsuario> builder)
    {
        builder.ToTable("AuditoriaUsuarios");

        builder.HasKey(a => a.Id);

        // bigint identity: lo asigna la base de datos.
        builder.Property(a => a.Id).UseIdentityColumn();

        builder.Property(a => a.EmpresaId).IsRequired();
        builder.Property(a => a.ActorUsuarioId).IsRequired();
        builder.Property(a => a.OcurridoEn).IsRequired();

        // Los enums tienen byte como tipo subyacente: tinyint y tinyint null.
        builder.Property(a => a.TipoOperacion).IsRequired();
        builder.Property(a => a.UsuarioAfectadoId).IsRequired();
        builder.Property(a => a.OperacionSolicitada).IsRequired(false);

        builder.Property(a => a.CambiosJson).IsRequired(false);
    }
}
