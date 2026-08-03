SELECT ImportedTransactionId, TransactionDate, PostingDate, TransactionTime, Amount, [Type], [Description], ExternalTransactionId, FundingSource, SourceCategory, Merchant, Memo
FROM dbo.ImportedTransaction
ORDER BY ImportedTransactionId;
