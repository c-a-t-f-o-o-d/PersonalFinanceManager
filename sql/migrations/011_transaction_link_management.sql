SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.TransactionLink', N'UpdatedAtUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.TransactionLink ADD UpdatedAtUtc datetime2(7) NULL;
    END;
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'LinkType') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransactionLink') AND name = N'IX_TransactionLink_Type_Created')
BEGIN
    EXEC(N'CREATE INDEX IX_TransactionLink_Type_Created ON dbo.TransactionLink(LinkType, CreatedAtUtc DESC) INCLUDE (SourceTransactionId, LinkedTransactionId, ConfidenceScore);');
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'SourceTransactionId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransactionLink') AND name = N'IX_TransactionLink_Source')
BEGIN
    EXEC(N'CREATE INDEX IX_TransactionLink_Source ON dbo.TransactionLink(SourceTransactionId);');
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'LinkedTransactionId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransactionLink') AND name = N'IX_TransactionLink_Linked')
BEGIN
    EXEC(N'CREATE INDEX IX_TransactionLink_Linked ON dbo.TransactionLink(LinkedTransactionId);');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.8.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.8.0');
END;
