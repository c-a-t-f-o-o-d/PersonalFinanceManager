SELECT TOP (1) ImportedTransactionId
FROM dbo.ImportedTransaction AS existing
WHERE existing.AccountId <> @AccountId
  AND (existing.ImportBatchId IS NULL OR existing.ImportBatchId <> @ImportBatchId)
  AND existing.TransactionDate = @TransactionDate
  AND existing.Amount = @Amount
  AND UPPER(LTRIM(RTRIM(existing.[Type]))) = UPPER(LTRIM(RTRIM(@Type)))
  AND UPPER(LTRIM(RTRIM(existing.[Description]))) = UPPER(LTRIM(RTRIM(@Description)))
  AND
  (
      NULLIF(LTRIM(RTRIM(existing.Memo)), N'') IS NULL
      OR NULLIF(LTRIM(RTRIM(@Memo)), N'') IS NULL
      OR UPPER(LTRIM(RTRIM(existing.Memo))) = UPPER(LTRIM(RTRIM(@Memo)))
  )
ORDER BY existing.ImportedTransactionId DESC;
