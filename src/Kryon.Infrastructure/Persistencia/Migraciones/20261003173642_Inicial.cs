using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kryon.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditoriaUsuarios",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TipoOperacion = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioAfectadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperacionSolicitada = table.Column<byte>(type: "tinyint", nullable: true),
                    CambiosJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaUsuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Capacidades = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    IdentificadorAcceso = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    RolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaId_Estado_RolId",
                table: "Usuarios",
                columns: new[] { "EmpresaId", "Estado", "RolId" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaId_IdentificadorAcceso",
                table: "Usuarios",
                columns: new[] { "EmpresaId", "IdentificadorAcceso" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaId_NombreCompleto_Id",
                table: "Usuarios",
                columns: new[] { "EmpresaId", "NombreCompleto", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriaUsuarios");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
