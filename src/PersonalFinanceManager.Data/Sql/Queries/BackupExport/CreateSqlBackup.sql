DECLARE @BackupPath nvarchar(1000) = @FilePath;
BACKUP DATABASE [PersonalFinanceManager]
TO DISK = @BackupPath
WITH COPY_ONLY, INIT, CHECKSUM, NAME = N'PersonalFinanceManager Full Database Backup';

RESTORE VERIFYONLY
FROM DISK = @BackupPath
WITH CHECKSUM;
