IF NULLIF(@DatabaseName, N'') IS NULL OR DB_ID(@DatabaseName) IS NULL
    THROW 50001, 'The configured database does not exist.', 1;

DECLARE @BackupSql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(@DatabaseName)
    + N' TO DISK = @Path WITH COPY_ONLY, INIT, CHECKSUM, NAME = @BackupName;';
DECLARE @BackupName nvarchar(128) = LEFT(@DatabaseName + N' Full Database Backup', 128);
EXEC sys.sp_executesql @BackupSql,
    N'@Path nvarchar(1000), @BackupName nvarchar(128)',
    @Path = @FilePath, @BackupName = @BackupName;

RESTORE VERIFYONLY FROM DISK = @FilePath WITH CHECKSUM;
