DELETE FROM dbo.TransactionLink
WHERE SourceTransactionId = @ImportedTransactionId
   OR LinkedTransactionId = @ImportedTransactionId;
