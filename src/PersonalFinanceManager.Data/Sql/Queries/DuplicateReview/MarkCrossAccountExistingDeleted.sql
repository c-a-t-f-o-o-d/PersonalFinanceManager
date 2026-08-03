UPDATE dbo.CrossAccountDuplicateReview
SET [Status] = N'ExistingDeleted',
    ExistingTransactionId = NULL,
    ResolvedAtUtc = SYSUTCDATETIME()
WHERE CrossAccountDuplicateReviewId = @CrossAccountDuplicateReviewId;
