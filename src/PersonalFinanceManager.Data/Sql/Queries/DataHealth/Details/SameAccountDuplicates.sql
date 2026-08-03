WITH duplicateGroups AS
(
    SELECT AccountId, TransactionDate, Amount, [Type], [Description], ISNULL(NULLIF(UPPER(LTRIM(RTRIM(Memo))), N''), N'') AS NormalizedMemo
    FROM dbo.ImportedTransaction
    GROUP BY AccountId, TransactionDate, Amount, [Type], [Description], ISNULL(NULLIF(UPPER(LTRIM(RTRIM(Memo))), N''), N'')
    HAVING COUNT_BIG(*) > 1
)
SELECT
    CONCAT(t.AccountId, N'|', CONVERT(nvarchar(30), t.TransactionDate, 23), N'|', CONVERT(nvarchar(50), t.Amount), N'|', t.[Type], N'|', t.[Description], N'|', ISNULL(NULLIF(UPPER(LTRIM(RTRIM(t.Memo))), N''), N'')) AS GroupKey,
    CAST(N'Same-account duplicate' AS nvarchar(50)) AS Source,
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
    CAST(N'Same account, date, amount, type, description, and memo.' AS nvarchar(1000)) AS Details
FROM dbo.ImportedTransaction AS t
INNER JOIN duplicateGroups AS g ON g.AccountId = t.AccountId
    AND g.TransactionDate = t.TransactionDate
    AND g.Amount = t.Amount
    AND g.[Type] = t.[Type]
    AND g.[Description] = t.[Description]
    AND g.NormalizedMemo = ISNULL(NULLIF(UPPER(LTRIM(RTRIM(t.Memo))), N''), N'')
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
ORDER BY GroupKey, t.ImportedTransactionId;
