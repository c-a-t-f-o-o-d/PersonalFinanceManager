SELECT
    CAST(b.RecurringBillId AS nvarchar(30)) AS GroupKey,
    CAST(N'Bill' AS nvarchar(50)) AS Source,
    CAST(NULL AS bigint) AS TransactionId,
    b.AccountId,
    a.AccountNickname AS Account,
    a.AccountType,
    a.AccountOwner,
    b.NextDueDate AS [Date],
    b.TypicalAmount AS Amount,
    b.Frequency AS [Type],
    b.BillName AS [Description],
    b.Category,
    b.SpendingClass AS CategoryType,
    CAST(NULL AS nvarchar(50)) AS IncomeOwner,
    b.Notes AS Memo,
    CAST(N'Active required bill has no positive TypicalAmount.' AS nvarchar(1000)) AS Details
FROM dbo.RecurringBill AS b
LEFT JOIN dbo.Account AS a ON a.AccountId = b.AccountId
WHERE b.IsActive = 1
  AND b.IsRequired = 1
  AND (b.TypicalAmount IS NULL OR b.TypicalAmount <= 0)
ORDER BY b.BillName;
