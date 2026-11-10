using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActivePropertyRulesVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "dbo",
                table: "ContractRulesVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Preserve the currently effective version for properties that already have rules.
            migrationBuilder.Sql(@"UPDATE version SET IsActive = 1
                FROM dbo.ContractRulesVersions version
                WHERE version.PropertyId IS NOT NULL AND version.VersionNumber = (
                    SELECT MAX(other.VersionNumber) FROM dbo.ContractRulesVersions other
                    WHERE other.PropertyId = version.PropertyId)");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesVersions_PropertyId",
                schema: "dbo",
                table: "ContractRulesVersions",
                column: "PropertyId",
                unique: true,
                filter: "[PropertyId] IS NOT NULL AND [IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractRulesVersions_PropertyId",
                schema: "dbo",
                table: "ContractRulesVersions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "dbo",
                table: "ContractRulesVersions");
        }
    }
}
