SELECT TOP (1) t.ImportedTransactionId
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE t.ImportedTransactionId <> @CardPaymentTransactionId
  AND t.AccountId <> @CardAccountId
  AND t.Amount = -@PaymentAmount
  AND t.TransactionDate BETWEEN @FromDate AND @ToDate
  AND UPPER(a.AccountType) NOT LIKE N'%CREDIT%'
  AND
  (
      UPPER(t.[Description]) LIKE N'%CHASE%'
      OR UPPER(t.[Description]) LIKE N'%CREDIT%'
      OR UPPER(t.[Description]) LIKE N'% CRD %'
      OR UPPER(t.[Description]) LIKE N'%CARD%'
      OR UPPER(t.[Description]) LIKE N'%EPAY%'
      OR UPPER(t.[Description]) LIKE N'%PAYMENT%'
      OR UPPER(t.Category) LIKE N'%CREDIT%'
      OR UPPER(t.SpendingClass) LIKE N'%PAYMENT%'
  )
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE l.LinkType = N'CreditCardPaymentFunding'
        AND (l.LinkedTransactionId = t.ImportedTransactionId OR l.SourceTransactionId = t.ImportedTransactionId)
  )
ORDER BY ABS(DATEDIFF(day, t.TransactionDate, @PaymentDate)), t.ImportedTransactionId;
