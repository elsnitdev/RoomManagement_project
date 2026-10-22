namespace RoomManagement.Data.Migrations;

public partial class InitialSchema
{
    private const string vwInvoiceBalancesSql = @"CREATE VIEW dbo.vwInvoiceBalances AS
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
    SELECT SUM(CONVERT(decimal(38,2), a.Amount)) AS TotalAdjustments
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
) AS d;";

    private const string vwContractDepositBalancesSql = @"CREATE VIEW dbo.vwContractDepositBalances AS
SELECT i.ContractId, i.Id AS DepositInvoiceId,
       b.TotalPaid AS DepositCollected,
       b.TotalRefunded AS DepositRefunded,
       CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0)) AS DepositApplied,
       b.TotalPaid - b.TotalRefunded - CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0)) AS DepositHeld
FROM dbo.Invoices AS i
JOIN dbo.vwInvoiceBalances AS b ON b.InvoiceId = i.Id
OUTER APPLY (
    SELECT SUM(CONVERT(decimal(38,2), d.Amount)) AS TotalApplied
    FROM dbo.DepositApplications AS d
    WHERE d.ContractId = i.ContractId
) AS d
WHERE i.InvoiceType = N'Deposit' AND i.Status = N'Issued';";
}
