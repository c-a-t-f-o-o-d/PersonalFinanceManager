SELECT ImportedTransactionId, AccountId, ImportBatchId, TransactionDate, TransactionTime,
       Amount, [Type], [Description], AppliedRuleId, Category, SpendingClass, Merchant,
       IsIncome, IsTransfer, IsManual, Notes, CreatedAtUtc, UpdatedAtUtc
FROM dbo.ImportedTransaction
ORDER BY TransactionDate, ImportedTransactionId;
