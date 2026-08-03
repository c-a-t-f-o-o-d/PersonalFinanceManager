SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.ImportedTransaction', N'IsManual') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportedTransaction ADD IsManual bit NOT NULL CONSTRAINT DF_ImportedTransaction_IsManual DEFAULT 0;
    END;

    IF COL_LENGTH(N'dbo.ImportedTransaction', N'Notes') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportedTransaction ADD Notes nvarchar(1000) NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportedTransaction', N'UpdatedAtUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportedTransaction ADD UpdatedAtUtc datetime2(7) NULL;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction')
          AND name = N'ImportBatchId'
          AND is_nullable = 0
    )
    BEGIN
        IF EXISTS
        (
            SELECT 1
            FROM sys.foreign_keys
            WHERE name = N'FK_ImportedTransaction_ImportBatch'
              AND parent_object_id = OBJECT_ID(N'dbo.ImportedTransaction')
        )
        BEGIN
            ALTER TABLE dbo.ImportedTransaction DROP CONSTRAINT FK_ImportedTransaction_ImportBatch;
        END;

        ALTER TABLE dbo.ImportedTransaction ALTER COLUMN ImportBatchId bigint NULL;

        ALTER TABLE dbo.ImportedTransaction WITH CHECK ADD CONSTRAINT FK_ImportedTransaction_ImportBatch
            FOREIGN KEY (ImportBatchId) REFERENCES dbo.ImportBatch(ImportBatchId);
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_Search')
    BEGIN
        CREATE INDEX IX_ImportedTransaction_Search ON dbo.ImportedTransaction(AccountId, TransactionDate DESC, IsManual, IsIncome, IsTransfer)
            INCLUDE (Amount, [Type], Category, SpendingClass);
    END;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.1.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.1.0');
END;
