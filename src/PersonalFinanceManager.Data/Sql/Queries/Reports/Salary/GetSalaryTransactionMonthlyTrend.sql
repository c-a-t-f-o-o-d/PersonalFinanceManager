SELECT
    DATEFROMPARTS(YEAR(t.TransactionDate), MONTH(t.TransactionDate), 1) AS MonthStart,
    SUM(CASE WHEN t.Amount < 0 THEN -t.Amount ELSE t.Amount END) AS TotalAmount,
    COUNT(*) AS TransactionCount
FROM dbo.ImportedTransaction AS t
WHERE /*EXPENSE_WHERE*/
  AND (@AccountId IS NULL OR t.AccountId = @AccountId)
  AND (@FromDate IS NULL OR t.TransactionDate >= @FromDate)
  AND (@ToDate IS NULL OR t.TransactionDate <= @ToDate)
  /*SALARY_CATEGORY_PREDICATE*/
GROUP BY YEAR(t.TransactionDate), MONTH(t.TransactionDate)
ORDER BY YEAR(t.TransactionDate), MONTH(t.TransactionDate);
