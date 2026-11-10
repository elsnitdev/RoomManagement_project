using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractRulesOnline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OnlineRulesRequired",
                schema: "dbo",
                table: "ContractTenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ContractRulesVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRulesVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractRulesVersions_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "dbo",
                        principalTable: "Contracts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractRulesVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractRulesAcceptances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RulesVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRulesAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractRulesAcceptances_ContractRulesVersions_RulesVersionId",
                        column: x => x.RulesVersionId,
                        principalSchema: "dbo",
                        principalTable: "ContractRulesVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractRulesAcceptances_ContractTenants_ContractTenantId",
                        column: x => x.ContractTenantId,
                        principalSchema: "dbo",
                        principalTable: "ContractTenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractRulesAcceptances_Users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesAcceptances_AcceptedByUserId",
                schema: "dbo",
                table: "ContractRulesAcceptances",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesAcceptances_ContractTenantId",
                schema: "dbo",
                table: "ContractRulesAcceptances",
                column: "ContractTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesAcceptances_RulesVersionId_ContractTenantId",
                schema: "dbo",
                table: "ContractRulesAcceptances",
                columns: new[] { "RulesVersionId", "ContractTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesVersions_ContractId_VersionNumber",
                schema: "dbo",
                table: "ContractRulesVersions",
                columns: new[] { "ContractId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesVersions_CreatedByUserId",
                schema: "dbo",
                table: "ContractRulesVersions",
                column: "CreatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractRulesAcceptances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ContractRulesVersions",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "OnlineRulesRequired",
                schema: "dbo",
                table: "ContractTenants");
        }
    }
}
