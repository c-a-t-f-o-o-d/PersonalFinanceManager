SELECT CAST(link.TransactionLinkId AS nvarchar(30)) AS GroupKey,
       CAST(N'Transaction link' AS nvarchar(50)) AS Source, t.ImportedTransactionId AS TransactionId,
       t.AccountId, a.AccountNickname AS Account, a.AccountType, a.AccountOwner,
       t.TransactionDate AS [Date], t.Amount, t.[Type], t.[Description], t.Category,
       t.SpendingClass AS CategoryType, t.IncomeOwner, t.Memo,
       CAST(N'Link ' + CONVERT(nvarchar(30), link.TransactionLinkId) + N' to transaction '
            + CONVERT(nvarchar(30), link.LinkedTransactionId) + N': ' + link.Notes AS nvarchar(2500)) AS Details
FROM dbo.TransactionLink AS link
JOIN dbo.ImportedTransaction AS t ON t.ImportedTransactionId = link.SourceTransactionId
JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE link.LinkType = N'ManualReview' AND link.Notes LIKE N'Review required after upgrade: %'
ORDER BY t.TransactionDate DESC, link.TransactionLinkId;
