using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelWithDepositFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts",
                sql: "(Status IN (N'Draft', N'PendingTenantConfirmation', N'PendingOperatorConfirmation', N'PendingStart', N'PendingCancelSettlement', N'Active', N'Expired', N'Terminated', N'Cancelled', N'PendingMoveOut', N'PendingSettlement'))");

            migrationBuilder.AddColumn<decimal>(
                name: "ForfeitedAmount",
                schema: "dbo",
                table: "ContractSettlements",
                type: "decimal(18,2)",
                nullable: false,
                defaultValueSql: "0");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContractSettlements_Amounts",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContractSettlements_Amounts",
                schema: "dbo",
                table: "ContractSettlements",
                sql: "(DepositAmount >= 0 AND OutstandingAmount >= 0 AND DamageFee >= 0 AND OtherFee >= 0 AND RefundAmount >= 0 AND ForfeitedAmount >= 0)");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContractSettlements_Disposition",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContractSettlements_Disposition",
                schema: "dbo",
                table: "ContractSettlements",
                sql: "(DepositDisposition IN (N'Refunded', N'Offset', N'NoDeposit', N'CancelRefunded', N'CancelPartialRefund', N'CancelForfeited'))");

            migrationBuilder.Sql(@"CREATE OR ALTER VIEW dbo.vwContractDepositBalances AS
SELECT i.ContractId, i.Id AS DepositInvoiceId,
       b.TotalPaid AS DepositCollected,
       b.TotalRefunded + CONVERT(decimal(18,2), COALESCE(s.RefundAmount, 0)) AS DepositRefunded,
       CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0)) AS DepositApplied,
       b.TotalPaid - b.TotalRefunded - CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0))
           - CONVERT(decimal(18,2), COALESCE(s.RefundAmount, 0))
           - CONVERT(decimal(18,2), COALESCE(s.ForfeitedAmount, 0)) AS DepositHeld
FROM dbo.Invoices AS i
JOIN dbo.vwInvoiceBalances AS b ON b.InvoiceId = i.Id
OUTER APPLY (SELECT SUM(CONVERT(decimal(38,2), a.Amount)) AS TotalApplied
    FROM dbo.DepositApplications AS a WHERE a.ContractId = i.ContractId) AS d
OUTER APPLY (SELECT SUM(CONVERT(decimal(38,2), x.RefundAmount)) AS RefundAmount,
                    SUM(CONVERT(decimal(38,2), x.ForfeitedAmount)) AS ForfeitedAmount
    FROM dbo.ContractSettlements AS x WHERE x.ContractId = i.ContractId) AS s
WHERE i.InvoiceType = N'Deposit' AND i.Status = N'Issued';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContractSettlements_Disposition",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContractSettlements_Disposition",
                schema: "dbo",
                table: "ContractSettlements",
                sql: "(DepositDisposition IN (N'Refunded', N'Offset', N'NoDeposit'))");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContractSettlements_Amounts",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContractSettlements_Amounts",
                schema: "dbo",
                table: "ContractSettlements",
                sql: "(DepositAmount >= 0 AND OutstandingAmount >= 0 AND DamageFee >= 0 AND OtherFee >= 0 AND RefundAmount >= 0)");

            migrationBuilder.DropColumn(
                name: "ForfeitedAmount",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts",
                sql: "(Status IN (N'Draft', N'PendingTenantConfirmation', N'PendingOperatorConfirmation', N'PendingStart', N'Active', N'Expired', N'Terminated', N'Cancelled', N'PendingMoveOut', N'PendingSettlement'))");
        }
    }
}
