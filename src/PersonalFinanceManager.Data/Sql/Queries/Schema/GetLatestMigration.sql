SELECT TOP (1) ScriptName
FROM dbo.SchemaMigration
ORDER BY AppliedAtUtc DESC, SchemaMigrationId DESC;
