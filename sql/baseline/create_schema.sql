/*
    PersonalFinanceManager consolidated schema baseline.

    This script is intended for human review and clean database provisioning. The running
    application initializes and upgrades databases from sql/migrations/*.sql. Keep this baseline
    synchronized when the public schema changes substantially.

    Generated from the current public migration chain through schema marker 2.22.0.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;



/* ===== 001_create_schema.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.SchemaMetadata', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaMetadata
    (
        SchemaMetadataId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SchemaMetadata PRIMARY KEY,
        SchemaVersion nvarchar(20) NOT NULL,
        AppliedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SchemaMetadata_AppliedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;


IF OBJECT_ID(N'dbo.SchemaMigration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaMigration
    (
        SchemaMigrationId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SchemaMigration PRIMARY KEY,
        ScriptName nvarchar(260) NOT NULL,
        SchemaVersion nvarchar(40) NULL,
        ScriptHash nvarchar(64) NOT NULL,
        AppliedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SchemaMigration_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
        [Status] nvarchar(30) NOT NULL,
        [Message] nvarchar(1000) NULL,
        CONSTRAINT UQ_SchemaMigration_ScriptName UNIQUE (ScriptName)
    );
END;

IF OBJECT_ID(N'dbo.Account', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Account
    (
        AccountId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Account PRIMARY KEY,
        AccountNickname nvarchar(200) NOT NULL,
        Institution nvarchar(200) NOT NULL,
        BaseUrl nvarchar(1000) NULL,
        AccountSummaryUrl nvarchar(1000) NULL,
        AccountType nvarchar(100) NOT NULL,
        AccountNumberProtected varbinary(max) NULL,
        AccountNumberLastFour nvarchar(10) NULL,
        PrincipalBalance decimal(18,2) NULL,
        AvailableBalance decimal(18,2) NULL,
        CurrentBalance decimal(18,2) NULL,
        OriginalLoanAmount decimal(18,2) NULL,
        CurrentInterestRate decimal(9,5) NULL,
        LastPaymentAmount decimal(18,2) NULL,
        NextPaymentAmount decimal(18,2) NULL,
        NextPaymentDueDate date NULL,
        NextPaymentInterest decimal(9,5) NULL,
        NextPaymentEscrowAmount decimal(18,2) NULL,
        LastEscrowAnalysisDate date NULL,
        CurrentEscrowAccountBalance decimal(18,2) NULL,
        CurrentEscrowPayment decimal(18,2) NULL,
        ProjectedEscrowPayment decimal(18,2) NULL,
        ProjectedEscrowPaymentDate date NULL,
        LastPaymentInterestAmount decimal(18,2) NULL,
        LastPaymentEscrow decimal(18,2) NULL,
        RegularMonthlyPayment decimal(18,2) NULL,
        LatePaymentFee decimal(18,2) NULL,
        EscrowDeletionFee nvarchar(200) NULL,
        LateCharges nvarchar(200) NULL,
        LoanAssumptionFee nvarchar(200) NULL,
        PartialReleaseFee nvarchar(200) NULL,
        PrincipalReductionReAmortizationFee nvarchar(200) NULL,
        LateAfterDays int NULL,
        LastPaymentDateReceived date NULL,
        Recurrence nvarchar(100) NULL,
        PaidInFullDiscount decimal(18,2) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Account_IsActive DEFAULT 1,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Account_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Account_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.FinancialRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FinancialRule
    (
        RuleId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FinancialRule PRIMARY KEY,
        RuleName nvarchar(200) NOT NULL,
        Priority int NOT NULL CONSTRAINT DF_FinancialRule_Priority DEFAULT 100,
        MatchDescription nvarchar(1000) NOT NULL,
        MatchType nvarchar(100) NULL,
        DescriptionMatchMode nvarchar(30) NOT NULL CONSTRAINT DF_FinancialRule_DescriptionMatchMode DEFAULT N'Exact',
        TypeMatchMode nvarchar(30) NOT NULL CONSTRAINT DF_FinancialRule_TypeMatchMode DEFAULT N'Exact',
        Category nvarchar(200) NULL,
        SpendingClass nvarchar(100) NULL,
        Merchant nvarchar(200) NULL,
        IsIncome bit NULL,
        IsTransfer bit NULL,
        IsActive bit NOT NULL CONSTRAINT DF_FinancialRule_IsActive DEFAULT 1,
        Notes nvarchar(1000) NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_FinancialRule_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_FinancialRule_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_FinancialRule_DescriptionMatchMode CHECK (DescriptionMatchMode IN (N'Exact', N'Contains', N'StartsWith')),
        CONSTRAINT CK_FinancialRule_TypeMatchMode CHECK (TypeMatchMode IN (N'Exact', N'Contains', N'StartsWith'))
    );
END;

IF OBJECT_ID(N'dbo.ImportBatch', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImportBatch
    (
        ImportBatchId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ImportBatch PRIMARY KEY,
        AccountId int NOT NULL,
        ImportProfileCode nvarchar(100) NOT NULL,
        OriginalFileName nvarchar(260) NOT NULL,
        FileSha256 nvarchar(64) NOT NULL,
        ImportedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ImportBatch_ImportedAtUtc DEFAULT SYSUTCDATETIME(),
        TotalRowCount int NOT NULL CONSTRAINT DF_ImportBatch_TotalRowCount DEFAULT 0,
        ImportedRowCount int NOT NULL CONSTRAINT DF_ImportBatch_ImportedRowCount DEFAULT 0,
        DuplicateRowCount int NOT NULL CONSTRAINT DF_ImportBatch_DuplicateRowCount DEFAULT 0,
        ErrorRowCount int NOT NULL CONSTRAINT DF_ImportBatch_ErrorRowCount DEFAULT 0,
        CONSTRAINT FK_ImportBatch_Account FOREIGN KEY (AccountId) REFERENCES dbo.Account(AccountId)
    );
END;

IF OBJECT_ID(N'dbo.ImportRow', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImportRow
    (
        ImportRowId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ImportRow PRIMARY KEY,
        ImportBatchId bigint NOT NULL,
        RowNumber int NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        ErrorMessage nvarchar(1000) NULL,
        TransactionDate date NULL,
        TransactionTime time(0) NULL,
        Amount decimal(18,2) NULL,
        [Type] nvarchar(100) NULL,
        [Description] nvarchar(1000) NULL,
        AppliedRuleId int NULL,
        Category nvarchar(200) NULL,
        SpendingClass nvarchar(100) NULL,
        Merchant nvarchar(200) NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ImportRow_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ImportRow_ImportBatch FOREIGN KEY (ImportBatchId) REFERENCES dbo.ImportBatch(ImportBatchId) ON DELETE CASCADE,
        CONSTRAINT FK_ImportRow_FinancialRule FOREIGN KEY (AppliedRuleId) REFERENCES dbo.FinancialRule(RuleId) ON DELETE SET NULL
    );
END;

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImportedTransaction
    (
        ImportedTransactionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ImportedTransaction PRIMARY KEY,
        AccountId int NOT NULL,
        ImportBatchId bigint NULL,
        TransactionDate date NOT NULL,
        TransactionTime time(0) NULL,
        Amount decimal(18,2) NOT NULL,
        [Type] nvarchar(100) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        AppliedRuleId int NULL,
        Category nvarchar(200) NULL,
        SpendingClass nvarchar(100) NULL,
        Merchant nvarchar(200) NULL,
        IsIncome bit NOT NULL CONSTRAINT DF_ImportedTransaction_IsIncome DEFAULT 0,
        IsTransfer bit NOT NULL CONSTRAINT DF_ImportedTransaction_IsTransfer DEFAULT 0,
        IsManual bit NOT NULL CONSTRAINT DF_ImportedTransaction_IsManual DEFAULT 0,
        Notes nvarchar(1000) NULL,
        RawRowHash nvarchar(64) NOT NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ImportedTransaction_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(7) NULL,
        CONSTRAINT FK_ImportedTransaction_Account FOREIGN KEY (AccountId) REFERENCES dbo.Account(AccountId),
        CONSTRAINT FK_ImportedTransaction_ImportBatch FOREIGN KEY (ImportBatchId) REFERENCES dbo.ImportBatch(ImportBatchId),
        CONSTRAINT FK_ImportedTransaction_FinancialRule FOREIGN KEY (AppliedRuleId) REFERENCES dbo.FinancialRule(RuleId) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Account') AND name = N'IX_Account_Institution_Nickname')
BEGIN
    CREATE INDEX IX_Account_Institution_Nickname ON dbo.Account(Institution, AccountNickname);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FinancialRule') AND name = N'IX_FinancialRule_Active_Priority')
BEGIN
    CREATE INDEX IX_FinancialRule_Active_Priority ON dbo.FinancialRule(IsActive, Priority, RuleName);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportBatch') AND name = N'IX_ImportBatch_Account_Date')
BEGIN
    CREATE INDEX IX_ImportBatch_Account_Date ON dbo.ImportBatch(AccountId, ImportedAtUtc DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportBatch') AND name = N'IX_ImportBatch_FileSha256')
BEGIN
    CREATE INDEX IX_ImportBatch_FileSha256 ON dbo.ImportBatch(FileSha256);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_DuplicateLookup')
BEGIN
    CREATE INDEX IX_ImportedTransaction_DuplicateLookup ON dbo.ImportedTransaction(AccountId, TransactionDate, Amount) INCLUDE (TransactionTime, [Type], [Description]);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.0.3')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.0.3');
END;

GO


/* ===== 002_seed.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.AppLookup', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppLookup
    (
        AppLookupId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppLookup PRIMARY KEY,
        LookupGroup nvarchar(100) NOT NULL,
        LookupCode nvarchar(100) NOT NULL,
        DisplayName nvarchar(200) NOT NULL,
        SortOrder int NOT NULL CONSTRAINT DF_AppLookup_SortOrder DEFAULT 100,
        IsActive bit NOT NULL CONSTRAINT DF_AppLookup_IsActive DEFAULT 1,
        CONSTRAINT UQ_AppLookup_Group_Code UNIQUE (LookupGroup, LookupCode)
    );
END;

MERGE dbo.AppLookup AS target
USING
(
    VALUES
    (N'AccountType', N'CHECKING', N'Checking', 10),
    (N'AccountType', N'SAVINGS', N'Savings', 20),
    (N'AccountType', N'CREDIT_CARD', N'Credit Card', 30),
    (N'AccountType', N'MORTGAGE', N'Mortgage', 40),
    (N'AccountType', N'VEHICLE_LOAN', N'Vehicle Loan', 50),
    (N'AccountType', N'PERSONAL_LOAN', N'Personal Loan', 60),
    (N'AccountType', N'INVESTMENT', N'Investment', 70),
    (N'AccountType', N'401K', N'401k', 80),
    (N'AccountType', N'IRA', N'IRA', 90),
    (N'AccountType', N'PAYPAL_SMART_CONNECT', N'PayPal Smart Connect', 100),
    (N'AccountType', N'OTHER', N'Other', 900),
    (N'ImportProfile', N'ALLY_CHECKING_CSV', N'Ally Checking CSV', 10)
) AS source (LookupGroup, LookupCode, DisplayName, SortOrder)
ON target.LookupGroup = source.LookupGroup AND target.LookupCode = source.LookupCode
WHEN MATCHED THEN
    UPDATE SET DisplayName = source.DisplayName, SortOrder = source.SortOrder, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (LookupGroup, LookupCode, DisplayName, SortOrder)
    VALUES (source.LookupGroup, source.LookupCode, source.DisplayName, source.SortOrder);

GO


/* ===== 003_seed_user_rules.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- User-specific categorization rules are intentionally not seeded in the public repository.
-- Import or create rules from the application UI so private merchant/payee names are not committed.
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.0.2-user-rules')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.0.2-user-rules');
END;

GO


/* ===== 004_transactions_crud.sql ===== */

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

