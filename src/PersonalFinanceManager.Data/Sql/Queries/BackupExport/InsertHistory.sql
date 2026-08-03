INSERT INTO dbo.DatabaseBackupHistory (BackupType, FilePath, Succeeded, Verified, ErrorMessage)
VALUES (@BackupType, @FilePath, @Succeeded, @Verified, @ErrorMessage);
