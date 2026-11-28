UPDATE dbo.ImportedTransaction
SET AppliedRuleId = @AppliedRuleId,
    Category = @Category,
    SpendingClass = @SpendingClass,
    Merchant = @Merchant,
    IsIncome = @IsIncome,
    IsTransfer = @IsTransfer,
    IncomeOwner = CASE WHEN @IncomeOwner IS NULL THEN IncomeOwner ELSE @IncomeOwner END,
    LegacyLinkTreatment = 0,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE ImportedTransactionId = @ImportedTransactionId
  AND ClassificationIsManual = 0;
