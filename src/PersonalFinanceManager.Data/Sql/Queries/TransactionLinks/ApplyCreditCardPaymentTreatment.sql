UPDATE t
SET IsTransfer = 1,
    Category = N'Transfer',
    SpendingClass = N'Credit Card Payment',
    Notes = CASE
        WHEN t.Notes IS NULL OR LTRIM(RTRIM(t.Notes)) = N'' THEN N'Linked as credit card payment transfer.'
        WHEN t.Notes NOT LIKE N'%credit card payment%' THEN t.Notes + N' Linked as credit card payment transfer.'
        ELSE t.Notes
    END,
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.TransactionLink AS link ON link.SourceTransactionId = t.ImportedTransactionId OR link.LinkedTransactionId = t.ImportedTransactionId
WHERE link.TransactionLinkId IN (/*ID_LIST*/)
  AND link.LinkType = N'CreditCardPaymentFunding';
