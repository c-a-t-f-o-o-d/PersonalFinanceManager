UPDATE t
SET IsTransfer = 1,
    Category = N'Transfer',
    SpendingClass = N'Bank Transfer',
    Notes = CASE
        WHEN t.Notes IS NULL OR LTRIM(RTRIM(t.Notes)) = N'' THEN N'Auto-linked as bank transfer.'
        WHEN t.Notes NOT LIKE N'%bank transfer%' THEN t.Notes + N' Auto-linked as bank transfer.'
        ELSE t.Notes
    END,
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.TransactionLink AS l ON l.SourceTransactionId = t.ImportedTransactionId OR l.LinkedTransactionId = t.ImportedTransactionId
WHERE l.LinkType = N'BankTransfer'
  AND
  (
      t.IsTransfer = 0
      OR ISNULL(t.Category, N'') <> N'Transfer'
      OR ISNULL(t.SpendingClass, N'') <> N'Bank Transfer'
  );
