SELECT ImportedTransactionId, AccountId, ImportBatchId, TransactionDate, PostingDate, TransactionTime,
       Amount, [Type], [Description], ExternalTransactionId, FundingSource, SourceCategory, Memo,
       AppliedRuleId, Category, SpendingClass, Merchant,
       IsIncome, IsTransfer, IsManual, ClassificationIsManual, LegacyLinkTreatment, IncomeOwner, Notes, CreatedAtUtc, UpdatedAtUtc
FROM dbo.ImportedTransaction
ORDER BY TransactionDate, ImportedTransactionId;
