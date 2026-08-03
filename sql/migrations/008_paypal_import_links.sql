SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.ImportedTransaction', N'ExternalTransactionId') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportedTransaction ADD ExternalTransactionId nvarchar(100) NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportedTransaction', N'FundingSource') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportedTransaction ADD FundingSource nvarchar(200) NULL;
    END;
END;

IF OBJECT_ID(N'dbo.ImportRow', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.ImportRow', N'ExternalTransactionId') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD ExternalTransactionId nvarchar(100) NULL;
    END;

    IF COL_LENGTH(N'dbo.ImportRow', N'FundingSource') IS NULL
    BEGIN
        ALTER TABLE dbo.ImportRow ADD FundingSource nvarchar(200) NULL;
    END;
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TransactionLink
    (
        TransactionLinkId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TransactionLink PRIMARY KEY,
        SourceTransactionId bigint NOT NULL,
        LinkedTransactionId bigint NOT NULL,
        LinkType nvarchar(100) NOT NULL,
        ConfidenceScore int NOT NULL CONSTRAINT DF_TransactionLink_ConfidenceScore DEFAULT 0,
        Notes nvarchar(1000) NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_TransactionLink_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TransactionLink_Source FOREIGN KEY (SourceTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId),
        CONSTRAINT FK_TransactionLink_Linked FOREIGN KEY (LinkedTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId),
        CONSTRAINT CK_TransactionLink_DifferentRows CHECK (SourceTransactionId <> LinkedTransactionId),
        CONSTRAINT CK_TransactionLink_Confidence CHECK (ConfidenceScore >= 0 AND ConfidenceScore <= 100)
    );
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.TransactionLink', N'MatchedTransactionId') IS NOT NULL
    BEGIN
        DECLARE @DropMatchedForeignKeysSql nvarchar(max) = N'';

        SELECT @DropMatchedForeignKeysSql = @DropMatchedForeignKeysSql + N'ALTER TABLE dbo.TransactionLink DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(13) + CHAR(10)
        FROM sys.foreign_keys AS fk
        INNER JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
        INNER JOIN sys.columns AS c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
        WHERE fk.parent_object_id = OBJECT_ID(N'dbo.TransactionLink')
          AND c.name = N'MatchedTransactionId';

        IF @DropMatchedForeignKeysSql <> N''
        BEGIN
            EXEC sys.sp_executesql @DropMatchedForeignKeysSql;
        END;

        DECLARE @DropMatchedChecksSql nvarchar(max) = N'';

        SELECT @DropMatchedChecksSql = @DropMatchedChecksSql + N'ALTER TABLE dbo.TransactionLink DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + CHAR(13) + CHAR(10)
        FROM sys.check_constraints AS cc
        WHERE cc.parent_object_id = OBJECT_ID(N'dbo.TransactionLink')
          AND cc.[definition] LIKE N'%MatchedTransactionId%';

        IF @DropMatchedChecksSql <> N''
        BEGIN
            EXEC sys.sp_executesql @DropMatchedChecksSql;
        END;

        DECLARE @DropMatchedIndexesSql nvarchar(max) = N'';

        SELECT @DropMatchedIndexesSql = @DropMatchedIndexesSql + N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.TransactionLink;' + CHAR(13) + CHAR(10)
        FROM sys.indexes AS i
        INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID(N'dbo.TransactionLink')
          AND c.name = N'MatchedTransactionId'
          AND i.is_primary_key = 0
          AND i.is_unique_constraint = 0
          AND i.name IS NOT NULL;

        IF @DropMatchedIndexesSql <> N''
        BEGIN
            EXEC sys.sp_executesql @DropMatchedIndexesSql;
        END;
    END;

    IF COL_LENGTH(N'dbo.TransactionLink', N'SourceTransactionId') IS NULL
    BEGIN
        ALTER TABLE dbo.TransactionLink ADD SourceTransactionId bigint NULL;
    END;

    IF COL_LENGTH(N'dbo.TransactionLink', N'LinkedTransactionId') IS NULL
    BEGIN
        ALTER TABLE dbo.TransactionLink ADD LinkedTransactionId bigint NULL;
    END;

    IF COL_LENGTH(N'dbo.TransactionLink', N'MatchedTransactionId') IS NOT NULL
       AND COL_LENGTH(N'dbo.TransactionLink', N'LinkedTransactionId') IS NOT NULL
    BEGIN
        EXEC(N'UPDATE dbo.TransactionLink SET LinkedTransactionId = MatchedTransactionId WHERE LinkedTransactionId IS NULL AND MatchedTransactionId IS NOT NULL;');

        IF EXISTS
        (
            SELECT 1
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.TransactionLink')
              AND name = N'MatchedTransactionId'
              AND is_nullable = 0
        )
        BEGIN
            BEGIN TRY
                EXEC(N'ALTER TABLE dbo.TransactionLink ALTER COLUMN MatchedTransactionId bigint NULL;');
            END TRY
            BEGIN CATCH
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.default_constraints AS dc
                    INNER JOIN sys.columns AS c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.TransactionLink')
                      AND c.name = N'MatchedTransactionId'
                )
                BEGIN
                    EXEC(N'ALTER TABLE dbo.TransactionLink ADD CONSTRAINT DF_TransactionLink_MatchedTransactionId_Legacy DEFAULT 0 FOR MatchedTransactionId;');
                END;
            END CATCH;
        END;
    END;

    IF COL_LENGTH(N'dbo.TransactionLink', N'LinkType') IS NULL
    BEGIN
        ALTER TABLE dbo.TransactionLink ADD LinkType nvarchar(100) NOT NULL CONSTRAINT DF_TransactionLink_LinkType DEFAULT N'PayPalFunding';
    END;

    IF COL_LENGTH(N'dbo.TransactionLink', N'ConfidenceScore') IS NULL
    BEGIN
        ALTER TABLE dbo.TransactionLink ADD ConfidenceScore int NOT NULL CONSTRAINT DF_TransactionLink_ConfidenceScore DEFAULT 0;
    END;

    IF COL_LENGTH(N'dbo.TransactionLink', N'Notes') IS NULL
    BEGIN
        ALTER TABLE dbo.TransactionLink ADD Notes nvarchar(1000) NULL;
    END;

    IF COL_LENGTH(N'dbo.TransactionLink', N'CreatedAtUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.TransactionLink ADD CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_TransactionLink_CreatedAtUtc DEFAULT SYSUTCDATETIME();
    END;
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'SourceTransactionId') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'LinkedTransactionId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE parent_object_id = OBJECT_ID(N'dbo.TransactionLink')
         AND name = N'FK_TransactionLink_Source'
   )
