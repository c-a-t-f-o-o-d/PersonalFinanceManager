SELECT
    t.ImportedTransactionId,
    t.AccountId,
    a.AccountNickname,
    t.ImportBatchId,
    t.TransactionDate,
    t.TransactionTime,
    t.Amount,
    t.[Type],
    t.[Description],
    t.AppliedRuleId,
    t.Category,
    t.SpendingClass,
    t.Merchant,
    t.IsIncome,
    t.IsTransfer,
    t.IsManual,
    t.ClassificationIsManual,
    t.LegacyLinkTreatment,
    t.IncomeOwner,
    t.Notes,
    t.CreatedAtUtc,
    t.UpdatedAtUtc
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE t.ImportedTransactionId = @ImportedTransactionId;
