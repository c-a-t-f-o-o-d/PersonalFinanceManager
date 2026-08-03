UPDATE dbo.ImportedTransaction
SET Category = @Category,
    SpendingClass = @SpendingClass,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE ImportedTransactionId IN (/*ID_LIST*/);
