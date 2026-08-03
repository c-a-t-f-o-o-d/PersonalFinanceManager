/*ALIAS*/.IsIncome = 0
  AND /*ALIAS*/.IsTransfer = 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE
      (
          l.LinkType = N'PayPalFunding'
          AND l.LinkedTransactionId = /*ALIAS*/.ImportedTransactionId
      )
      OR
      (
          l.LinkType = N'CreditCardPaymentFunding'
          AND (l.LinkedTransactionId = /*ALIAS*/.ImportedTransactionId OR l.SourceTransactionId = /*ALIAS*/.ImportedTransactionId)
      )
  )
  AND /*ALIAS*/.Amount < 0
  AND ISNULL(NULLIF(LTRIM(RTRIM(/*ALIAS*/.Category)), N''), N'') <> N'Ignore'
  AND ISNULL(NULLIF(LTRIM(RTRIM(/*ALIAS*/.SpendingClass)), N''), N'') <> N'Ignore'
  AND NOT
  (
      (UPPER(ISNULL(/*ALIAS*/.Category, N'')) = N'PAYPAL' OR UPPER(ISNULL(/*ALIAS*/.SpendingClass, N'')) IN (N'PAYPAL', N'PAYPAL FUNDING'))
      AND (UPPER(/*ALIAS*/.[Description]) LIKE N'%PAYPAL%' OR UPPER(ISNULL(/*ALIAS*/.FundingSource, N'')) LIKE N'%PAYPAL%' OR UPPER(ISNULL(/*ALIAS*/.FundingSource, N'')) LIKE N'%ALLY%')
  )
  AND NOT
  (
      (UPPER(ISNULL(/*ALIAS*/.Category, N'')) IN (N'DEBT (CREDIT CARD)', N'CREDIT CARD PAYMENT') OR UPPER(ISNULL(/*ALIAS*/.SpendingClass, N'')) IN (N'PAYMENT', N'CREDIT CARD PAYMENT'))
      AND
      (
          UPPER(/*ALIAS*/.[Description]) LIKE N'%APPLECARD%'
          OR UPPER(/*ALIAS*/.[Description]) LIKE N'%GSBANK%'
          OR UPPER(/*ALIAS*/.[Description]) LIKE N'%CHASE CREDIT%'
          OR UPPER(/*ALIAS*/.[Description]) LIKE N'%CREDIT CRD%'
          OR UPPER(/*ALIAS*/.[Description]) LIKE N'%CARD PAYMENT%'
          OR UPPER(/*ALIAS*/.[Description]) LIKE N'%CRD EPAY%'
      )
  )
