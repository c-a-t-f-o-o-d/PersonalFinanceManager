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
