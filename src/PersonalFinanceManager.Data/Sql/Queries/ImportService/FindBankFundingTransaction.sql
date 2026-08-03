SELECT TOP (1) t.ImportedTransactionId
FROM dbo.ImportedTransaction AS t
WHERE t.ImportedTransactionId <> @PayPalTransactionId
  AND t.Amount = @Amount
  AND t.TransactionDate BETWEEN @FromDate AND @ToDate
  AND
  (
      UPPER(t.[Description]) LIKE N'%PAYPAL%'
      OR UPPER(t.Category) = N'PAYPAL'
      OR UPPER(t.SpendingClass) = N'PAYPAL'
      OR UPPER(t.SpendingClass) = N'PAYPAL FUNDING'
      OR UPPER(t.[Type]) LIKE N'%PAYPAL%'
  )
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE l.LinkType = N'PayPalFunding'
        AND (l.LinkedTransactionId = t.ImportedTransactionId OR l.SourceTransactionId = t.ImportedTransactionId)
  )
ORDER BY ABS(DATEDIFF(day, t.TransactionDate, @PayPalDate)), t.ImportedTransactionId;
