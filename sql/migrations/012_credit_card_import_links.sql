SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.ImportedTransaction', N'PostingDate') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportedTransaction ADD PostingDate date NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportedTransaction', N'SourceCategory') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportedTransaction ADD SourceCategory nvarchar(200) NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportedTransaction', N'Memo') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportedTransaction ADD Memo nvarchar(1000) NULL;
    END;
END;

IF OBJECT_ID(N'dbo.ImportRow', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.ImportRow', N'PostingDate') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD PostingDate date NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportRow', N'SourceCategory') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD SourceCategory nvarchar(200) NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportRow', N'Memo') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD Memo nvarchar(1000) NULL;
    END;
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.TransactionLink', N'AllocationAmount') IS NULL
    BEGIN
        ALTER TABLE dbo.TransactionLink ADD AllocationAmount decimal(18,2) NULL;
    END;
END;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'PostingDate') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_CreditCardPaymentLookup')
BEGIN
    EXEC(N'CREATE INDEX IX_ImportedTransaction_CreditCardPaymentLookup ON dbo.ImportedTransaction(AccountId, TransactionDate, Amount, [Type]) INCLUDE ([Description], Category, SpendingClass);');
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'AllocationAmount') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransactionLink') AND name = N'IX_TransactionLink_CreditCardAllocation')
BEGIN
    EXEC(N'CREATE INDEX IX_TransactionLink_CreditCardAllocation ON dbo.TransactionLink(LinkType, SourceTransactionId, LinkedTransactionId) INCLUDE (AllocationAmount);');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.9.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.9.0');
END;
