UPDATE t
SET AppliedRuleId = @RuleId,
    Category = @Category,
    SpendingClass = @SpendingClass,
    Merchant = CASE WHEN @Merchant IS NULL THEN t.Merchant ELSE @Merchant END,
    IsIncome = COALESCE(@IsIncome, t.IsIncome),
    IsTransfer = COALESCE(@IsTransfer, t.IsTransfer),
    IncomeOwner = COALESCE(@IncomeOwner, t.IncomeOwner),
    LegacyLinkTreatment = 0,
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.ImportedTransaction AS t
WHERE /*DESCRIPTION_CLAUSE*//*TYPE_CLAUSE*/
  AND t.ClassificationIsManual = 0;
