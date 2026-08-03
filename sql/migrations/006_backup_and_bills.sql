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