GO


/* ===== 005_reports_iteration.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.2.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.2.0');
END;

GO


/* ===== 006_backup_and_bills.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.DatabaseBackupHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DatabaseBackupHistory
    (
        DatabaseBackupHistoryId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DatabaseBackupHistory PRIMARY KEY,
        BackupType nvarchar(50) NOT NULL,
        FilePath nvarchar(1000) NOT NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_DatabaseBackupHistory_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        Succeeded bit NOT NULL CONSTRAINT DF_DatabaseBackupHistory_Succeeded DEFAULT 0,
        Verified bit NOT NULL CONSTRAINT DF_DatabaseBackupHistory_Verified DEFAULT 0,
        ErrorMessage nvarchar(2000) NULL
    );
END;

IF OBJECT_ID(N'dbo.RecurringBill', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecurringBill
    (
        RecurringBillId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecurringBill PRIMARY KEY,
        AccountId int NULL,
        BillName nvarchar(200) NOT NULL,
        Payee nvarchar(300) NOT NULL,
        Category nvarchar(200) NULL,
        SpendingClass nvarchar(100) NULL,
        RequirementType nvarchar(100) NOT NULL CONSTRAINT DF_RecurringBill_RequirementType DEFAULT N'Need',
        TypicalAmount decimal(18,2) NOT NULL,
        DueDay int NULL,
        NextDueDate date NULL,
        Frequency nvarchar(50) NOT NULL CONSTRAINT DF_RecurringBill_Frequency DEFAULT N'Monthly',
        IsRequired bit NOT NULL CONSTRAINT DF_RecurringBill_IsRequired DEFAULT 1,
        IsActive bit NOT NULL CONSTRAINT DF_RecurringBill_IsActive DEFAULT 1,
        SourceDescription nvarchar(1000) NULL,
        FirstSeenDate date NULL,
        LastSeenDate date NULL,
        TransactionCount int NOT NULL CONSTRAINT DF_RecurringBill_TransactionCount DEFAULT 0,
        Notes nvarchar(1000) NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_RecurringBill_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_RecurringBill_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_RecurringBill_Account FOREIGN KEY (AccountId) REFERENCES dbo.Account(AccountId) ON DELETE SET NULL,
        CONSTRAINT CK_RecurringBill_DueDay CHECK (DueDay IS NULL OR (DueDay >= 1 AND DueDay <= 31)),
        CONSTRAINT CK_RecurringBill_TypicalAmount CHECK (TypicalAmount >= 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DatabaseBackupHistory') AND name = N'IX_DatabaseBackupHistory_CreatedAtUtc')
BEGIN
    CREATE INDEX IX_DatabaseBackupHistory_CreatedAtUtc ON dbo.DatabaseBackupHistory(CreatedAtUtc DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RecurringBill') AND name = N'IX_RecurringBill_Active_Required')
BEGIN
    CREATE INDEX IX_RecurringBill_Active_Required ON dbo.RecurringBill(IsActive, IsRequired, RequirementType, NextDueDate);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RecurringBill') AND name = N'IX_RecurringBill_Source')
BEGIN
    CREATE INDEX IX_RecurringBill_Source ON dbo.RecurringBill(AccountId, SourceDescription) INCLUDE (BillName, TypicalAmount, IsActive);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.3.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.3.0');
END;

GO


/* ===== 007_report_range_metrics.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.4.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.4.0');
END;

GO


/* ===== 008_paypal_import_links.sql ===== */

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

