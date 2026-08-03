UPDATE t
SET IsTransfer = 1,
    Category = N'Transfer',
    SpendingClass = @CategoryType,
    Notes = CASE
        WHEN t.Notes IS NULL OR LTRIM(RTRIM(t.Notes)) = N'' THEN @Note
        WHEN t.Notes NOT LIKE N'%' + @CategoryType + N'%' THEN t.Notes + N' ' + @Note
        ELSE t.Notes
    END,
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.TransactionLink AS link ON link.LinkedTransactionId = t.ImportedTransactionId
WHERE link.TransactionLinkId IN (/*ID_LIST*/)
  AND link.LinkType = @LinkType;
