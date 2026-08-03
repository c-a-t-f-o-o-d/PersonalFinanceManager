SELECT TOP (/*TOP_ROWS*/)
    t.ImportedTransactionId,
    t.AccountId,
    a.AccountNickname,
    t.TransactionDate,
    t.Amount,
    t.[Type],
    t.[Description],
    t.Category,
    t.SpendingClass,
    t.FundingSource,
    t.ExternalTransactionId
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
/*WHERE_CLAUSE*/
ORDER BY t.TransactionDate DESC, t.ImportedTransactionId DESC;
