DELETE FROM dbo.TransactionLinkRejection
WHERE SourceTransactionId = @SourceTransactionId AND LinkedTransactionId = @LinkedTransactionId AND LinkType = @LinkType;
