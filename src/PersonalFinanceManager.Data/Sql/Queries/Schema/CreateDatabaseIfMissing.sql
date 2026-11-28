IF NULLIF(@DatabaseName, N'') IS NULL OR LEN(@DatabaseName) > 128
    THROW 50001, 'A valid configured database name is required.', 1;

IF DB_ID(@DatabaseName) IS NULL
BEGIN
    DECLARE @CreateSql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName) + N';';
    EXEC sys.sp_executesql @CreateSql;
END;