GO


/* ===== 009_analysis_sections.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.6.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.6.0');
END;

GO


/* ===== 010_data_flow_integrity.sql ===== */

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

GO


/* ===== 011_transaction_link_management.sql ===== */

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

GO


/* ===== 012_credit_card_import_links.sql ===== */

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

GO


/* ===== 013_payment_funding_report_integrity.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH(N'dbo.ImportedTransaction', N'FundingSource') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE bank
        SET IsTransfer = 1,
            Category = N''Transfer'',
            SpendingClass = N''PayPal Funding'',
            Notes = CASE
                WHEN bank.Notes IS NULL OR LTRIM(RTRIM(bank.Notes)) = N'''' THEN N''Auto-normalized by report/data-flow integrity pass as likely PayPal funding transfer.''
                WHEN bank.Notes NOT LIKE N''%PayPal funding%'' THEN bank.Notes + N'' Auto-normalized by report/data-flow integrity pass as likely PayPal funding transfer.''
                ELSE bank.Notes
            END,
            UpdatedAtUtc = SYSUTCDATETIME()
        FROM dbo.ImportedTransaction AS bank
        INNER JOIN dbo.Account AS bankAccount ON bankAccount.AccountId = bank.AccountId
        WHERE bank.Amount < 0
          AND UPPER(bankAccount.AccountType) NOT LIKE N''%PAYPAL%''
          AND
          (
              UPPER(ISNULL(bank.Category, N'''')) = N''PAYPAL''
              OR UPPER(ISNULL(bank.SpendingClass, N'''')) IN (N''PAYPAL'', N''PAYPAL FUNDING'')
              OR UPPER(bank.[Description]) LIKE N''%PAYPAL%''
          )
          AND EXISTS
          (
              SELECT 1
              FROM dbo.ImportedTransaction AS payPal
              INNER JOIN dbo.Account AS payPalAccount ON payPalAccount.AccountId = payPal.AccountId
              WHERE payPal.ImportedTransactionId <> bank.ImportedTransactionId
                AND payPal.Amount = bank.Amount
                AND payPal.TransactionDate BETWEEN DATEADD(day, -5, bank.TransactionDate) AND DATEADD(day, 7, bank.TransactionDate)
                AND
                (
                    UPPER(payPalAccount.AccountType) LIKE N''%PAYPAL%''
                    OR payPal.FundingSource IS NOT NULL
                    OR UPPER(ISNULL(payPal.[Type], N'''')) LIKE N''%PAYPAL%''
                )
          )
          AND
          (
              bank.IsTransfer = 0
              OR ISNULL(bank.Category, N'''') <> N''Transfer''
              OR ISNULL(bank.SpendingClass, N'''') <> N''PayPal Funding''
          );
    ');
END;

EXEC(N'
    UPDATE bank
    SET IsTransfer = 1,
        Category = N''Transfer'',
        SpendingClass = N''Credit Card Payment'',
        Notes = CASE
            WHEN bank.Notes IS NULL OR LTRIM(RTRIM(bank.Notes)) = N'''' THEN N''Auto-normalized by report/data-flow integrity pass as likely credit card payment funding transfer.''
            WHEN bank.Notes NOT LIKE N''%credit card payment%'' THEN bank.Notes + N'' Auto-normalized by report/data-flow integrity pass as likely credit card payment funding transfer.''
            ELSE bank.Notes
        END,
        UpdatedAtUtc = SYSUTCDATETIME()
    FROM dbo.ImportedTransaction AS bank
    INNER JOIN dbo.Account AS bankAccount ON bankAccount.AccountId = bank.AccountId
    WHERE bank.Amount < 0
      AND UPPER(bankAccount.AccountType) NOT LIKE N''%CREDIT%''
      AND
      (
          UPPER(ISNULL(bank.Category, N'''')) IN (N''DEBT (CREDIT CARD)'', N''CREDIT CARD PAYMENT'')
          OR UPPER(ISNULL(bank.SpendingClass, N'''')) IN (N''PAYMENT'', N''CREDIT CARD PAYMENT'')
          OR UPPER(bank.[Description]) LIKE N''%APPLECARD%''
          OR UPPER(bank.[Description]) LIKE N''%GSBANK%''
          OR UPPER(bank.[Description]) LIKE N''%CHASE CREDIT%''
          OR UPPER(bank.[Description]) LIKE N''%CREDIT CRD%''
          OR UPPER(bank.[Description]) LIKE N''%CARD PAYMENT%''
      )
      AND EXISTS
      (
          SELECT 1
          FROM dbo.ImportedTransaction AS cardPayment
          INNER JOIN dbo.Account AS cardAccount ON cardAccount.AccountId = cardPayment.AccountId
          WHERE cardPayment.ImportedTransactionId <> bank.ImportedTransactionId
            AND UPPER(cardAccount.AccountType) LIKE N''%CREDIT%''
            AND cardPayment.Amount = -bank.Amount
            AND cardPayment.Amount > 0
            AND UPPER(LTRIM(RTRIM(cardPayment.[Type]))) = N''PAYMENT''
            AND cardPayment.TransactionDate BETWEEN DATEADD(day, -7, bank.TransactionDate) AND DATEADD(day, 10, bank.TransactionDate)
      )
      AND
      (
          bank.IsTransfer = 0
          OR ISNULL(bank.Category, N'''') <> N''Transfer''
          OR ISNULL(bank.SpendingClass, N'''') <> N''Credit Card Payment''
      );
');

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.10.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.10.0');
END;

GO


/* ===== 014_apple_card_import.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.11.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.11.0');
END;

GO


/* ===== 015_salary_needed_analysis.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.12.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.12.0');
END;

GO


/* ===== 016_salary_modes_allowances.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.13.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.13.0');
END;

GO


/* ===== 017_ui_salary_cleanup.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.14.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.14.0');
END;

GO


/* ===== 018_static_layout_followup.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.15.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.15.0');
END;

GO


/* ===== 019_duplicate_review_workflow.sql ===== */

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

