SELECT t.ImportedTransactionId, t.TransactionDate, t.PostingDate, t.TransactionTime, t.Amount, t.[Type], t.[Description],
       t.ExternalTransactionId, t.FundingSource, t.SourceCategory, t.Merchant, t.Memo, a.AccountType
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE t.ClassificationIsManual = 0
ORDER BY t.ImportedTransactionId;
