SELECT p.ImportedTransactionId, p.AccountId, p.TransactionDate, p.Amount, p.[Description]
FROM dbo.ImportedTransaction AS p
INNER JOIN dbo.Account AS a ON a.AccountId = p.AccountId
WHERE p.Amount > 0
  AND UPPER(LTRIM(RTRIM(p.[Type]))) = N'PAYMENT'
  AND UPPER(a.AccountType) LIKE N'%CREDIT%'
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE l.SourceTransactionId = p.ImportedTransactionId
        AND l.LinkType = N'CreditCardPaymentFunding'
  );