GO


/* ===== 020_regions_bank_imports.sql ===== */

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

GO


/* ===== 021_cross_account_duplicate_review.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.CrossAccountDuplicateReview', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrossAccountDuplicateReview
    (
        CrossAccountDuplicateReviewId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_CrossAccountDuplicateReview PRIMARY KEY,
        CandidateTransactionId bigint NULL,
        ExistingTransactionId bigint NULL,
        [Status] nvarchar(30) NOT NULL CONSTRAINT DF_CrossAccountDuplicateReview_Status DEFAULT N'Pending',
        Reason nvarchar(1000) NULL,
        Confidence nvarchar(30) NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CrossAccountDuplicateReview_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ResolvedAtUtc datetime2(7) NULL,
        CONSTRAINT CK_CrossAccountDuplicateReview_Status CHECK ([Status] IN (N'Pending', N'Kept', N'CandidateDeleted', N'ExistingDeleted'))
    );
END;
GO

IF OBJECT_ID(N'dbo.CrossAccountDuplicateReview', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'CandidateTransactionId') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD CandidateTransactionId bigint NULL;
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'ExistingTransactionId') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD ExistingTransactionId bigint NULL;
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'Status') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD [Status] nvarchar(30) NOT NULL CONSTRAINT DF_CrossAccountDuplicateReview_Status2 DEFAULT N'Pending';
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'Reason') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD Reason nvarchar(1000) NULL;
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'Confidence') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD Confidence nvarchar(30) NULL;
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'CreatedAtUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CrossAccountDuplicateReview_CreatedAtUtc2 DEFAULT SYSUTCDATETIME();
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'ResolvedAtUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD ResolvedAtUtc datetime2(7) NULL;
    END;
END;
GO

IF OBJECT_ID(N'dbo.CrossAccountDuplicateReview', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'CandidateTransactionId') IS NOT NULL
   AND COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'ExistingTransactionId') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CrossAccountDuplicateReview') AND name = N'UX_CrossAccountDuplicateReview_Pair')
    BEGIN
        EXEC(N'CREATE UNIQUE INDEX UX_CrossAccountDuplicateReview_Pair
            ON dbo.CrossAccountDuplicateReview(CandidateTransactionId, ExistingTransactionId)
            WHERE CandidateTransactionId IS NOT NULL AND ExistingTransactionId IS NOT NULL;');
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CrossAccountDuplicateReview') AND name = N'IX_CrossAccountDuplicateReview_Status')
    BEGIN
        EXEC(N'CREATE INDEX IX_CrossAccountDuplicateReview_Status
            ON dbo.CrossAccountDuplicateReview([Status], CreatedAtUtc DESC)
            INCLUDE (CandidateTransactionId, ExistingTransactionId, Confidence);');
    END;
END;
GO

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_CrossAccountDuplicateScan')
    BEGIN
        EXEC(N'CREATE INDEX IX_ImportedTransaction_CrossAccountDuplicateScan
            ON dbo.ImportedTransaction(TransactionDate, Amount, [Type], [Description])
            INCLUDE (AccountId, PostingDate, TransactionTime, Category, SpendingClass);');
    END;
END;
GO

IF OBJECT_ID(N'dbo.CrossAccountDuplicateReview', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.CrossAccountDuplicateReview
    (
        CandidateTransactionId,
        ExistingTransactionId,
        [Status],
        Reason,
        Confidence
    )
    SELECT
        t1.ImportedTransactionId,
        t2.ImportedTransactionId,
        N'Pending',
        N'Same transaction date, amount, type, and description appear under different accounts. Verify whether one account import was selected incorrectly or whether both entries are legitimate.',
        N'Needs Review'
    FROM dbo.ImportedTransaction AS t1
    INNER JOIN dbo.ImportedTransaction AS t2 ON t1.ImportedTransactionId < t2.ImportedTransactionId
    WHERE t1.AccountId <> t2.AccountId
      AND t1.TransactionDate = t2.TransactionDate
      AND t1.Amount = t2.Amount
      AND UPPER(LTRIM(RTRIM(t1.[Type]))) = UPPER(LTRIM(RTRIM(t2.[Type])))
      AND UPPER(LTRIM(RTRIM(t1.[Description]))) = UPPER(LTRIM(RTRIM(t2.[Description])))
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.CrossAccountDuplicateReview AS existing
          WHERE
              (existing.CandidateTransactionId = t1.ImportedTransactionId AND existing.ExistingTransactionId = t2.ImportedTransactionId)
              OR
              (existing.CandidateTransactionId = t2.ImportedTransactionId AND existing.ExistingTransactionId = t1.ImportedTransactionId)
      )
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.TransactionLink AS link
          WHERE
              (link.SourceTransactionId = t1.ImportedTransactionId AND link.LinkedTransactionId = t2.ImportedTransactionId)
              OR
              (link.SourceTransactionId = t2.ImportedTransactionId AND link.LinkedTransactionId = t1.ImportedTransactionId)
      );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.18.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.18.0');
END;

GO


/* ===== 022_income_owner_and_data_health.sql ===== */

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