BEGIN
    ALTER TABLE dbo.TransactionLink WITH NOCHECK
        ADD CONSTRAINT FK_TransactionLink_Source FOREIGN KEY (SourceTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId);
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'LinkedTransactionId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE parent_object_id = OBJECT_ID(N'dbo.TransactionLink')
         AND name = N'FK_TransactionLink_Linked'
   )
BEGIN
    ALTER TABLE dbo.TransactionLink WITH NOCHECK
        ADD CONSTRAINT FK_TransactionLink_Linked FOREIGN KEY (LinkedTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId);
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'SourceTransactionId') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'LinkedTransactionId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.check_constraints
       WHERE parent_object_id = OBJECT_ID(N'dbo.TransactionLink')
         AND name = N'CK_TransactionLink_DifferentRows'
   )
BEGIN
    ALTER TABLE dbo.TransactionLink WITH NOCHECK
        ADD CONSTRAINT CK_TransactionLink_DifferentRows CHECK (SourceTransactionId <> LinkedTransactionId);
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'ConfidenceScore') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.check_constraints
       WHERE parent_object_id = OBJECT_ID(N'dbo.TransactionLink')
         AND name = N'CK_TransactionLink_Confidence'
   )
BEGIN
    ALTER TABLE dbo.TransactionLink WITH NOCHECK
        ADD CONSTRAINT CK_TransactionLink_Confidence CHECK (ConfidenceScore >= 0 AND ConfidenceScore <= 100);
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'SourceTransactionId') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'LinkedTransactionId') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'LinkType') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransactionLink') AND name = N'UX_TransactionLink_Source_Linked_Type')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX UX_TransactionLink_Source_Linked_Type ON dbo.TransactionLink(SourceTransactionId, LinkedTransactionId, LinkType) WHERE SourceTransactionId IS NOT NULL AND LinkedTransactionId IS NOT NULL AND LinkType IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'FundingSource') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_PayPalFundingLookup')
BEGIN
    EXEC(N'CREATE INDEX IX_ImportedTransaction_PayPalFundingLookup ON dbo.ImportedTransaction(TransactionDate, Amount, IsTransfer) INCLUDE ([Description], Category, FundingSource);');
END;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'ExternalTransactionId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_ExternalTransactionId')
BEGIN
    EXEC(N'CREATE INDEX IX_ImportedTransaction_ExternalTransactionId ON dbo.ImportedTransaction(ExternalTransactionId) WHERE ExternalTransactionId IS NOT NULL;');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.5.3')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.5.3');
END;
