SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.Account', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Account', N'AccountOwner') IS NULL
BEGIN
    ALTER TABLE dbo.Account ADD AccountOwner nvarchar(50) NOT NULL CONSTRAINT DF_Account_AccountOwner DEFAULT N'Unassigned';
END;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.ImportedTransaction', N'IncomeOwner') IS NULL
BEGIN
    ALTER TABLE dbo.ImportedTransaction ADD IncomeOwner nvarchar(50) NULL;
END;

IF OBJECT_ID(N'dbo.ImportRow', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.ImportRow', N'IncomeOwner') IS NULL
BEGIN
    ALTER TABLE dbo.ImportRow ADD IncomeOwner nvarchar(50) NULL;
END;

IF OBJECT_ID(N'dbo.FinancialRule', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.FinancialRule', N'IncomeOwner') IS NULL
BEGIN
    ALTER TABLE dbo.FinancialRule ADD IncomeOwner nvarchar(50) NULL;
END;
GO

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Account', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'IncomeOwner') IS NOT NULL
   AND COL_LENGTH(N'dbo.Account', N'AccountOwner') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE t
        SET IncomeOwner = a.AccountOwner,
            UpdatedAtUtc = SYSUTCDATETIME()
        FROM dbo.ImportedTransaction AS t
        INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
        WHERE t.IsIncome = 1
          AND t.IsTransfer = 0
          AND t.Amount > 0
          AND (t.IncomeOwner IS NULL OR LTRIM(RTRIM(t.IncomeOwner)) = N'''' OR t.IncomeOwner = N''Unassigned'')
          AND a.AccountOwner IN (N''Me'', N''Spouse'');
    ');
END;
GO

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ImportedTransaction', N'IncomeOwner') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_IncomeOwner')
BEGIN
    EXEC(N'CREATE INDEX IX_ImportedTransaction_IncomeOwner ON dbo.ImportedTransaction(IncomeOwner, IsIncome, IsTransfer, TransactionDate) INCLUDE (Amount, AccountId);');
END;

IF OBJECT_ID(N'dbo.Account', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Account', N'AccountOwner') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Account') AND name = N'IX_Account_AccountOwner')
BEGIN
    EXEC(N'CREATE INDEX IX_Account_AccountOwner ON dbo.Account(AccountOwner, IsActive, AccountType);');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.19.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.19.0');
END;
