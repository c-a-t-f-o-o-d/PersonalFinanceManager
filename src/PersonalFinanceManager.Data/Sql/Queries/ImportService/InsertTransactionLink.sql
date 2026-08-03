IF NOT EXISTS
(
    SELECT 1
    FROM dbo.TransactionLink
    WHERE SourceTransactionId = @SourceTransactionId
      AND LinkedTransactionId = @LinkedTransactionId
      AND LinkType = @LinkType
)
BEGIN
    INSERT INTO dbo.TransactionLink
    (
        SourceTransactionId, LinkedTransactionId, LinkType, AllocationAmount, ConfidenceScore, Notes, UpdatedAtUtc
    )
    VALUES
    (
        @SourceTransactionId, @LinkedTransactionId, @LinkType, @AllocationAmount, @ConfidenceScore, @Notes, SYSUTCDATETIME()
    );
    SELECT CAST(1 AS bit);
END
ELSE
BEGIN
    SELECT CAST(0 AS bit);
END;
