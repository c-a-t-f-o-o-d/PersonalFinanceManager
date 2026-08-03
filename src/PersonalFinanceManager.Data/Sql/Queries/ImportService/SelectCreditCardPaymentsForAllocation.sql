SELECT p.ImportedTransactionId, p.AccountId, p.TransactionDate, p.Amount
FROM dbo.ImportedTransaction AS p
INNER JOIN dbo.Account AS a ON a.AccountId = p.AccountId
WHERE p.Amount > 0
  AND UPPER(LTRIM(RTRIM(p.[Type]))) = N'PAYMENT'
  AND UPPER(a.AccountType) LIKE N'%CREDIT%'
ORDER BY p.TransactionDate, p.ImportedTransactionId;
