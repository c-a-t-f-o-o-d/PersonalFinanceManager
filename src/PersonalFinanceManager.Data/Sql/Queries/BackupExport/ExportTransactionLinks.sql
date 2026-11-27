SELECT TransactionLinkId, SourceTransactionId, LinkedTransactionId, LinkType, AllocationAmount,
       ConfidenceScore, Notes, CreatedAtUtc
FROM dbo.TransactionLink
ORDER BY TransactionLinkId;
