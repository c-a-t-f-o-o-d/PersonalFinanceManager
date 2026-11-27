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
            AND (
                currentBatch.ImportProfileCode IN (N'OFX_QFX', N'PAYPAL_ACTIVITY_CSV', N'PAYPAL_BALANCE_STATEMENT_CSV')
                OR (
                    currentBatch.ImportProfileCode IN (N'ACCOUNT_ACTIVITY_CSV', N'CREDIT_ACCOUNT_CSV', N'PAYPAL_SMART_CONNECT_CSV')
                    AND LEFT(@ExternalTransactionId, LEN(currentBatch.ImportProfileCode) + 1) <> currentBatch.ImportProfileCode + N':'
                    AND LEFT(ExternalTransactionId, LEN(currentBatch.ImportProfileCode) + 1) <> currentBatch.ImportProfileCode + N':'
                )
            )
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
