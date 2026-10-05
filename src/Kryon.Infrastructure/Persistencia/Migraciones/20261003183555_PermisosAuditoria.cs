using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kryon.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Permisos de la auditoría (FR-045, research §R7): el rol de base de datos de la aplicación puede insertar y
    /// leer <c>AuditoriaUsuarios</c>, pero tiene denegado modificarla o borrarla. Solo define el rol y sus permisos:
    /// asignarle el usuario de base de datos con el que se conecta la aplicación es un paso del despliegue.
    /// </summary>
    public partial class PermisosAuditoria : Migration
    {
        /// <summary>
        /// Nombre técnico del rol de la aplicación. Los artefactos no fijan uno: es una decisión de infraestructura.
        /// </summary>
        private const string RolAplicacion = "kryon_aplicacion";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"CREATE ROLE [{RolAplicacion}];");
            migrationBuilder.Sql($"GRANT SELECT, INSERT ON OBJECT::dbo.AuditoriaUsuarios TO [{RolAplicacion}];");

            // DENY explícito: prevalece sobre cualquier GRANT que el rol o sus miembros reciban por otra vía.
            migrationBuilder.Sql($"DENY UPDATE, DELETE ON OBJECT::dbo.AuditoriaUsuarios TO [{RolAplicacion}];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // REVOKE retira tanto los GRANT como los DENY; después el rol queda sin permisos y se elimina.
            migrationBuilder.Sql($"REVOKE SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.AuditoriaUsuarios FROM [{RolAplicacion}];");
            migrationBuilder.Sql($"DROP ROLE [{RolAplicacion}];");
        }
    }
}
