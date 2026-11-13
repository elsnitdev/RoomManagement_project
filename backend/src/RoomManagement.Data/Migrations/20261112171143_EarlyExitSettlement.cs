using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class EarlyExitSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_MeterReadings_Period",
                schema: "dbo",
                table: "MeterReadings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.AddColumn<Guid>(
                name: "ContractId",
                schema: "dbo",
                table: "MeterReadings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositDisposition",
                schema: "dbo",
                table: "ContractSettlements",
                type: "nvarchar(20)",
                nullable: false,
                defaultValue: "NoDeposit");

            migrationBuilder.AddColumn<DateOnly>(
                name: "HandoverOn",
                schema: "dbo",
                table: "Contracts",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_MeterReadings_ContractPeriod",
                schema: "dbo",
                table: "MeterReadings",
                columns: new[] { "ContractId", "MeterType", "Year", "Month" },
                unique: true,
                filter: "ContractId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_MeterReadings_Period",
                schema: "dbo",
                table: "MeterReadings",
                columns: new[] { "RoomId", "MeterType", "Year", "Month" },
                unique: true,
                filter: "ContractId IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContractSettlements_Disposition",
                schema: "dbo",
                table: "ContractSettlements",
                sql: "(DepositDisposition IN (N'Refunded', N'Offset', N'NoDeposit'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts",
                sql: "(Status IN (N'Draft', N'Active', N'Expired', N'Terminated', N'Cancelled', N'PendingMoveOut', N'PendingSettlement'))");

            migrationBuilder.AddForeignKey(
                name: "FK_MeterReadings_Contracts_ContractId",
                schema: "dbo",
                table: "MeterReadings",
                column: "ContractId",
                principalSchema: "dbo",
                principalTable: "Contracts",
                principalColumn: "Id");

            migrationBuilder.Sql(@"CREATE OR ALTER VIEW dbo.vwContractDepositBalances AS
SELECT i.ContractId, i.Id AS DepositInvoiceId,
       b.TotalPaid AS DepositCollected,
       b.TotalRefunded + CONVERT(decimal(18,2), COALESCE(s.RefundAmount, 0)) AS DepositRefunded,
       CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0)) AS DepositApplied,
       b.TotalPaid - b.TotalRefunded - CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0))
           - CONVERT(decimal(18,2), COALESCE(s.RefundAmount, 0)) AS DepositHeld
FROM dbo.Invoices AS i
JOIN dbo.vwInvoiceBalances AS b ON b.InvoiceId = i.Id
OUTER APPLY (SELECT SUM(CONVERT(decimal(38,2), a.Amount)) AS TotalApplied
    FROM dbo.DepositApplications AS a WHERE a.ContractId = i.ContractId) AS d
OUTER APPLY (SELECT SUM(CONVERT(decimal(38,2), x.RefundAmount)) AS RefundAmount
    FROM dbo.ContractSettlements AS x WHERE x.ContractId = i.ContractId) AS s
WHERE i.InvoiceType = N'Deposit' AND i.Status = N'Issued';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE OR ALTER VIEW dbo.vwContractDepositBalances AS
SELECT i.ContractId, i.Id AS DepositInvoiceId,
       b.TotalPaid AS DepositCollected, b.TotalRefunded AS DepositRefunded,
       CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0)) AS DepositApplied,
       b.TotalPaid - b.TotalRefunded - CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0)) AS DepositHeld
FROM dbo.Invoices AS i
JOIN dbo.vwInvoiceBalances AS b ON b.InvoiceId = i.Id
OUTER APPLY (SELECT SUM(CONVERT(decimal(38,2), a.Amount)) AS TotalApplied
    FROM dbo.DepositApplications AS a WHERE a.ContractId = i.ContractId) AS d
WHERE i.InvoiceType = N'Deposit' AND i.Status = N'Issued';");
            migrationBuilder.DropForeignKey(
                name: "FK_MeterReadings_Contracts_ContractId",
                schema: "dbo",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "UX_MeterReadings_ContractPeriod",
                schema: "dbo",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "UX_MeterReadings_Period",
                schema: "dbo",
                table: "MeterReadings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContractSettlements_Disposition",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ContractId",
                schema: "dbo",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "DepositDisposition",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.DropColumn(
                name: "HandoverOn",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "UX_MeterReadings_Period",
                schema: "dbo",
                table: "MeterReadings",
                columns: new[] { "RoomId", "MeterType", "Year", "Month" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts",
                sql: "(Status IN (N'Draft', N'Active', N'Expired', N'Terminated', N'Cancelled'))");
        }
    }
}
