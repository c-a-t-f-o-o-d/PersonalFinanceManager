INSERT INTO dbo.ImportBatch (AccountId, ImportProfileCode, OriginalFileName, FileSha256)
OUTPUT INSERTED.ImportBatchId
VALUES (@AccountId, @ImportProfileCode, @OriginalFileName, @FileSha256);
