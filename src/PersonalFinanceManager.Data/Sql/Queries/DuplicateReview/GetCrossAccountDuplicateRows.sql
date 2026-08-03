SELECT TOP (/*MAX_ROWS*/)
    CAST(N'/*REVIEW_KIND*/' AS nvarchar(30)) AS ReviewKind,
    r.CrossAccountDuplicateReviewId,
    CAST(0 AS bigint) AS ImportRowId,
    CAST(0 AS bigint) AS ImportBatchId,
    CAST(0 AS int) AS RowNumber,
    CASE r.[Status]
        WHEN N'Pending' THEN N'/*STATUS_CROSS_ACCOUNT_REVIEW*/'
        WHEN N'Kept' THEN N'/*STATUS_CROSS_ACCOUNT_KEPT*/'
        WHEN N'CandidateDeleted' THEN N'/*STATUS_CROSS_ACCOUNT_CANDIDATE_DELETED*/'
        WHEN N'ExistingDeleted' THEN N'/*STATUS_CROSS_ACCOUNT_EXISTING_DELETED*/'
        ELSE r.[Status]
    END AS [Status],
    r.Confidence AS DuplicateConfidence,
    r.Reason AS DuplicateReason,
    CAST(N'Existing Transactions' AS nvarchar(100)) AS ImportProfileCode,
    CAST(N'Cross-account duplicate scan' AS nvarchar(260)) AS OriginalFileName,
    candidate.AccountId,
    candidateAccount.AccountNickname,
    candidate.ImportedTransactionId AS CandidateTransactionId,
    candidateAccount.AccountNickname AS CandidateAccountNickname,
    candidate.TransactionDate AS CandidateDate,
    candidate.PostingDate AS CandidatePostingDate,
    candidate.TransactionTime AS CandidateTime,
    candidate.Amount AS CandidateAmount,
    candidate.[Type] AS CandidateType,
    candidate.[Description] AS CandidateDescription,
    candidate.ExternalTransactionId AS CandidateExternalTransactionId,
    candidate.FundingSource AS CandidateFundingSource,
    candidate.SourceCategory AS CandidateSourceCategory,
    candidate.Memo AS CandidateMemo,
    candidate.Category AS CandidateCategory,
    candidate.SpendingClass AS CandidateCategoryType,
    candidate.Merchant AS CandidateMerchant,
    candidate.IsIncome AS CandidateIsIncome,
    candidate.IsTransfer AS CandidateIsTransfer,
    existing.ImportedTransactionId AS ExistingTransactionId,
    existingAccount.AccountNickname AS ExistingAccountNickname,
    existing.TransactionDate AS ExistingDate,
    existing.PostingDate AS ExistingPostingDate,
    existing.TransactionTime AS ExistingTime,
    existing.Amount AS ExistingAmount,
    existing.[Type] AS ExistingType,
    existing.[Description] AS ExistingDescription,
    existing.ExternalTransactionId AS ExistingExternalTransactionId,
    existing.FundingSource AS ExistingFundingSource,
    existing.SourceCategory AS ExistingSourceCategory,
    existing.Memo AS ExistingMemo,
    existing.Category AS ExistingCategory,
    existing.SpendingClass AS ExistingCategoryType,
    existing.Merchant AS ExistingMerchant,
    existing.IsIncome AS ExistingIsIncome,
    existing.IsTransfer AS ExistingIsTransfer,
    r.CreatedAtUtc
FROM dbo.CrossAccountDuplicateReview AS r
LEFT JOIN dbo.ImportedTransaction AS candidate ON candidate.ImportedTransactionId = r.CandidateTransactionId
LEFT JOIN dbo.ImportedTransaction AS existing ON existing.ImportedTransactionId = r.ExistingTransactionId
LEFT JOIN dbo.Account AS candidateAccount ON candidateAccount.AccountId = candidate.AccountId
LEFT JOIN dbo.Account AS existingAccount ON existingAccount.AccountId = existing.AccountId
/*WHERE_CLAUSE*/
ORDER BY r.CreatedAtUtc DESC, r.CrossAccountDuplicateReviewId DESC;
