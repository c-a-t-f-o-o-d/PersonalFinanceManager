SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.ImportRow', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.ImportRow', N'IsIncome') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD IsIncome bit NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportRow', N'IsTransfer') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD IsTransfer bit NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportRow', N'DuplicateMatchTransactionId') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD DuplicateMatchTransactionId bigint NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportRow', N'DuplicateReason') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD DuplicateReason nvarchar(1000) NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportRow', N'DuplicateConfidence') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD DuplicateConfidence nvarchar(30) NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportRow', N'ResolvedAtUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD ResolvedAtUtc datetime2(7) NULL;
    END;
END;
GO

IF OBJECT_ID(N'dbo.ImportRow', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportRow', N'DuplicateMatchTransactionId') IS NOT NULL
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_ImportRow_DuplicateMatchTransaction'
          AND parent_object_id = OBJECT_ID(N'dbo.ImportRow')
    )
    BEGIN
        ALTER TABLE dbo.ImportRow WITH CHECK ADD CONSTRAINT FK_ImportRow_DuplicateMatchTransaction
            FOREIGN KEY (DuplicateMatchTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId);
    END;
END;
GO

IF OBJECT_ID(N'dbo.ImportRow', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportRow', N'DuplicateReason') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportRow', N'DuplicateConfidence') IS NOT NULL
BEGIN
    UPDATE dbo.ImportRow
    SET [Status] = N'DuplicateReview',
        DuplicateConfidence = ISNULL(DuplicateConfidence, N'Needs Review'),
        DuplicateReason = ISNULL(DuplicateReason, N'Legacy duplicate from earlier import logic. Review before adding.')
    WHERE [Status] = N'Duplicate';
END;
GO

IF OBJECT_ID(N'dbo.ImportRow', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportRow', N'DuplicateMatchTransactionId') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportRow', N'DuplicateConfidence') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportRow') AND name = N'IX_ImportRow_DuplicateReview')
    BEGIN
        EXEC(N'CREATE INDEX IX_ImportRow_DuplicateReview
            ON dbo.ImportRow([Status], CreatedAtUtc DESC)
            INCLUDE (ImportBatchId, RowNumber, TransactionDate, Amount, [Type], [Description], DuplicateMatchTransactionId, DuplicateConfidence);');
    END;
END;
GO

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'RawRowHash') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'PostingDate') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'ExternalTransactionId') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'FundingSource') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'SourceCategory') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'Memo') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_ExactDuplicateLookup')
    BEGIN
        EXEC(N'CREATE INDEX IX_ImportedTransaction_ExactDuplicateLookup
            ON dbo.ImportedTransaction(AccountId, TransactionDate, Amount, RawRowHash)
            INCLUDE (ImportBatchId, PostingDate, TransactionTime, [Type], [Description], ExternalTransactionId, FundingSource, SourceCategory, Memo);');
    END;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.16.1')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.16.1');
END;
