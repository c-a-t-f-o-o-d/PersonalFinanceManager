WITH pairs AS
(
    SELECT
        t1.ImportedTransactionId AS LeftId,
        t2.ImportedTransactionId AS RightId,
        CONCAT(CONVERT(nvarchar(30), t1.TransactionDate, 23), N'|', CONVERT(nvarchar(50), t1.Amount), N'|', t1.[Type], N'|', t1.[Description]) AS GroupKey
    FROM dbo.ImportedTransaction AS t1
    INNER JOIN dbo.ImportedTransaction AS t2 ON t1.ImportedTransactionId < t2.ImportedTransactionId
    WHERE t1.AccountId <> t2.AccountId
      AND t1.TransactionDate = t2.TransactionDate
      AND t1.Amount = t2.Amount
      AND UPPER(LTRIM(RTRIM(t1.[Type]))) = UPPER(LTRIM(RTRIM(t2.[Type])))
      AND UPPER(LTRIM(RTRIM(t1.[Description]))) = UPPER(LTRIM(RTRIM(t2.[Description])))
      AND
      (
          NULLIF(LTRIM(RTRIM(t1.Memo)), N'') IS NULL
          OR NULLIF(LTRIM(RTRIM(t2.Memo)), N'') IS NULL
          OR UPPER(LTRIM(RTRIM(t1.Memo))) = UPPER(LTRIM(RTRIM(t2.Memo)))
      )
), pairRows AS
(
    SELECT GroupKey, LeftId AS TransactionId, CAST(N'Left' AS nvarchar(50)) AS Source FROM pairs
    UNION ALL
    SELECT GroupKey, RightId AS TransactionId, CAST(N'Right' AS nvarchar(50)) AS Source FROM pairs
)
SELECT
    p.GroupKey,
    p.Source,
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
    CAST(N'Same date, amount, type, and description under different accounts. Non-empty memo differences are not counted.' AS nvarchar(1000)) AS Details
FROM pairRows AS p
INNER JOIN dbo.ImportedTransaction AS t ON t.ImportedTransactionId = p.TransactionId
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
ORDER BY p.GroupKey, p.Source, t.ImportedTransactionId;
