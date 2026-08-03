SELECT TOP (1) ImportedTransactionId
FROM dbo.ImportedTransaction
WHERE AccountId = @AccountId
  AND (ImportBatchId IS NULL OR ImportBatchId <> @ImportBatchId)
  AND
  (
      RawRowHash = @RawRowHash
      OR
      (
          TransactionDate = @TransactionDate
          AND ((PostingDate IS NULL AND @PostingDate IS NULL) OR PostingDate = @PostingDate)
          AND ((TransactionTime IS NULL AND @TransactionTime IS NULL) OR TransactionTime = @TransactionTime)
          AND Amount = @Amount
          AND UPPER(LTRIM(RTRIM([Type]))) = UPPER(LTRIM(RTRIM(@Type)))
          AND UPPER(LTRIM(RTRIM([Description]))) = UPPER(LTRIM(RTRIM(@Description)))
          AND ISNULL(UPPER(LTRIM(RTRIM(ExternalTransactionId))), N'') = ISNULL(UPPER(LTRIM(RTRIM(@ExternalTransactionId))), N'')
          AND ISNULL(UPPER(LTRIM(RTRIM(FundingSource))), N'') = ISNULL(UPPER(LTRIM(RTRIM(@FundingSource))), N'')
          AND ISNULL(UPPER(LTRIM(RTRIM(SourceCategory))), N'') = ISNULL(UPPER(LTRIM(RTRIM(@SourceCategory))), N'')
          AND ISNULL(UPPER(LTRIM(RTRIM(Memo))), N'') = ISNULL(UPPER(LTRIM(RTRIM(@Memo))), N'')
      )
  )
ORDER BY ImportedTransactionId DESC;
