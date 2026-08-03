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
ORDER BY
    CASE
        WHEN UPPER(LTRIM(RTRIM([Type]))) = UPPER(LTRIM(RTRIM(@Type)))
         AND UPPER(LTRIM(RTRIM([Description]))) = UPPER(LTRIM(RTRIM(@Description)))
        THEN 0
        ELSE 1
    END,
    ImportedTransactionId DESC;
