using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementOneOffIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreationSource",
                schema: "dbo",
                table: "Invoices",
                type: "nvarchar(40)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementChargeFingerprint",
                schema: "dbo",
                table: "Invoices",
                type: "char(64)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementOperationKey",
                schema: "dbo",
                table: "Invoices",
                type: "nvarchar(100)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementRequestHash",
                schema: "dbo",
                table: "Invoices",
                type: "char(64)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_SettlementCharge",
                schema: "dbo",
                table: "Invoices",
                columns: new[] { "ContractId", "SettlementChargeFingerprint" },
                unique: true,
                filter: "CreationSource = N'EarlyExitSettlement' AND Status <> N'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_SettlementOperation",
                schema: "dbo",
                table: "Invoices",
                columns: new[] { "ContractId", "SettlementOperationKey" },
                unique: true,
                filter: "SettlementOperationKey IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_SettlementSource",
                schema: "dbo",
                table: "Invoices",
                sql: "((CreationSource IS NULL AND SettlementOperationKey IS NULL AND SettlementRequestHash IS NULL AND SettlementChargeFingerprint IS NULL) OR (CreationSource = N'EarlyExitSettlement' AND SettlementOperationKey IS NOT NULL AND SettlementRequestHash IS NOT NULL AND SettlementChargeFingerprint IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Invoices_SettlementCharge",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "UX_Invoices_SettlementOperation",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_SettlementSource",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreationSource",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SettlementChargeFingerprint",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SettlementOperationKey",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SettlementRequestHash",
                schema: "dbo",
                table: "Invoices");
        }
    }
}
