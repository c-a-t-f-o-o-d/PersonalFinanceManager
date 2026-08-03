SELECT
    ISNULL(NULLIF(LTRIM(RTRIM(t.Category)), N''), N'(Uncategorized)') AS Category,
    ISNULL(NULLIF(LTRIM(RTRIM(t.SpendingClass)), N''), N'(Uncategorized)') AS SpendingClass,
    SUM(CASE WHEN t.Amount < 0 THEN -t.Amount ELSE t.Amount END) AS TotalAmount,
    COUNT(*) AS TransactionCount,
    COUNT(DISTINCT CONVERT(char(7), t.TransactionDate, 120)) AS ActiveMonths
FROM dbo.ImportedTransaction AS t
WHERE /*EXPENSE_WHERE*/
  AND (@AccountId IS NULL OR t.AccountId = @AccountId)
  AND (@FromDate IS NULL OR t.TransactionDate >= @FromDate)
  AND (@ToDate IS NULL OR t.TransactionDate <= @ToDate)
  /*SALARY_CATEGORY_PREDICATE*/
GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(t.Category)), N''), N'(Uncategorized)'), ISNULL(NULLIF(LTRIM(RTRIM(t.SpendingClass)), N''), N'(Uncategorized)')
ORDER BY TotalAmount DESC;
