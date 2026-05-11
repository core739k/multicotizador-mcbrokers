using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McBrokers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Quotations_Flow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnownInsurerErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsurerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalMessagePattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HumanMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SuggestedAction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AutoRetry = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownInsurerErrors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuotationInsurerResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsurerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorCategory = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ErrorMessageHuman = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PremiumTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PremiumNet = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Tax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Fees = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: false),
                    ExternalQuoteRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestBlobRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResponseBlobRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationInsurerResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Package = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PaymentMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValuationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SumInsured = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CustomerSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpectedResultsCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_KnownInsurerErrors_Insurer_Code",
                table: "KnownInsurerErrors",
                columns: new[] { "InsurerId", "ExternalCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationInsurerResults_QuotationId",
                table: "QuotationInsurerResults",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "UX_QuotationInsurerResults_Quotation_Insurer",
                table: "QuotationInsurerResults",
                columns: new[] { "QuotationId", "InsurerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_AgentId",
                table: "Quotations",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_CorrelationId",
                table: "Quotations",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_CreatedAt",
                table: "Quotations",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnownInsurerErrors");

            migrationBuilder.DropTable(
                name: "QuotationInsurerResults");

            migrationBuilder.DropTable(
                name: "Quotations");
        }
    }
}
