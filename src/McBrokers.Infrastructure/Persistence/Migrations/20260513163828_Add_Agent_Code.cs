using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McBrokers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Agent_Code : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentCode",
                table: "Agents",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Agents_AgentCode",
                table: "Agents",
                column: "AgentCode",
                unique: true,
                filter: "[AgentCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Agents_AgentCode",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "AgentCode",
                table: "Agents");
        }
    }
}
