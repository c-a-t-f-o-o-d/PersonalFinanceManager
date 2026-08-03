UPDATE bank
SET IsTransfer = 1,
    Category = N'Transfer',
    SpendingClass = N'PayPal Funding',
    Notes = CASE
        WHEN bank.Notes IS NULL OR LTRIM(RTRIM(bank.Notes)) = N'' THEN N'Auto-normalized as likely PayPal funding transfer.'
        WHEN bank.Notes NOT LIKE N'%PayPal funding%' THEN bank.Notes + N' Auto-normalized as likely PayPal funding transfer.'
        ELSE bank.Notes
    END,
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.ImportedTransaction AS bank
INNER JOIN dbo.Account AS bankAccount ON bankAccount.AccountId = bank.AccountId
WHERE bank.Amount < 0
  AND UPPER(bankAccount.AccountType) NOT LIKE N'%PAYPAL%'
  AND
  (
      UPPER(ISNULL(bank.Category, N'')) = N'PAYPAL'
      OR UPPER(ISNULL(bank.SpendingClass, N'')) IN (N'PAYPAL', N'PAYPAL FUNDING')
      OR UPPER(bank.[Description]) LIKE N'%PAYPAL%'
  )
  AND EXISTS
  (
      SELECT 1
      FROM dbo.ImportedTransaction AS payPal
      INNER JOIN dbo.Account AS payPalAccount ON payPalAccount.AccountId = payPal.AccountId
      WHERE payPal.ImportedTransactionId <> bank.ImportedTransactionId
        AND payPal.Amount = bank.Amount
        AND payPal.TransactionDate BETWEEN DATEADD(day, -5, bank.TransactionDate) AND DATEADD(day, 7, bank.TransactionDate)
        AND
        (
            UPPER(payPalAccount.AccountType) LIKE N'%PAYPAL%'
            OR payPal.FundingSource IS NOT NULL
            OR UPPER(ISNULL(payPal.[Type], N'')) LIKE N'%PAYPAL%'
        )
  )
  AND
  (
      bank.IsTransfer = 0
      OR ISNULL(bank.Category, N'') <> N'Transfer'
      OR ISNULL(bank.SpendingClass, N'') <> N'PayPal Funding'
  );
