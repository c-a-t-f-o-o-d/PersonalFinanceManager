UPDATE dbo.FinancialRule
SET RuleName = @RuleName,
    Priority = @Priority,
    MatchDescription = @MatchDescription,
    MatchType = @MatchType,
    DescriptionMatchMode = @DescriptionMatchMode,
    TypeMatchMode = @TypeMatchMode,
    Category = @Category,
    SpendingClass = @SpendingClass,
    Merchant = @Merchant,
    IsIncome = @IsIncome,
    IsTransfer = @IsTransfer,
    IncomeOwner = @IncomeOwner,
    IsActive = @IsActive,
    Notes = @Notes,
    StarterRuleCode = NULL,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE RuleId = @RuleId;
