UPDATE dbo.CrossAccountDuplicateReview
SET [Status] = N'Kept', ResolvedAtUtc = SYSUTCDATETIME()
WHERE [Status] = N'Pending' AND @LinkType <> N'ManualReview'
  AND ((CandidateTransactionId = @SourceTransactionId AND ExistingTransactionId = @LinkedTransactionId)
       OR (CandidateTransactionId = @LinkedTransactionId AND ExistingTransactionId = @SourceTransactionId));

INSERT INTO dbo.TransactionLink
(
    SourceTransactionId, LinkedTransactionId, LinkType, AllocationAmount, ConfidenceScore, Notes, UpdatedAtUtc
)
OUTPUT INSERTED.TransactionLinkId
VALUES
(
    @SourceTransactionId, @LinkedTransactionId, @LinkType, @AllocationAmount, @ConfidenceScore, @Notes, SYSUTCDATETIME()
);
