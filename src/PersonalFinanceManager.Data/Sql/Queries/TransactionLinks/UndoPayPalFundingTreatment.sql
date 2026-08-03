UPDATE t
SET IsTransfer = 0,
    Category = N'Paypal',
    SpendingClass = N'Paypal',
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.TransactionLink AS link ON link.LinkedTransactionId = t.ImportedTransactionId
WHERE link.TransactionLinkId IN (/*ID_LIST*/)
  AND link.LinkType = N'PayPalFunding';
