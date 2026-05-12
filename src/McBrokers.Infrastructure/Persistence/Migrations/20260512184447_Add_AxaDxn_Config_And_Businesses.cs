using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McBrokers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_AxaDxn_Config_And_Businesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AxaDxnConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsurerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Tarifa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TarifaPickup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descuento = table.Column<int>(type: "int", nullable: false),
                    DescuentoPickup = table.Column<int>(type: "int", nullable: false),
                    MesPolizaDefault = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AxaDxnConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AxaDxnConfigs_Insurers_InsurerId",
                        column: x => x.InsurerId,
                        principalTable: "Insurers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AxaDxnBusinesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AxaDxnConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PolizaAutos = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PolizaPickup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Mes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AxaDxnBusinesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AxaDxnBusinesses_AxaDxnConfigs_AxaDxnConfigId",
                        column: x => x.AxaDxnConfigId,
                        principalTable: "AxaDxnConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AxaDxnBusinesses_Config_Nombre",
                table: "AxaDxnBusinesses",
                columns: new[] { "AxaDxnConfigId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AxaDxnConfigs_Insurer",
                table: "AxaDxnConfigs",
                column: "InsurerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AxaDxnBusinesses");

            migrationBuilder.DropTable(
                name: "AxaDxnConfigs");
        }
    }
}
