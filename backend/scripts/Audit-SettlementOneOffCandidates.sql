-- Read-only review. Multiple one-off invoices can be legitimate; inspect each row before any correction.
-- Historic invoices have no CreationSource, so this query marks candidates rather than proven duplicates.
WITH CandidateContracts AS (
    SELECT i.ContractId, COUNT(*) AS OneOffCount
    FROM dbo.Invoices AS i
    INNER JOIN dbo.Contracts AS c ON c.Id = i.ContractId
    WHERE c.Status = N'PendingSettlement'
      AND i.InvoiceType IN (N'Damage', N'Other')
      AND i.Status <> N'Cancelled'
    GROUP BY i.ContractId
    HAVING COUNT(*) > 1
)
SELECT c.ContractCode, c.HandoverOn, candidates.OneOffCount,
       i.Id AS InvoiceId, i.InvoiceCode, i.InvoiceType, i.Status,
       i.CreationSource, i.SettlementOperationKey,
       i.IssueDate, i.DueDate, i.CreatedAt, i.Note,
       i.TotalAmount AS BaseTotal, balance.EffectiveAmount,
       balance.TotalPaid, balance.TotalDepositApplied,
       balance.TotalRefunded, balance.RemainingAmount,
       items.Descriptions
FROM CandidateContracts AS candidates
INNER JOIN dbo.Contracts AS c ON c.Id = candidates.ContractId
INNER JOIN dbo.Invoices AS i ON i.ContractId = candidates.ContractId
    AND i.InvoiceType IN (N'Damage', N'Other') AND i.Status <> N'Cancelled'
LEFT JOIN dbo.InvoiceBalances AS balance ON balance.InvoiceId = i.Id
OUTER APPLY (
    SELECT STRING_AGG(CONVERT(nvarchar(max), item.Description), N'; ') AS Descriptions
    FROM dbo.InvoiceItems AS item
    WHERE item.InvoiceId = i.Id
) AS items
ORDER BY c.ContractCode, i.CreatedAt, i.Id;
