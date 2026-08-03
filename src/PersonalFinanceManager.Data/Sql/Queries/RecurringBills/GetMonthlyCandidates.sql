WITH BaseRows AS
(
    SELECT
        t.AccountId,
        a.AccountNickname,
        LTRIM(RTRIM(t.[Description])) AS SourceDescription,
        ISNULL(NULLIF(LTRIM(RTRIM(t.Merchant)), N''), LTRIM(RTRIM(t.[Description]))) AS Payee,
        NULLIF(LTRIM(RTRIM(t.Category)), N'') AS Category,
        NULLIF(LTRIM(RTRIM(t.SpendingClass)), N'') AS SpendingClass,
        -t.Amount AS ExpenseAmount,
        t.TransactionDate,
        DATEFROMPARTS(YEAR(t.TransactionDate), MONTH(t.TransactionDate), 1) AS MonthStart
    FROM dbo.ImportedTransaction AS t
    INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
    WHERE t.IsIncome = 0
      AND t.IsTransfer = 0
      AND t.Amount < 0
      AND LTRIM(RTRIM(t.[Description])) <> N''
), GroupedRows AS
(
    SELECT
        AccountId,
        MAX(AccountNickname) AS AccountNickname,
        SourceDescription,
        MAX(Payee) AS Payee,
        MAX(Category) AS Category,
        MAX(SpendingClass) AS SpendingClass,
        AVG(ExpenseAmount) AS AverageAmount,
        MIN(ExpenseAmount) AS MinimumAmount,
        MAX(ExpenseAmount) AS MaximumAmount,
        MIN(TransactionDate) AS FirstSeenDate,
        MAX(TransactionDate) AS LastSeenDate,
        COUNT(*) AS TransactionCount,
        COUNT(DISTINCT MonthStart) AS MonthCount,
        DAY(MAX(TransactionDate)) AS SuggestedDueDay
    FROM BaseRows
    GROUP BY AccountId, SourceDescription
    HAVING COUNT(*) >= @MinimumTransactions
       AND COUNT(DISTINCT MonthStart) >= @MinimumMonths
)
SELECT
    g.*,
    CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.RecurringBill AS b
        WHERE b.AccountId = g.AccountId
          AND b.IsActive = 1
          AND ISNULL(b.SourceDescription, N'') = ISNULL(g.SourceDescription, N'')
    ) THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END AS AlreadyExists
FROM GroupedRows AS g
ORDER BY g.AverageAmount DESC, g.TransactionCount DESC, g.SourceDescription ASC;
