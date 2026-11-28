UPDATE dbo.CrossAccountDuplicateReview
SET [Status] = N'Kept', ResolvedAtUtc = SYSUTCDATETIME()
WHERE [Status] = N'Pending' AND @LinkType <> N'ManualReview'
  AND ((CandidateTransactionId = @SourceTransactionId AND ExistingTransactionId = @LinkedTransactionId)
       OR (CandidateTransactionId = @LinkedTransactionId AND ExistingTransactionId = @SourceTransactionId));

UPDATE dbo.TransactionLink
SET SourceTransactionId = @SourceTransactionId,
    LinkedTransactionId = @LinkedTransactionId,
    LinkType = @LinkType,
    AllocationAmount = @AllocationAmount,
    ConfidenceScore = @ConfidenceScore,
    Notes = @Notes,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TransactionLinkId = @TransactionLinkId;
SELECT @TransactionLinkId;
