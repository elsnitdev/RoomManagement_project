using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261113130000_InvoiceAdditionalCharge")]
public sealed class InvoiceAdditionalCharge : Migration
{
    private const string ViewSql = """
        ALTER VIEW dbo.vwInvoiceBalances AS
        SELECT i.Id AS InvoiceId, i.ContractId, i.RoomId, i.InvoiceType, i.Status,
               i.TotalAmount,
               CONVERT(decimal(18,2), COALESCE(a.TotalAdjustments, 0)) AS TotalAdjustments,
               CONVERT(decimal(18,2), COALESCE(p.TotalPaid, 0)) AS TotalPaid,
               CONVERT(decimal(18,2), COALESCE(r.TotalRefunded, 0)) AS TotalRefunded,
               CONVERT(decimal(18,2), COALESCE(d.TotalDepositApplied, 0)) AS TotalDepositApplied,
               CASE WHEN i.Status <> N'Issued' THEN CONVERT(decimal(18,2), 0)
                    ELSE i.TotalAmount - CONVERT(decimal(18,2), COALESCE(a.TotalAdjustments, 0))
               END AS EffectiveAmount,
               CASE WHEN i.Status <> N'Issued' THEN CONVERT(decimal(18,2), 0)
                    ELSE i.TotalAmount - CONVERT(decimal(18,2), COALESCE(a.TotalAdjustments, 0))
               END - CONVERT(decimal(18,2), COALESCE(p.TotalPaid, 0))
                   + CASE WHEN i.InvoiceType = N'Deposit' THEN CONVERT(decimal(18,2), 0)
                          ELSE CONVERT(decimal(18,2), COALESCE(r.TotalRefunded, 0)) END
                   - CONVERT(decimal(18,2), COALESCE(d.TotalDepositApplied, 0)) AS RemainingAmount
        FROM dbo.Invoices AS i
        OUTER APPLY (
            SELECT SUM(CASE WHEN a.AdjustmentType = N'AdditionalCharge'
                            THEN -CONVERT(decimal(38,2), a.Amount)
                            ELSE CONVERT(decimal(38,2), a.Amount) END) AS TotalAdjustments
            FROM dbo.InvoiceAdjustments AS a
            WHERE a.InvoiceId = i.Id AND a.VoidedAt IS NULL
        ) AS a
        OUTER APPLY (
            SELECT SUM(CONVERT(decimal(38,2), p.VerifiedAmount)) AS TotalPaid
            FROM dbo.Payments AS p
            WHERE p.InvoiceId = i.Id AND p.Status = N'Success'
        ) AS p
        OUTER APPLY (
            SELECT SUM(CONVERT(decimal(38,2), r.Amount)) AS TotalRefunded
            FROM dbo.Refunds AS r
            WHERE r.InvoiceId = i.Id AND r.Status = N'Success'
        ) AS r
        OUTER APPLY (
            SELECT SUM(CONVERT(decimal(38,2), d.Amount)) AS TotalDepositApplied
            FROM dbo.DepositApplications AS d
            WHERE d.TargetInvoiceId = i.Id
        ) AS d;
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_InvoiceAdjustments_Type", "dbo", "InvoiceAdjustments");
        migrationBuilder.AddCheckConstraint("CK_InvoiceAdjustments_Type", "dbo", "InvoiceAdjustments",
            "AdjustmentType IN (N'Discount', N'Correction', N'WriteOff', N'AdditionalCharge')");
        migrationBuilder.Sql("EXEC(N'" + ViewSql.Replace("'", "''") + "')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("EXEC(N'" + ViewSql.Replace("a.AdjustmentType = N'AdditionalCharge'", "1 = 0")
            .Replace("'", "''") + "')");
        migrationBuilder.DropCheckConstraint("CK_InvoiceAdjustments_Type", "dbo", "InvoiceAdjustments");
        migrationBuilder.AddCheckConstraint("CK_InvoiceAdjustments_Type", "dbo", "InvoiceAdjustments",
            "AdjustmentType IN (N'Discount', N'Correction', N'WriteOff')");
    }
}
