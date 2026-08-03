SELECT
    ISNULL(SUM(CASE WHEN t.Amount < 0 THEN -t.Amount ELSE t.Amount END), 0) AS TotalExpenses,
    COUNT(*) AS TransactionCount,
    MIN(t.TransactionDate) AS FirstTransactionDate,
    MAX(t.TransactionDate) AS LastTransactionDate
FROM dbo.ImportedTransaction AS t
WHERE t.IsIncome = 0
  AND t.IsTransfer = 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE
      (
          l.LinkType = N'PayPalFunding'
          AND l.LinkedTransactionId = t.ImportedTransactionId
      )
      OR
      (
          l.LinkType = N'CreditCardPaymentFunding'
          AND (l.LinkedTransactionId = t.ImportedTransactionId OR l.SourceTransactionId = t.ImportedTransactionId)
      )
  )
  AND NOT
  (
      t.Amount < 0
      AND
      (
          (
              (UPPER(ISNULL(t.Category, N'')) = N'PAYPAL' OR UPPER(ISNULL(t.SpendingClass, N'')) IN (N'PAYPAL', N'PAYPAL FUNDING'))
              AND (UPPER(t.[Description]) LIKE N'%PAYPAL%' OR UPPER(ISNULL(t.FundingSource, N'')) LIKE N'%PAYPAL%' OR UPPER(ISNULL(t.FundingSource, N'')) LIKE N'%ALLY%')
          )
          OR
          (
              (UPPER(ISNULL(t.Category, N'')) IN (N'DEBT (CREDIT CARD)', N'CREDIT CARD PAYMENT') OR UPPER(ISNULL(t.SpendingClass, N'')) IN (N'PAYMENT', N'CREDIT CARD PAYMENT'))
              AND
              (
                  UPPER(t.[Description]) LIKE N'%APPLECARD%'
                  OR UPPER(t.[Description]) LIKE N'%GSBANK%'
                  OR UPPER(t.[Description]) LIKE N'%CHASE CREDIT%'
                  OR UPPER(t.[Description]) LIKE N'%CREDIT CRD%'
                  OR UPPER(t.[Description]) LIKE N'%CARD PAYMENT%'
                  OR UPPER(t.[Description]) LIKE N'%CRD EPAY%'
              )
          )
      )
  )
  AND t.Amount < 0
  AND (@AccountId IS NULL OR t.AccountId = @AccountId)
  AND (@FromDate IS NULL OR t.TransactionDate >= @FromDate)
  AND (@ToDate IS NULL OR t.TransactionDate <= @ToDate);
