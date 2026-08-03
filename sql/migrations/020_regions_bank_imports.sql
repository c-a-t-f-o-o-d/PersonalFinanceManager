SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_BankTransferLookup')
BEGIN
    CREATE INDEX IX_ImportedTransaction_BankTransferLookup
        ON dbo.ImportedTransaction(TransactionDate, Amount, IsTransfer)
        INCLUDE (AccountId, Category, SpendingClass, [Description]);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.17.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.17.0');
END;
