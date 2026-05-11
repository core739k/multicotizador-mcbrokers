using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McBrokers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Admin_Insurers_Audit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Insurers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insurers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InsurerConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsurerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EndpointUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BusinessNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AgentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    KeyVaultSecretName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsurerConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InsurerConfigs_Insurers_InsurerId",
                        column: x => x.InsurerId,
                        principalTable: "Insurers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CorrelationId",
                table: "AuditLog",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CreatedAt",
                table: "AuditLog",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Entity",
                table: "AuditLog",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "UX_InsurerConfigs_Insurer_Environment",
                table: "InsurerConfigs",
                columns: new[] { "InsurerId", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Insurers_Code",
                table: "Insurers",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "InsurerConfigs");

            migrationBuilder.DropTable(
                name: "Insurers");
        }
    }
}
