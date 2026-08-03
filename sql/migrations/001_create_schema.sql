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
