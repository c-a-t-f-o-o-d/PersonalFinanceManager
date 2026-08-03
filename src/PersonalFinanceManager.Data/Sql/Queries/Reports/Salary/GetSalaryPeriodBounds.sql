SELECT MIN(t.TransactionDate) AS FirstDate,
       MAX(t.TransactionDate) AS LastDate
FROM dbo.ImportedTransaction AS t
WHERE /*EXPENSE_WHERE*/
  AND /*SALARY_SCOPE_WHERE*/
  AND (@AccountId IS NULL OR t.AccountId = @AccountId)
  AND (@FromDate IS NULL OR t.TransactionDate >= @FromDate)
  AND (@ToDate IS NULL OR t.TransactionDate <= @ToDate);
