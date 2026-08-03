UPDATE t
SET IsTransfer = 1,
    Category = N'Transfer',
    SpendingClass = N'Credit Card Payment',
    Notes = CASE
        WHEN t.Notes IS NULL OR LTRIM(RTRIM(t.Notes)) = N'' THEN N'Auto-linked as credit card payment transfer.'
        WHEN t.Notes NOT LIKE N'%credit card payment%' THEN t.Notes + N' Auto-linked as credit card payment transfer.'
        ELSE t.Notes
    END,
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.ImportedTransaction AS t
WHERE
(
    EXISTS
    (
        SELECT 1
        FROM dbo.TransactionLink AS l
        WHERE l.LinkType = N'CreditCardPaymentFunding'
          AND (l.SourceTransactionId = t.ImportedTransactionId OR l.LinkedTransactionId = t.ImportedTransactionId)
    )
    OR
    (
        t.Amount > 0
        AND UPPER(LTRIM(RTRIM(t.[Type]))) = N'PAYMENT'
        AND EXISTS
        (
            SELECT 1
            FROM dbo.Account AS a
            WHERE a.AccountId = t.AccountId
              AND UPPER(a.AccountType) LIKE N'%CREDIT%'
        )
    )
)
AND
(
    t.IsTransfer = 0
    OR ISNULL(t.Category, N'') <> N'Transfer'
    OR ISNULL(t.SpendingClass, N'') <> N'Credit Card Payment'
);
