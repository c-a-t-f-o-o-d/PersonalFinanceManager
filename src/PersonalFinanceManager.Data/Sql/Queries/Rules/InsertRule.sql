INSERT INTO dbo.FinancialRule
    (RuleName, Priority, MatchDescription, MatchType, DescriptionMatchMode, TypeMatchMode, Category, SpendingClass, Merchant, IsIncome, IsTransfer, IncomeOwner, IsActive, Notes)
OUTPUT INSERTED.RuleId
VALUES
    (@RuleName, @Priority, @MatchDescription, @MatchType, @DescriptionMatchMode, @TypeMatchMode, @Category, @SpendingClass, @Merchant, @IsIncome, @IsTransfer, @IncomeOwner, @IsActive, @Notes);
