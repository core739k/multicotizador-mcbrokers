using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McBrokers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_Master_Mappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsurerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowsTotal = table.Column<int>(type: "int", nullable: false),
                    RowsAutoApproved = table.Column<int>(type: "int", nullable: false),
                    RowsPendingReview = table.Column<int>(type: "int", nullable: false),
                    RowsRejected = table.Column<int>(type: "int", nullable: false),
                    ImportedByAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransmissionSynonyms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Canonical = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransmissionSynonyms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Transmission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Doors = table.Column<int>(type: "int", nullable: false),
                    Cylinders = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrandSynonyms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SynonymText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandSynonyms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandSynonyms_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleInsurerMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsurerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalClave = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InsurerBrandRaw = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InsurerModelRaw = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InsurerVersionRaw = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ReviewState = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedByAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleInsurerMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleInsurerMappings_VehicleMasters_VehicleMasterId",
                        column: x => x.VehicleMasterId,
                        principalTable: "VehicleMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Brands_CanonicalName",
                table: "Brands",
                column: "CanonicalName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrandSynonyms_BrandId",
                table: "BrandSynonyms",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandSynonyms_SynonymText",
                table: "BrandSynonyms",
                column: "SynonymText");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogImportBatches_StartedAt",
                table: "CatalogImportBatches",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "UX_TransmissionSynonyms_Text",
                table: "TransmissionSynonyms",
                column: "Text",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleInsurerMappings_Master_Insurer",
                table: "VehicleInsurerMappings",
                columns: new[] { "VehicleMasterId", "InsurerId" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleInsurerMappings_ReviewState",
                table: "VehicleInsurerMappings",
                column: "ReviewState");

            migrationBuilder.CreateIndex(
                name: "UX_VehicleInsurerMappings_Insurer_ExternalClave",
                table: "VehicleInsurerMappings",
                columns: new[] { "InsurerId", "ExternalClave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleMasters_Year_Brand",
                table: "VehicleMasters",
                columns: new[] { "Year", "Brand" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleMasters_Year_Brand_Model_Version",
                table: "VehicleMasters",
                columns: new[] { "Year", "Brand", "Model", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandSynonyms");

            migrationBuilder.DropTable(
                name: "CatalogImportBatches");

            migrationBuilder.DropTable(
                name: "TransmissionSynonyms");

            migrationBuilder.DropTable(
                name: "VehicleInsurerMappings");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "VehicleMasters");
        }
    }
}
