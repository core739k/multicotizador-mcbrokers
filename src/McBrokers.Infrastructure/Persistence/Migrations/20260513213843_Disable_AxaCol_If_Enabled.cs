using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McBrokers.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// One-shot data fix: deshabilita la fila AXA Colectividad si quedó
    /// con IsEnabled=true por un seed anterior al commit 0d61690 que la
    /// estableció en false por defecto. El seed es idempotente — no toca
    /// filas existentes — así que esta migración es la forma rastreable
    /// de reconciliar entornos viejos con la decisión de negocio.
    /// MCBrokers solo opera AXA DXN; AXA COL queda apagada hasta que
    /// negocio indique lo contrario.
    /// </summary>
    public partial class Disable_AxaCol_If_Enabled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Insurers] SET [IsEnabled] = 0 WHERE [Code] = 'AxaCol' AND [IsEnabled] = 1;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversa: re-habilita AXA COL. Solo aplicar si negocio cambia de opinión —
            // la migración inversa existe por simetría, no porque sea esperable correrla.
            migrationBuilder.Sql(
                "UPDATE [Insurers] SET [IsEnabled] = 1 WHERE [Code] = 'AxaCol';");
        }
    }
}
