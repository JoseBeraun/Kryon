using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kryon.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Row-Level Security por empresa (research §R3, segunda capa): una función de predicado y una política de
    /// seguridad con predicados de filtro y de bloqueo sobre <c>Usuarios</c> y <c>AuditoriaUsuarios</c>, basados en
    /// <c>SESSION_CONTEXT(N'EmpresaId')</c>, que fija <c>ContextoEmpresaInterceptor</c> al abrir cada conexión.
    /// </summary>
    public partial class SeguridadFilas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cada sentencia va dentro de EXEC(N'...') porque el script idempotente las envuelve en IF ... BEGIN ... END
            // y CREATE FUNCTION debe ser la única sentencia de su lote.
            //
            // Sin SESSION_CONTEXT (o con un valor que no sea un uniqueidentifier) TRY_CAST devuelve NULL, la
            // comparación no es verdadera y la función no devuelve filas: se deniega el acceso (fallo cerrado).
            migrationBuilder.Sql("""
                EXEC(N'CREATE FUNCTION dbo.PredicadoAccesoEmpresa(@EmpresaId uniqueidentifier)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN
                    SELECT 1 AS Permitido
                    WHERE @EmpresaId = TRY_CAST(SESSION_CONTEXT(N''EmpresaId'') AS uniqueidentifier);');
                """);

            // FILTER: solo se leen, actualizan o eliminan filas de la empresa de la sesión.
            // BLOCK AFTER INSERT / AFTER UPDATE: no se puede escribir una fila que quede en otra empresa.
            // BEFORE UPDATE / BEFORE DELETE no se añaden: el filtro ya impide alcanzar filas de otra empresa.
            migrationBuilder.Sql("""
                EXEC(N'CREATE SECURITY POLICY dbo.PoliticaAislamientoEmpresa
                    ADD FILTER PREDICATE dbo.PredicadoAccesoEmpresa(EmpresaId) ON dbo.Usuarios,
                    ADD BLOCK PREDICATE dbo.PredicadoAccesoEmpresa(EmpresaId) ON dbo.Usuarios AFTER INSERT,
                    ADD BLOCK PREDICATE dbo.PredicadoAccesoEmpresa(EmpresaId) ON dbo.Usuarios AFTER UPDATE,
                    ADD FILTER PREDICATE dbo.PredicadoAccesoEmpresa(EmpresaId) ON dbo.AuditoriaUsuarios,
                    ADD BLOCK PREDICATE dbo.PredicadoAccesoEmpresa(EmpresaId) ON dbo.AuditoriaUsuarios AFTER INSERT,
                    ADD BLOCK PREDICATE dbo.PredicadoAccesoEmpresa(EmpresaId) ON dbo.AuditoriaUsuarios AFTER UPDATE
                WITH (STATE = ON, SCHEMABINDING = ON);');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La política se elimina antes que la función, que está ligada a ella por SCHEMABINDING.
            migrationBuilder.Sql("DROP SECURITY POLICY dbo.PoliticaAislamientoEmpresa;");
            migrationBuilder.Sql("DROP FUNCTION dbo.PredicadoAccesoEmpresa;");
        }
    }
}
