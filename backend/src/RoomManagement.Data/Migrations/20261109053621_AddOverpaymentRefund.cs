using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOverpaymentRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOverpaymentRefund",
                schema: "dbo",
                table: "Refunds",
                type: "bit",
                nullable: false,
                defaultValue: false);
            migrationBuilder.Sql(BalanceViewSql(true));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BalanceViewSql(false));
            migrationBuilder.DropColumn(
                name: "IsOverpaymentRefund",
                schema: "dbo",
                table: "Refunds");
        }

        private static string BalanceViewSql(bool overpayment) => $@"
CREATE OR ALTER VIEW dbo.vwInvoiceBalances AS
SELECT i.Id AS InvoiceId, i.ContractId, i.RoomId, i.InvoiceType, i.Status,
       i.TotalAmount,
       CONVERT(decimal(18,2), COALESCE(a.TotalAdjustments, 0)) AS TotalAdjustments,
       CONVERT(decimal(18,2), COALESCE(p.TotalPaid, 0)) AS TotalPaid,
       CONVERT(decimal(18,2), COALESCE(r.TotalRefunded, 0)) AS TotalRefunded,
       CONVERT(decimal(18,2), COALESCE(d.TotalDepositApplied, 0)) AS TotalDepositApplied,
       CASE WHEN i.Status <> N'Issued' THEN CONVERT(decimal(18,2), 0)
            ELSE i.TotalAmount - CONVERT(decimal(18,2), COALESCE(a.TotalAdjustments, 0)) END AS EffectiveAmount,
       CASE WHEN i.Status <> N'Issued' THEN CONVERT(decimal(18,2), 0)
            ELSE i.TotalAmount - CONVERT(decimal(18,2), COALESCE(a.TotalAdjustments, 0)) END
           - CONVERT(decimal(18,2), COALESCE(p.TotalPaid, 0))
           + CASE WHEN i.InvoiceType = N'Deposit' THEN {(overpayment ? "CONVERT(decimal(18,2), COALESCE(r.OverpaymentRefunded, 0))" : "CONVERT(decimal(18,2), 0)")}
                  ELSE CONVERT(decimal(18,2), COALESCE(r.TotalRefunded, 0)) END
           - CONVERT(decimal(18,2), COALESCE(d.TotalDepositApplied, 0)) AS RemainingAmount
FROM dbo.Invoices AS i
OUTER APPLY (SELECT SUM(CONVERT(decimal(38,2), a.Amount)) AS TotalAdjustments
    FROM dbo.InvoiceAdjustments AS a WHERE a.InvoiceId = i.Id AND a.VoidedAt IS NULL) AS a
OUTER APPLY (SELECT SUM(CONVERT(decimal(38,2), p.VerifiedAmount)) AS TotalPaid
    FROM dbo.Payments AS p WHERE p.InvoiceId = i.Id AND p.Status = N'Success') AS p
OUTER APPLY (SELECT SUM(CONVERT(decimal(38,2), r.Amount)) AS TotalRefunded
    {(overpayment ? ", SUM(CASE WHEN r.IsOverpaymentRefund = 1 THEN CONVERT(decimal(38,2), r.Amount) ELSE CONVERT(decimal(38,2), 0) END) AS OverpaymentRefunded" : "")}
    FROM dbo.Refunds AS r WHERE r.InvoiceId = i.Id AND r.Status = N'Success') AS r
OUTER APPLY (SELECT SUM(CONVERT(decimal(38,2), d.Amount)) AS TotalDepositApplied
    FROM dbo.DepositApplications AS d WHERE d.TargetInvoiceId = i.Id) AS d;";
    }
}
