SELECT TOP (/*TOP_ROWS*/)
    link.TransactionLinkId,
    link.SourceTransactionId,
    source.AccountId AS SourceAccountId,
    sourceAccount.AccountNickname AS SourceAccountNickname,
    source.TransactionDate AS SourceDate,
    source.Amount AS SourceAmount,
    source.[Type] AS SourceType,
    source.[Description] AS SourceDescription,
    source.Category AS SourceCategory,
    source.SpendingClass AS SourceCategoryType,
    source.FundingSource AS SourceFundingSource,
    source.ExternalTransactionId AS SourceExternalTransactionId,
    link.LinkedTransactionId,
    linked.AccountId AS LinkedAccountId,
    linkedAccount.AccountNickname AS LinkedAccountNickname,
    linked.TransactionDate AS LinkedDate,
    linked.Amount AS LinkedAmount,
    linked.[Type] AS LinkedType,
    linked.[Description] AS LinkedDescription,
    linked.Category AS LinkedCategory,
    linked.SpendingClass AS LinkedCategoryType,
    linked.FundingSource AS LinkedFundingSource,
    linked.ExternalTransactionId AS LinkedExternalTransactionId,
    link.LinkType,
    link.AllocationAmount,
    link.ConfidenceScore,
    link.Notes,
    link.CreatedAtUtc,
    link.UpdatedAtUtc
FROM dbo.TransactionLink AS link
INNER JOIN dbo.ImportedTransaction AS source ON source.ImportedTransactionId = link.SourceTransactionId
INNER JOIN dbo.ImportedTransaction AS linked ON linked.ImportedTransactionId = link.LinkedTransactionId
INNER JOIN dbo.Account AS sourceAccount ON sourceAccount.AccountId = source.AccountId
INNER JOIN dbo.Account AS linkedAccount ON linkedAccount.AccountId = linked.AccountId
/*WHERE_CLAUSE*/
ORDER BY link.CreatedAtUtc DESC, link.TransactionLinkId DESC;
