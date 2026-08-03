SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'RawRowHash') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_RawRowHash')
BEGIN
    CREATE INDEX IX_ImportedTransaction_RawRowHash ON dbo.ImportedTransaction(AccountId, RawRowHash);
END;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'ExternalTransactionId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_Account_ExternalTransactionId')
BEGIN
    EXEC(N'CREATE INDEX IX_ImportedTransaction_Account_ExternalTransactionId ON dbo.ImportedTransaction(AccountId, ExternalTransactionId) WHERE ExternalTransactionId IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'FundingSource') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_DataFlowFundingLookup')
BEGIN
    CREATE INDEX IX_ImportedTransaction_DataFlowFundingLookup
        ON dbo.ImportedTransaction(TransactionDate, Amount, IsTransfer)
        INCLUDE ([Description], [Type], Category, SpendingClass, FundingSource);
END;

IF OBJECT_ID(N'dbo.TransactionLink', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'LinkedTransactionId') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransactionLink', N'LinkType') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE t
        SET IsTransfer = 1,
            Category = N''Transfer'',
            SpendingClass = N''PayPal Funding'',
            Notes = CASE
                WHEN t.Notes IS NULL OR LTRIM(RTRIM(t.Notes)) = N'''' THEN N''Auto-linked as PayPal funding transfer.''
                WHEN t.Notes NOT LIKE N''%PayPal funding%'' THEN t.Notes + N'' Auto-linked as PayPal funding transfer.''
                ELSE t.Notes
            END,
            UpdatedAtUtc = SYSUTCDATETIME()
        FROM dbo.ImportedTransaction AS t
        INNER JOIN dbo.TransactionLink AS l ON l.LinkedTransactionId = t.ImportedTransactionId
        WHERE l.LinkType = N''PayPalFunding''
          AND
          (
              t.IsTransfer = 0
              OR ISNULL(t.Category, N'''') <> N''Transfer''
              OR ISNULL(t.SpendingClass, N'''') <> N''PayPal Funding''
          );
    ');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.7.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.7.0');
END;
