SELECT TOP (100)
    DatabaseBackupHistoryId,
    BackupType,
    FilePath,
    CreatedAtUtc,
    Succeeded,
    Verified,
    ErrorMessage
FROM dbo.DatabaseBackupHistory
ORDER BY CreatedAtUtc DESC, DatabaseBackupHistoryId DESC;
