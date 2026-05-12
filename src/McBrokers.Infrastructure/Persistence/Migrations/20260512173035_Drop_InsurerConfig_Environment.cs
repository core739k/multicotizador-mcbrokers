using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McBrokers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Drop_InsurerConfig_Environment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_InsurerConfigs_Insurer_Environment",
                table: "InsurerConfigs");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "InsurerConfigs");

            migrationBuilder.CreateIndex(
                name: "UX_InsurerConfigs_Insurer",
                table: "InsurerConfigs",
                column: "InsurerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_InsurerConfigs_Insurer",
                table: "InsurerConfigs");

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "InsurerConfigs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UX_InsurerConfigs_Insurer_Environment",
                table: "InsurerConfigs",
                columns: new[] { "InsurerId", "Environment" },
                unique: true);
        }
    }
}
