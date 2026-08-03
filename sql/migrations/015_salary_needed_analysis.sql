SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.12.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.12.0');
END;
