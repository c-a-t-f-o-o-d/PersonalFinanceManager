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
