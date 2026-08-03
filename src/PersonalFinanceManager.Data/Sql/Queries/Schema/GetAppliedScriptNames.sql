SELECT ScriptName
FROM dbo.SchemaMigration
WHERE [Status] IN (N'Executed', N'AlreadyApplied', N'ObsoleteSkipped');
