SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- User-specific categorization rules are intentionally not seeded in the public repository.
-- Import or create rules from the application UI so private merchant/payee names are not committed.
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.0.2-user-rules')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.0.2-user-rules');
END;
