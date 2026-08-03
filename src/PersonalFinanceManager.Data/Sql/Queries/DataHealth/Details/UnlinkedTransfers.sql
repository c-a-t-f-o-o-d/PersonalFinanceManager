SELECT
    CAST(t.ImportedTransactionId AS nvarchar(30)) AS GroupKey,
    CAST(N'Transaction' AS nvarchar(50)) AS Source,
    t.ImportedTransactionId AS TransactionId,
    t.AccountId,
    a.AccountNickname AS Account,
    a.AccountType,
    a.AccountOwner,
    t.TransactionDate AS [Date],
    t.Amount,
    t.[Type],
    t.[Description],
    t.Category,
    t.SpendingClass AS CategoryType,
    t.IncomeOwner,
    t.Memo,
    CAST(NULL AS nvarchar(1000)) AS Details
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE t.IsTransfer = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE l.SourceTransactionId = t.ImportedTransactionId
         OR l.LinkedTransactionId = t.ImportedTransactionId
  )
ORDER BY t.TransactionDate DESC, t.ImportedTransactionId DESC;
