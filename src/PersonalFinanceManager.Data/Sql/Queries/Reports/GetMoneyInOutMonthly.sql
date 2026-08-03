SELECT
    DATEFROMPARTS(YEAR(t.TransactionDate), MONTH(t.TransactionDate), 1) AS MonthStart,
    ISNULL(SUM(CASE WHEN t.Amount > 0 THEN t.Amount ELSE 0 END), 0) AS MoneyIn,
    ISNULL(SUM(CASE WHEN t.Amount < 0 THEN -t.Amount ELSE 0 END), 0) AS MoneyOut,
    COUNT(*) AS TransactionCount
FROM dbo.ImportedTransaction AS t
WHERE t.IsTransfer = 0
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
  AND ISNULL(NULLIF(LTRIM(RTRIM(t.Category)), N''), N'') <> N'Ignore'
  AND ISNULL(NULLIF(LTRIM(RTRIM(t.SpendingClass)), N''), N'') <> N'Ignore'
  AND (@AccountId IS NULL OR t.AccountId = @AccountId)
  AND (@FromDate IS NULL OR t.TransactionDate >= @FromDate)
  AND (@ToDate IS NULL OR t.TransactionDate <= @ToDate)
GROUP BY YEAR(t.TransactionDate), MONTH(t.TransactionDate)
ORDER BY YEAR(t.TransactionDate), MONTH(t.TransactionDate);
