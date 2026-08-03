UPDATE dbo.CrossAccountDuplicateReview
SET [Status] = N'CandidateDeleted',
    CandidateTransactionId = NULL,
    ResolvedAtUtc = SYSUTCDATETIME()
WHERE CrossAccountDuplicateReviewId = @CrossAccountDuplicateReviewId;
