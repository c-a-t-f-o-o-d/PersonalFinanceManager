SELECT TOP (1) t.ImportedTransactionId
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE t.ImportedTransactionId <> @SourceTransactionId
  AND t.Amount = -@SourceAmount
  AND t.TransactionDate BETWEEN @FromDate AND @ToDate
  AND UPPER(a.AccountType) NOT LIKE N'%CREDIT%'
  AND
  (
      t.IsTransfer = 1
      OR UPPER(ISNULL(t.Category, N'')) = N'TRANSFER'
      OR UPPER(ISNULL(t.SpendingClass, N'')) LIKE N'%TRANSFER%'
      OR UPPER(t.[Description]) LIKE N'EB TO %'
      OR UPPER(t.[Description]) LIKE N'EB FROM %'
  )
  AND UPPER(ISNULL(t.SpendingClass, N'')) NOT IN (N'PAYPAL FUNDING', N'CREDIT CARD PAYMENT')
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE l.SourceTransactionId = t.ImportedTransactionId OR l.LinkedTransactionId = t.ImportedTransactionId
  )
ORDER BY ABS(DATEDIFF(day, t.TransactionDate, @SourceDate)), t.ImportedTransactionId;
