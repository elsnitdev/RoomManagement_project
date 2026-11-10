using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyWideContractRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractRulesVersions_ContractId_VersionNumber",
                schema: "dbo",
                table: "ContractRulesVersions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractId",
                schema: "dbo",
                table: "ContractRulesVersions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                schema: "dbo",
                table: "ContractRulesVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesVersions_ContractId_VersionNumber",
                schema: "dbo",
                table: "ContractRulesVersions",
                columns: new[] { "ContractId", "VersionNumber" },
                unique: true,
                filter: "[ContractId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesVersions_PropertyId_VersionNumber",
                schema: "dbo",
                table: "ContractRulesVersions",
                columns: new[] { "PropertyId", "VersionNumber" },
                unique: true,
                filter: "[PropertyId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContractRulesVersion_Scope",
                schema: "dbo",
                table: "ContractRulesVersions",
                sql: "([ContractId] IS NULL AND [PropertyId] IS NOT NULL) OR ([ContractId] IS NOT NULL AND [PropertyId] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractRulesVersions_Properties_PropertyId",
                schema: "dbo",
                table: "ContractRulesVersions",
                column: "PropertyId",
                principalSchema: "dbo",
                principalTable: "Properties",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractRulesVersions_Properties_PropertyId",
                schema: "dbo",
                table: "ContractRulesVersions");

            migrationBuilder.DropIndex(
                name: "IX_ContractRulesVersions_ContractId_VersionNumber",
                schema: "dbo",
                table: "ContractRulesVersions");

            migrationBuilder.DropIndex(
                name: "IX_ContractRulesVersions_PropertyId_VersionNumber",
                schema: "dbo",
                table: "ContractRulesVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContractRulesVersion_Scope",
                schema: "dbo",
                table: "ContractRulesVersions");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                schema: "dbo",
                table: "ContractRulesVersions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractId",
                schema: "dbo",
                table: "ContractRulesVersions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesVersions_ContractId_VersionNumber",
                schema: "dbo",
                table: "ContractRulesVersions",
                columns: new[] { "ContractId", "VersionNumber" },
                unique: true);
        }
    }
}
