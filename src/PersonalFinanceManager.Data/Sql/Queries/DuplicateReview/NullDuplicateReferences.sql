UPDATE dbo.ImportRow
SET DuplicateMatchTransactionId = NULL
WHERE DuplicateMatchTransactionId = @ImportedTransactionId;
