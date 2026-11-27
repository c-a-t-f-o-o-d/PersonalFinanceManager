UPDATE dbo.ImportedTransaction
SET Category = COALESCE(@Category, Category),
    SpendingClass = COALESCE(@SpendingClass, SpendingClass),
    ClassificationIsManual = 1,
    IsTransfer = CASE WHEN LegacyLinkTreatment = 1 THEN 0 ELSE IsTransfer END,
    LegacyLinkTreatment = 0,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE ImportedTransactionId IN (/*ID_LIST*/);
