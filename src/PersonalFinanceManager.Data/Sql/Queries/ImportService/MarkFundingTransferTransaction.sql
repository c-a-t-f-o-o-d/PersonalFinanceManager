UPDATE dbo.ImportedTransaction
SET IsTransfer = 1,
    Category = N'Transfer',
    SpendingClass = @CategoryType,
    Notes = CASE
        WHEN Notes IS NULL OR LTRIM(RTRIM(Notes)) = N'' THEN @NoteText
        WHEN Notes NOT LIKE N'%' + @CategoryType + N'%' THEN Notes + N' ' + @NoteText
        ELSE Notes
    END,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE ImportedTransactionId = @ImportedTransactionId;
