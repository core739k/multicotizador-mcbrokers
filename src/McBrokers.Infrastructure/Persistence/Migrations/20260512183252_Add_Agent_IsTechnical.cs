using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McBrokers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Agent_IsTechnical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTechnical",
                table: "Agents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Bootstrap inicial: el primer admin técnico es econtreras@mcbrokers.com.mx.
            // Tras este seed, la UI /Admin/Agents toggle gobierna el flag.
            migrationBuilder.Sql(
                "UPDATE [Agents] SET [IsTechnical] = 1 WHERE [Email] = 'econtreras@mcbrokers.com.mx';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTechnical",
                table: "Agents");
        }
    }
}
