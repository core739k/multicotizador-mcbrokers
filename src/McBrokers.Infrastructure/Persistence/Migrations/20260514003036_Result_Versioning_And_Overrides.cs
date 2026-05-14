using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McBrokers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Result_Versioning_And_Overrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_QuotationInsurerResults_Quotation_Insurer",
                table: "QuotationInsurerResults");

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "QuotationInsurerResults",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Override_DMPct",
                table: "QuotationInsurerResults",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Override_GMOSumInsured",
                table: "QuotationInsurerResults",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Override_RTPct",
                table: "QuotationInsurerResults",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Override_Valuation",
                table: "QuotationInsurerResults",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Override_VehicleMasterId",
                table: "QuotationInsurerResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "QuotationInsurerResults",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "UX_QuotationInsurerResults_Quotation_Insurer_Current",
                table: "QuotationInsurerResults",
                columns: new[] { "QuotationId", "InsurerId" },
                unique: true,
                filter: "[IsCurrent] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_QuotationInsurerResults_Quotation_Insurer_Current",
                table: "QuotationInsurerResults");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "QuotationInsurerResults");

            migrationBuilder.DropColumn(
                name: "Override_DMPct",
                table: "QuotationInsurerResults");

            migrationBuilder.DropColumn(
                name: "Override_GMOSumInsured",
                table: "QuotationInsurerResults");

            migrationBuilder.DropColumn(
                name: "Override_RTPct",
                table: "QuotationInsurerResults");

            migrationBuilder.DropColumn(
                name: "Override_Valuation",
                table: "QuotationInsurerResults");

            migrationBuilder.DropColumn(
                name: "Override_VehicleMasterId",
                table: "QuotationInsurerResults");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "QuotationInsurerResults");

            migrationBuilder.CreateIndex(
                name: "UX_QuotationInsurerResults_Quotation_Insurer",
                table: "QuotationInsurerResults",
                columns: new[] { "QuotationId", "InsurerId" },
                unique: true);
        }
    }
}
