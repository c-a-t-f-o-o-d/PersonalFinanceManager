SELECT TOP (1)
    ImportedTransactionId,
    CASE
        WHEN UPPER(LTRIM(RTRIM([Type]))) = UPPER(LTRIM(RTRIM(@Type)))
         AND UPPER(LTRIM(RTRIM([Description]))) = UPPER(LTRIM(RTRIM(@Description)))
        THEN N'Same account/date/amount/type/description, but metadata differs.'
        ELSE N'Same account/date/amount, but type or description differs.'
    END AS DuplicateReason
FROM dbo.ImportedTransaction
WHERE AccountId = @AccountId
  AND (ImportBatchId IS NULL OR ImportBatchId <> @ImportBatchId)
  AND TransactionDate = @TransactionDate
  AND Amount = @Amount
  AND NOT (
      NULLIF(@ExternalTransactionId, N'') IS NOT NULL
      AND NULLIF(ExternalTransactionId, N'') IS NOT NULL
      AND ExternalTransactionId <> @ExternalTransactionId
      AND EXISTS (
          SELECT 1 FROM dbo.ImportBatch AS oldBatch
          INNER JOIN dbo.ImportBatch AS currentBatch ON currentBatch.ImportBatchId = @ImportBatchId
          WHERE oldBatch.ImportBatchId = dbo.ImportedTransaction.ImportBatchId
            AND oldBatch.ImportProfileCode = currentBatch.ImportProfileCode
            AND currentBatch.ImportProfileCode <> N'QIF'
      )
  )
ORDER BY
    CASE
        WHEN UPPER(LTRIM(RTRIM([Type]))) = UPPER(LTRIM(RTRIM(@Type)))
         AND UPPER(LTRIM(RTRIM([Description]))) = UPPER(LTRIM(RTRIM(@Description)))
        THEN 0
        ELSE 1
    END,
    ImportedTransactionId DESC;
