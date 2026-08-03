UPDATE t
SET IsTransfer = 1,
    Category = N'Transfer',
    SpendingClass = N'PayPal Funding',
    Notes = CASE
        WHEN t.Notes IS NULL OR LTRIM(RTRIM(t.Notes)) = N'' THEN N'Auto-linked as PayPal funding transfer.'
        WHEN t.Notes NOT LIKE N'%PayPal funding%' THEN t.Notes + N' Auto-linked as PayPal funding transfer.'
        ELSE t.Notes
    END,
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.TransactionLink AS l ON l.LinkedTransactionId = t.ImportedTransactionId
WHERE l.LinkType = N'PayPalFunding'
  AND
  (
      t.IsTransfer = 0
      OR ISNULL(t.Category, N'') <> N'Transfer'
      OR ISNULL(t.SpendingClass, N'') <> N'PayPal Funding'
  );
