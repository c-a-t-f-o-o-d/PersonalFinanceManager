SELECT TOP (1) ImportedTransactionId
FROM dbo.ImportedTransaction
WHERE AccountId = @AccountId
  AND ExternalTransactionId = @ExternalTransactionId
ORDER BY ImportedTransactionId DESC;
