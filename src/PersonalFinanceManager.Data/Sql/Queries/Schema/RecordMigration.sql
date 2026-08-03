IF EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = @ScriptName)
BEGIN
    UPDATE dbo.SchemaMigration
    SET SchemaVersion = @SchemaVersion,
        ScriptHash = @ScriptHash,
        AppliedAtUtc = SYSUTCDATETIME(),
        [Status] = @Status,
        [Message] = @Message
    WHERE ScriptName = @ScriptName;
END
ELSE
BEGIN
    INSERT INTO dbo.SchemaMigration (ScriptName, SchemaVersion, ScriptHash, [Status], [Message])
    VALUES (@ScriptName, @SchemaVersion, @ScriptHash, @Status, @Message);
END;
