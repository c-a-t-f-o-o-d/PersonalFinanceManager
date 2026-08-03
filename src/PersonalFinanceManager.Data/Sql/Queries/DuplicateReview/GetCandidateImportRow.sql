SELECT
    r.ImportRowId,
    r.ImportBatchId,
    r.[Status],
    b.AccountId,
    r.TransactionDate,
    r.PostingDate,
    r.TransactionTime,
    r.Amount,
    r.[Type],
    r.[Description],
    r.ExternalTransactionId,
    r.FundingSource,
    r.SourceCategory,
    r.Memo,
    r.AppliedRuleId,
    r.Category,
    r.SpendingClass,
    r.Merchant,
    r.IsIncome,
    r.IsTransfer,
    r.IncomeOwner
FROM dbo.ImportRow AS r
INNER JOIN dbo.ImportBatch AS b ON b.ImportBatchId = r.ImportBatchId
WHERE r.ImportRowId = @ImportRowId;
