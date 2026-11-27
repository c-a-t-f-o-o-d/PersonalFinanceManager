SELECT TOP (1) importRow.ImportRowId
FROM dbo.ImportRow AS importRow
JOIN dbo.ImportBatch AS batch ON batch.ImportBatchId = importRow.ImportBatchId
WHERE batch.AccountId = @AccountId AND importRow.[Status] = N'Deleted'
  AND ((NULLIF(@ExternalTransactionId, N'') IS NOT NULL AND importRow.ExternalTransactionId = @ExternalTransactionId)
       OR (importRow.TransactionDate = @TransactionDate AND importRow.Amount = @Amount
           AND UPPER(LTRIM(RTRIM(importRow.[Type]))) = UPPER(LTRIM(RTRIM(@Type)))
           AND UPPER(LTRIM(RTRIM(importRow.[Description]))) = UPPER(LTRIM(RTRIM(@Description)))))
ORDER BY importRow.ImportRowId DESC;
