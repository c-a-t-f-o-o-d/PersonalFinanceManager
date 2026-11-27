SELECT COUNT(*) FROM dbo.ImportBatch WHERE FileSha256 = @FileSha256 AND AccountId = @FileAccountId;
