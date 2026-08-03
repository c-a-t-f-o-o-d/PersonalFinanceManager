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
WHERE t.IsIncome = 1
  AND t.IsTransfer = 0
  AND t.Amount > 0
  AND (t.IncomeOwner IS NULL OR LTRIM(RTRIM(t.IncomeOwner)) = N'' OR t.IncomeOwner = N'Unassigned')
  AND (a.AccountOwner IS NULL OR a.AccountOwner NOT IN (N'Me', N'Spouse', N'Joint', N'Household'))
ORDER BY t.TransactionDate DESC, t.ImportedTransactionId DESC;
