using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIHU.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeneracionesHU",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TextoEntrada = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    TextoProcesado = table.Column<string>(type: "nvarchar(max)", maxLength: 15000, nullable: true),
                    Proyecto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Idioma = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "es"),
                    TotalHUs = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Exitoso = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MensajeError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    DuracionMs = table.Column<int>(type: "int", nullable: false),
                    ModeloIA = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TokensConsumidos = table.Column<int>(type: "int", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClientIP = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneracionesHU", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoriasUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Como = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quiero = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Para = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GeneracionHUId = table.Column<int>(type: "int", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoriasUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoriasUsuario_GeneracionesHU_GeneracionHUId",
                        column: x => x.GeneracionHUId,
                        principalTable: "GeneracionesHU",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CriteriosAceptacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistoriaUsuarioId = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EsObligatorio = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriteriosAceptacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CriteriosAceptacion_HistoriasUsuario_HistoriaUsuarioId",
                        column: x => x.HistoriaUsuarioId,
                        principalTable: "HistoriasUsuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TareasTecnicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistoriaUsuarioId = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EstaCompletada = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TareasTecnicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TareasTecnicas_HistoriasUsuario_HistoriaUsuarioId",
                        column: x => x.HistoriaUsuarioId,
                        principalTable: "HistoriasUsuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriteriosAceptacion_HistoriaUsuarioId",
                table: "CriteriosAceptacion",
                column: "HistoriaUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriasUsuario_GeneracionHUId",
                table: "HistoriasUsuario",
                column: "GeneracionHUId");

            migrationBuilder.CreateIndex(
                name: "IX_TareasTecnicas_HistoriaUsuarioId",
                table: "TareasTecnicas",
                column: "HistoriaUsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CriteriosAceptacion");

            migrationBuilder.DropTable(
                name: "TareasTecnicas");

            migrationBuilder.DropTable(
                name: "HistoriasUsuario");

            migrationBuilder.DropTable(
                name: "GeneracionesHU");
        }
    }
}
