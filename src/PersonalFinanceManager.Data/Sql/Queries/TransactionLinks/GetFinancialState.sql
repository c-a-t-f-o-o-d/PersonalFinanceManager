SELECT t.ImportedTransactionId, t.AccountId, a.AccountType, a.AccountNickname, a.Institution,
       a.AccountNumberLastFour, t.TransactionDate, t.Amount, t.[Type], t.[Description],
       t.ExternalTransactionId, t.FundingSource, t.Category, t.IsTransfer
FROM dbo.ImportedTransaction AS t WITH (UPDLOCK, HOLDLOCK)
JOIN dbo.Account AS a ON a.AccountId = t.AccountId;

SELECT TransactionLinkId, SourceTransactionId, LinkedTransactionId, LinkType, AllocationAmount
FROM dbo.TransactionLink WITH (UPDLOCK, HOLDLOCK);

SELECT SourceTransactionId, LinkedTransactionId, LinkType
FROM dbo.TransactionLinkRejection WITH (UPDLOCK, HOLDLOCK);
