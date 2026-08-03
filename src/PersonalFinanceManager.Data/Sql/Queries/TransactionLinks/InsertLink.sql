INSERT INTO dbo.TransactionLink
(
    SourceTransactionId, LinkedTransactionId, LinkType, AllocationAmount, ConfidenceScore, Notes, UpdatedAtUtc
)
OUTPUT INSERTED.TransactionLinkId
VALUES
(
    @SourceTransactionId, @LinkedTransactionId, @LinkType, @AllocationAmount, @ConfidenceScore, @Notes, SYSUTCDATETIME()
);
