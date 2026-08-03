UPDATE dbo.CrossAccountDuplicateReview
SET [Status] = @Status,
    ResolvedAtUtc = SYSUTCDATETIME()
WHERE CrossAccountDuplicateReviewId = @CrossAccountDuplicateReviewId
  AND [Status] = N'Pending';
