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
