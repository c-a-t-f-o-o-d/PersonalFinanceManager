SELECT TOP (1) ImportedTransactionId
FROM dbo.ImportedTransaction
WHERE AccountId = @AccountId
  AND (ImportBatchId IS NULL OR ImportBatchId <> @ImportBatchId)
  AND ExternalTransactionId = @ExternalTransactionId
ORDER BY ImportedTransactionId DESC;