GO


/* ===== 023_data_health_owner_sync.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.20.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.20.0');
END;

GO


IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.21.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.21.0');
END;

GO

/* ===== 025_financial_integrity.sql ===== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.Account', N'BalanceAsOfDate') IS NULL
    ALTER TABLE dbo.Account ADD BalanceAsOfDate date NULL;

IF COL_LENGTH(N'dbo.ImportedTransaction', N'ClassificationIsManual') IS NULL
    ALTER TABLE dbo.ImportedTransaction ADD ClassificationIsManual bit NOT NULL
        CONSTRAINT DF_ImportedTransaction_ClassificationIsManual DEFAULT 0;

IF COL_LENGTH(N'dbo.ImportedTransaction', N'LegacyLinkTreatment') IS NULL
    ALTER TABLE dbo.ImportedTransaction ADD LegacyLinkTreatment bit NOT NULL
        CONSTRAINT DF_ImportedTransaction_LegacyLinkTreatment DEFAULT 0;

IF COL_LENGTH(N'dbo.ImportRow', N'AcceptedTransactionId') IS NULL
    ALTER TABLE dbo.ImportRow ADD AcceptedTransactionId bigint NULL;
GO

UPDATE dbo.ImportedTransaction
SET ClassificationIsManual = 1
WHERE IsManual = 1 AND ClassificationIsManual = 0;

UPDATE dbo.ImportedTransaction
SET LegacyLinkTreatment = 1
WHERE Category = N'Transfer'
  AND SpendingClass IN (N'PayPal Funding', N'Credit Card Payment', N'Bank Transfer')
  AND ClassificationIsManual = 0
  AND LegacyLinkTreatment = 0
  AND
  (
      LOWER(Notes) LIKE N'%linked as paypal funding transfer.%'
      OR LOWER(Notes) LIKE N'%linked as credit card payment transfer.%'
      OR LOWER(Notes) LIKE N'%linked as credit card payment funding transfer.%'
      OR LOWER(Notes) LIKE N'%marked credit card payment row as transfer.%'
      OR LOWER(Notes) LIKE N'%auto-linked as bank transfer.%'
      OR LOWER(Notes) LIKE N'%auto-normalized as likely paypal funding transfer.%'
      OR LOWER(Notes) LIKE N'%auto-normalized as likely credit card payment funding transfer.%'
      OR LOWER(Notes) LIKE N'%auto-normalized by report/data-flow integrity pass as likely paypal funding transfer.%'
      OR LOWER(Notes) LIKE N'%auto-normalized by report/data-flow integrity pass as likely credit card payment funding transfer.%'
  );

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ImportRow_AcceptedTransaction')
    ALTER TABLE dbo.ImportRow ADD CONSTRAINT FK_ImportRow_AcceptedTransaction
        FOREIGN KEY (AcceptedTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportRow') AND name = N'UX_ImportRow_AcceptedTransaction')
    CREATE UNIQUE INDEX UX_ImportRow_AcceptedTransaction ON dbo.ImportRow(AcceptedTransactionId)
        WHERE AcceptedTransactionId IS NOT NULL;

IF OBJECT_ID(N'dbo.TransactionLinkRejection', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TransactionLinkRejection
    (
        SourceTransactionId bigint NOT NULL,
        LinkedTransactionId bigint NOT NULL,
        LinkType nvarchar(100) NOT NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_TransactionLinkRejection_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TransactionLinkRejection PRIMARY KEY (SourceTransactionId, LinkedTransactionId, LinkType),
        CONSTRAINT FK_TransactionLinkRejection_Source FOREIGN KEY (SourceTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId),
        CONSTRAINT FK_TransactionLinkRejection_Linked FOREIGN KEY (LinkedTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId)
    );
END;

ALTER TABLE dbo.TransactionLink ALTER COLUMN Notes nvarchar(2000) NULL;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.22.0')
BEGIN
    DECLARE @ReviewLinks TABLE
    (
        TransactionLinkId bigint PRIMARY KEY,
        SourceTransactionId bigint NOT NULL,
        LinkedTransactionId bigint NOT NULL,
        OriginalType nvarchar(100) NOT NULL
    );

    INSERT INTO @ReviewLinks (TransactionLinkId, SourceTransactionId, LinkedTransactionId, OriginalType)
    SELECT link.TransactionLinkId, link.SourceTransactionId, link.LinkedTransactionId, link.LinkType
    FROM dbo.TransactionLink AS link
    JOIN dbo.ImportedTransaction AS source ON source.ImportedTransactionId = link.SourceTransactionId
    JOIN dbo.ImportedTransaction AS linked ON linked.ImportedTransactionId = link.LinkedTransactionId
    WHERE link.LinkType <> N'ManualReview'
      AND
      (
          link.ConfidenceScore < 100
          OR LOWER(link.Notes) LIKE N'matched paypal %'
          OR LOWER(link.Notes) LIKE N'matched credit card payment:%'
          OR LOWER(link.Notes) LIKE N'fifo allocation of credit card payment%'
          OR LOWER(link.Notes) LIKE N'auto-linked as bank transfer%'
          OR source.Amount = 0 OR linked.Amount = 0
          OR link.LinkType NOT IN (N'BankTransfer', N'AccountTransfer', N'PurchaseMirror', N'PayPalFunding',
              N'CreditCardPaymentFunding', N'DebtPayment', N'CreditCardPaymentAllocation', N'PurchaseFundingAllocation', N'PurchaseRefund')
          OR (link.LinkType IN (N'BankTransfer', N'AccountTransfer', N'CreditCardPaymentFunding', N'DebtPayment')
              AND (source.AccountId = linked.AccountId OR source.Amount <> -linked.Amount))
          OR (link.LinkType IN (N'PurchaseMirror', N'PayPalFunding')
              AND (source.AccountId = linked.AccountId OR source.Amount <> linked.Amount))
          OR (link.LinkType IN (N'CreditCardPaymentAllocation', N'PurchaseRefund')
              AND (source.AccountId <> linked.AccountId OR source.Amount <= 0 OR linked.Amount >= 0))
          OR (link.LinkType = N'PurchaseFundingAllocation'
              AND (source.AccountId = linked.AccountId OR source.Amount >= 0 OR linked.Amount >= 0))
          OR (link.LinkType IN (N'CreditCardPaymentAllocation', N'PurchaseFundingAllocation', N'PurchaseRefund')
              AND (ISNULL(link.AllocationAmount, 0) <= 0
                  OR (SELECT SUM(other.AllocationAmount) FROM dbo.TransactionLink AS other
                      WHERE other.LinkType = link.LinkType AND other.SourceTransactionId = source.ImportedTransactionId) > ABS(source.Amount)
                  OR (SELECT SUM(other.AllocationAmount) FROM dbo.TransactionLink AS other
                      WHERE other.LinkType = link.LinkType AND other.LinkedTransactionId = linked.ImportedTransactionId) > ABS(linked.Amount)))
      );

    INSERT INTO dbo.TransactionLinkRejection (SourceTransactionId, LinkedTransactionId, LinkType)
    SELECT DISTINCT review.SourceTransactionId, review.LinkedTransactionId, canonical.LinkType
    FROM @ReviewLinks AS review
    CROSS APPLY (SELECT CASE review.OriginalType
        WHEN N'AccountTransfer' THEN N'BankTransfer'
        WHEN N'PayPalFunding' THEN N'PurchaseMirror'
        WHEN N'DebtPayment' THEN N'CreditCardPaymentFunding'
        ELSE review.OriginalType END AS LinkType) AS canonical
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.TransactionLinkRejection AS rejection
        WHERE rejection.SourceTransactionId = review.SourceTransactionId
          AND rejection.LinkedTransactionId = review.LinkedTransactionId
          AND rejection.LinkType = canonical.LinkType
    );

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransactionLink') AND name = N'UX_TransactionLink_Source_Linked_Type')
        DROP INDEX UX_TransactionLink_Source_Linked_Type ON dbo.TransactionLink;

    UPDATE link
    SET LinkType = N'ManualReview',
        Notes = N'Review required after upgrade: previous ' + review.OriginalType
            + N' link used unverified automatic matching or violates financial constraints. Confirm the statements and edit the relationship. Original notes: '
            + ISNULL(link.Notes, N''),
        UpdatedAtUtc = SYSUTCDATETIME()
    FROM dbo.TransactionLink AS link
    JOIN @ReviewLinks AS review ON review.TransactionLinkId = link.TransactionLinkId;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransactionLink') AND name = N'UX_TransactionLink_Source_Linked_Type')
    CREATE UNIQUE INDEX UX_TransactionLink_Source_Linked_Type
        ON dbo.TransactionLink(SourceTransactionId, LinkedTransactionId, LinkType)
        WHERE LinkType <> N'ManualReview';

IF OBJECT_ID(N'dbo.AccountReconciliation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccountReconciliation
    (
        AccountReconciliationId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccountReconciliation PRIMARY KEY,
        AccountId int NOT NULL,
        FromDate date NOT NULL,
        ToDate date NOT NULL,
        OpeningBalance decimal(18,2) NOT NULL,
        ClosingBalance decimal(18,2) NOT NULL,
        ExpectedClosingBalance decimal(18,2) NOT NULL,
        NetActivity decimal(18,2) NOT NULL,
        TransactionCount int NOT NULL,
        DateFallbackCount int NOT NULL,
        UsesLiabilityBalances bit NOT NULL,
        RecordedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AccountReconciliation_RecordedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AccountReconciliation_Account FOREIGN KEY (AccountId) REFERENCES dbo.Account(AccountId),
        CONSTRAINT CK_AccountReconciliation_Dates CHECK (FromDate <= ToDate)
    );
    CREATE INDEX IX_AccountReconciliation_Account_Date ON dbo.AccountReconciliation(AccountId, RecordedAtUtc DESC);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.22.0')
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.22.0');

COMMIT TRANSACTION;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.FinancialRule', N'StarterRuleCode') IS NULL
    ALTER TABLE dbo.FinancialRule ADD StarterRuleCode nvarchar(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FinancialRule') AND name = N'UX_FinancialRule_StarterRuleCode')
    CREATE UNIQUE INDEX UX_FinancialRule_StarterRuleCode
        ON dbo.FinancialRule(StarterRuleCode)
        WHERE StarterRuleCode IS NOT NULL;

-- Seed only once so subsequent initialization preserves edited or deleted choices.
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.23.0')
BEGIN
    DECLARE @IsEmptyDatabase bit = CASE WHEN
        NOT EXISTS (SELECT 1 FROM dbo.Account)
        AND NOT EXISTS (SELECT 1 FROM dbo.ImportedTransaction)
        AND NOT EXISTS (SELECT 1 FROM dbo.FinancialRule)
        AND NOT EXISTS (SELECT 1 FROM dbo.RecurringBill)
        THEN 1 ELSE 0 END;

    INSERT INTO dbo.AppLookup (LookupGroup, LookupCode, DisplayName, SortOrder, IsActive)
    SELECT choices.LookupGroup, choices.LookupCode, choices.DisplayName, choices.SortOrder, 1
    FROM (VALUES
        (N'Category', N'groceries', N'Groceries', 10),
        (N'Category', N'dining-out', N'Dining Out', 20),
        (N'Category', N'mortgage', N'Mortgage', 30),
        (N'Category', N'rent', N'Rent', 40),
        (N'Category', N'home-maintenance', N'Home Maintenance', 50),
        (N'Category', N'electricity', N'Electricity', 60),
        (N'Category', N'gas-heating', N'Gas & Heating', 70),
        (N'Category', N'water-sewer', N'Water & Sewer', 80),
        (N'Category', N'internet-phone', N'Internet & Phone', 90),
        (N'Category', N'fuel', N'Fuel', 100),
        (N'Category', N'public-transit', N'Public Transit', 110),
        (N'Category', N'vehicle-maintenance', N'Vehicle Maintenance', 120),
        (N'Category', N'auto-insurance', N'Auto Insurance', 130),
        (N'Category', N'home-insurance', N'Home Insurance', 140),
        (N'Category', N'health-insurance', N'Health Insurance', 150),
        (N'Category', N'medical-care', N'Medical Care', 160),
        (N'Category', N'education', N'Education', 170),
        (N'Category', N'childcare', N'Childcare', 180),
        (N'Category', N'subscriptions', N'Subscriptions', 190),
        (N'Category', N'entertainment', N'Entertainment', 200),
        (N'Category', N'travel', N'Travel', 210),
        (N'Category', N'clothing', N'Clothing', 220),
        (N'Category', N'gifts-donations', N'Gifts & Donations', 230),
        (N'Category', N'bank-fees', N'Bank Fees', 240),
        (N'Category', N'salary-wages', N'Salary & Wages', 250),
        (N'Category', N'interest-income', N'Interest Income', 260),
        (N'Category', N'internal-transfer', N'Internal Transfer', 270),
        (N'Category', N'credit-card-payment', N'Credit Card Payment', 280),
        (N'SpendingClass', N'food-drink', N'Food & Drink', 10),
        (N'SpendingClass', N'housing', N'Housing', 20),
        (N'SpendingClass', N'utilities', N'Utilities', 30),
        (N'SpendingClass', N'transportation', N'Transportation', 40),
        (N'SpendingClass', N'insurance', N'Insurance', 50),
        (N'SpendingClass', N'health', N'Health', 60),
        (N'SpendingClass', N'family-education', N'Family & Education', 70),
        (N'SpendingClass', N'lifestyle', N'Lifestyle', 80),
        (N'SpendingClass', N'travel', N'Travel', 90),
        (N'SpendingClass', N'financial-fees', N'Financial Fees', 100),
        (N'SpendingClass', N'income', N'Income', 110),
        (N'SpendingClass', N'transfers', N'Transfers', 120)
    ) AS choices(LookupGroup, LookupCode, DisplayName, SortOrder)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.AppLookup AS existing
        WHERE existing.LookupGroup = choices.LookupGroup AND existing.LookupCode = choices.LookupCode
    );

    -- Only exact debit fee descriptions are active for a new database. Existing
    -- databases receive inactive templates, leaving their classification unchanged.
    -- All starters preserve imported income, transfer, merchant, and owner values.
    INSERT INTO dbo.FinancialRule
    (
        RuleName, Priority, MatchDescription, MatchType, DescriptionMatchMode, TypeMatchMode,
        Category, SpendingClass, Merchant, IsIncome, IsTransfer, IncomeOwner, IsActive, Notes, StarterRuleCode
    )
    SELECT
        starters.RuleName, 1000000, starters.MatchDescription, starters.MatchType, N'Exact', N'Exact',
        starters.Category, starters.SpendingClass, NULL, NULL, NULL, NULL,
        CASE WHEN starters.SafeForNewDatabase = 1 AND @IsEmptyDatabase = 1 THEN 1 ELSE 0 END,
        starters.Notes, starters.StarterRuleCode
    FROM (VALUES
        (N'monthly-maintenance-fee', N'Starter: Monthly maintenance fee', N'MONTHLY MAINTENANCE FEE', N'Withdrawal', N'Bank Fees', N'Financial Fees', 1,
            N'Exact debit fee description. Customize the description for your statement. Income and transfer treatment are preserved.'),
        (N'atm-fee', N'Starter: ATM fee', N'ATM FEE', N'Withdrawal', N'Bank Fees', N'Financial Fees', 1,
            N'Matches the fee only, not an ATM cash withdrawal. Customize the exact description for your statement.'),
        (N'overdraft-fee', N'Starter: Overdraft fee', N'OVERDRAFT FEE', N'Withdrawal', N'Bank Fees', N'Financial Fees', 1,
            N'Exact debit fee description. Refunds and credits do not match this withdrawal rule.'),
        (N'wire-transfer-fee', N'Starter: Wire transfer fee', N'WIRE TRANSFER FEE', N'Withdrawal', N'Bank Fees', N'Financial Fees', 1,
            N'Matches the fee only. The transferred principal requires separate review and reconciliation.'),
        (N'groceries', N'Starter: Groceries', N'GROCERY MARKET', N'Withdrawal', N'Groceries', N'Food & Drink', 0,
            N'Inactive template. Replace the description with a grocery merchant from your imported transactions, then activate.'),
        (N'dining-out', N'Starter: Dining out', N'RESTAURANT', N'Withdrawal', N'Dining Out', N'Food & Drink', 0,
            N'Inactive template. Replace the description with a restaurant from your imported transactions, then activate.'),
        (N'mortgage', N'Starter: Mortgage', N'MORTGAGE PAYMENT', N'Withdrawal', N'Mortgage', N'Housing', 0,
            N'Inactive template. Review your lender description before activation. Use a recurring bill to group old and new servicer names as one obligation.'),
        (N'rent', N'Starter: Rent', N'RENT PAYMENT', N'Withdrawal', N'Rent', N'Housing', 0,
            N'Inactive template. Replace the description with your rent payment description before activation.'),
        (N'electricity', N'Starter: Electricity', N'ELECTRIC BILL', N'Withdrawal', N'Electricity', N'Utilities', 0,
            N'Inactive template. Replace the description with your electricity provider from an imported transaction before activation.'),
        (N'water-sewer', N'Starter: Water and sewer', N'WATER BILL', N'Withdrawal', N'Water & Sewer', N'Utilities', 0,
            N'Inactive template. Replace the description with your utility provider from an imported transaction before activation.'),
        (N'auto-insurance', N'Starter: Auto insurance', N'AUTO INSURANCE', N'Withdrawal', N'Auto Insurance', N'Insurance', 0,
            N'Inactive template. Confirm the description refers to auto coverage before activation.'),
        (N'salary-wages', N'Starter: Salary and wages', N'PAYROLL DEPOSIT', N'Deposit', N'Salary & Wages', N'Income', 0,
            N'Inactive template. Confirm the employer and set Income to Yes only for earned income. A category label alone does not mark a deposit as income.'),
        (N'interest-income', N'Starter: Interest income', N'INTEREST PAID', N'Deposit', N'Interest Income', N'Income', 0,
            N'Inactive template. Confirm this is interest received, then explicitly set Income to Yes. A category label alone does not mark a deposit as income.'),
        (N'internal-transfer', N'Starter: Internal transfer', N'INTERNAL TRANSFER', NULL, N'Internal Transfer', N'Transfers', 0,
            N'Inactive template. Confirm both accounts are yours and reconcile both sides. Set Transfer to Yes only after review; a category label alone does not exclude spending or income.')
    ) AS starters(StarterRuleCode, RuleName, MatchDescription, MatchType, Category, SpendingClass, SafeForNewDatabase, Notes)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.FinancialRule AS existing
        WHERE existing.StarterRuleCode = starters.StarterRuleCode
            OR existing.RuleName = starters.RuleName
            OR (existing.MatchDescription = starters.MatchDescription
                AND ISNULL(existing.MatchType, N'') = ISNULL(starters.MatchType, N'')
                AND existing.DescriptionMatchMode = N'Exact'
                AND existing.TypeMatchMode = N'Exact')
    );

    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.23.0');
END;

COMMIT TRANSACTION;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.RecurringBill', N'PayeeAliasesJson') IS NULL
    ALTER TABLE dbo.RecurringBill ADD PayeeAliasesJson nvarchar(max) NOT NULL
        CONSTRAINT DF_RecurringBill_PayeeAliasesJson DEFAULT N'[]' WITH VALUES;

IF COL_LENGTH(N'dbo.RecurringBill', N'SourceDescriptionAliasesJson') IS NULL
    ALTER TABLE dbo.RecurringBill ADD SourceDescriptionAliasesJson nvarchar(max) NOT NULL
        CONSTRAINT DF_RecurringBill_SourceDescriptionAliasesJson DEFAULT N'[]' WITH VALUES;

IF COL_LENGTH(N'dbo.RecurringBill', N'RowVersion') IS NULL
    ALTER TABLE dbo.RecurringBill ADD [RowVersion] rowversion NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.RecurringBill') AND name = N'CK_RecurringBill_PayeeAliasesJson')
    ALTER TABLE dbo.RecurringBill ADD CONSTRAINT CK_RecurringBill_PayeeAliasesJson
        CHECK (ISJSON(PayeeAliasesJson) = 1 AND LEFT(LTRIM(PayeeAliasesJson), 1) = N'[');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.RecurringBill') AND name = N'CK_RecurringBill_SourceDescriptionAliasesJson')
    ALTER TABLE dbo.RecurringBill ADD CONSTRAINT CK_RecurringBill_SourceDescriptionAliasesJson
        CHECK (ISJSON(SourceDescriptionAliasesJson) = 1 AND LEFT(LTRIM(SourceDescriptionAliasesJson), 1) = N'[');

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.24.0')
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.24.0');

COMMIT TRANSACTION;
