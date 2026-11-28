SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.FinancialRule', N'StarterRuleCode') IS NULL
    ALTER TABLE dbo.FinancialRule ADD StarterRuleCode nvarchar(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FinancialRule') AND name = N'UX_FinancialRule_StarterRuleCode')
    CREATE UNIQUE INDEX UX_FinancialRule_StarterRuleCode
        ON dbo.FinancialRule(StarterRuleCode)
        WHERE StarterRuleCode IS NOT NULL;

-- Seed only once so subsequent initialization preserves edited or deleted choices.
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.23.0')
BEGIN
    DECLARE @IsEmptyDatabase bit = CASE WHEN
        NOT EXISTS (SELECT 1 FROM dbo.Account)
        AND NOT EXISTS (SELECT 1 FROM dbo.ImportedTransaction)
        AND NOT EXISTS (SELECT 1 FROM dbo.FinancialRule)
        AND NOT EXISTS (SELECT 1 FROM dbo.RecurringBill)
        THEN 1 ELSE 0 END;

    INSERT INTO dbo.AppLookup (LookupGroup, LookupCode, DisplayName, SortOrder, IsActive)
    SELECT choices.LookupGroup, choices.LookupCode, choices.DisplayName, choices.SortOrder, 1
    FROM (VALUES
        (N'Category', N'groceries', N'Groceries', 10),
        (N'Category', N'dining-out', N'Dining Out', 20),
        (N'Category', N'mortgage', N'Mortgage', 30),
        (N'Category', N'rent', N'Rent', 40),
        (N'Category', N'home-maintenance', N'Home Maintenance', 50),
        (N'Category', N'electricity', N'Electricity', 60),
        (N'Category', N'gas-heating', N'Gas & Heating', 70),
        (N'Category', N'water-sewer', N'Water & Sewer', 80),
        (N'Category', N'internet-phone', N'Internet & Phone', 90),
        (N'Category', N'fuel', N'Fuel', 100),
        (N'Category', N'public-transit', N'Public Transit', 110),
        (N'Category', N'vehicle-maintenance', N'Vehicle Maintenance', 120),
        (N'Category', N'auto-insurance', N'Auto Insurance', 130),
        (N'Category', N'home-insurance', N'Home Insurance', 140),
        (N'Category', N'health-insurance', N'Health Insurance', 150),
        (N'Category', N'medical-care', N'Medical Care', 160),
        (N'Category', N'education', N'Education', 170),
        (N'Category', N'childcare', N'Childcare', 180),
        (N'Category', N'subscriptions', N'Subscriptions', 190),
        (N'Category', N'entertainment', N'Entertainment', 200),
        (N'Category', N'travel', N'Travel', 210),
        (N'Category', N'clothing', N'Clothing', 220),
        (N'Category', N'gifts-donations', N'Gifts & Donations', 230),
        (N'Category', N'bank-fees', N'Bank Fees', 240),
        (N'Category', N'salary-wages', N'Salary & Wages', 250),
        (N'Category', N'interest-income', N'Interest Income', 260),
        (N'Category', N'internal-transfer', N'Internal Transfer', 270),
        (N'Category', N'credit-card-payment', N'Credit Card Payment', 280),
        (N'SpendingClass', N'food-drink', N'Food & Drink', 10),
        (N'SpendingClass', N'housing', N'Housing', 20),
        (N'SpendingClass', N'utilities', N'Utilities', 30),
        (N'SpendingClass', N'transportation', N'Transportation', 40),
        (N'SpendingClass', N'insurance', N'Insurance', 50),
        (N'SpendingClass', N'health', N'Health', 60),
        (N'SpendingClass', N'family-education', N'Family & Education', 70),
        (N'SpendingClass', N'lifestyle', N'Lifestyle', 80),
        (N'SpendingClass', N'travel', N'Travel', 90),
        (N'SpendingClass', N'financial-fees', N'Financial Fees', 100),
        (N'SpendingClass', N'income', N'Income', 110),
        (N'SpendingClass', N'transfers', N'Transfers', 120)
    ) AS choices(LookupGroup, LookupCode, DisplayName, SortOrder)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.AppLookup AS existing
        WHERE existing.LookupGroup = choices.LookupGroup AND existing.LookupCode = choices.LookupCode
    );

    -- Only exact debit fee descriptions are active for a new database. Existing
    -- databases receive inactive templates, leaving their classification unchanged.
    -- All starters preserve imported income, transfer, merchant, and owner values.
    INSERT INTO dbo.FinancialRule
    (
        RuleName, Priority, MatchDescription, MatchType, DescriptionMatchMode, TypeMatchMode,
        Category, SpendingClass, Merchant, IsIncome, IsTransfer, IncomeOwner, IsActive, Notes, StarterRuleCode
    )
    SELECT
        starters.RuleName, 1000000, starters.MatchDescription, starters.MatchType, N'Exact', N'Exact',
        starters.Category, starters.SpendingClass, NULL, NULL, NULL, NULL,
        CASE WHEN starters.SafeForNewDatabase = 1 AND @IsEmptyDatabase = 1 THEN 1 ELSE 0 END,
        starters.Notes, starters.StarterRuleCode
    FROM (VALUES
        (N'monthly-maintenance-fee', N'Starter: Monthly maintenance fee', N'MONTHLY MAINTENANCE FEE', N'Withdrawal', N'Bank Fees', N'Financial Fees', 1,
            N'Exact debit fee description. Customize the description for your statement. Income and transfer treatment are preserved.'),
        (N'atm-fee', N'Starter: ATM fee', N'ATM FEE', N'Withdrawal', N'Bank Fees', N'Financial Fees', 1,
            N'Matches the fee only, not an ATM cash withdrawal. Customize the exact description for your statement.'),
        (N'overdraft-fee', N'Starter: Overdraft fee', N'OVERDRAFT FEE', N'Withdrawal', N'Bank Fees', N'Financial Fees', 1,
            N'Exact debit fee description. Refunds and credits do not match this withdrawal rule.'),
        (N'wire-transfer-fee', N'Starter: Wire transfer fee', N'WIRE TRANSFER FEE', N'Withdrawal', N'Bank Fees', N'Financial Fees', 1,
            N'Matches the fee only. The transferred principal requires separate review and reconciliation.'),
        (N'groceries', N'Starter: Groceries', N'GROCERY MARKET', N'Withdrawal', N'Groceries', N'Food & Drink', 0,
            N'Inactive template. Replace the description with a grocery merchant from your imported transactions, then activate.'),
        (N'dining-out', N'Starter: Dining out', N'RESTAURANT', N'Withdrawal', N'Dining Out', N'Food & Drink', 0,
            N'Inactive template. Replace the description with a restaurant from your imported transactions, then activate.'),
        (N'mortgage', N'Starter: Mortgage', N'MORTGAGE PAYMENT', N'Withdrawal', N'Mortgage', N'Housing', 0,
            N'Inactive template. Review your lender description before activation. Use a recurring bill to group old and new servicer names as one obligation.'),
        (N'rent', N'Starter: Rent', N'RENT PAYMENT', N'Withdrawal', N'Rent', N'Housing', 0,
            N'Inactive template. Replace the description with your rent payment description before activation.'),
        (N'electricity', N'Starter: Electricity', N'ELECTRIC BILL', N'Withdrawal', N'Electricity', N'Utilities', 0,
            N'Inactive template. Replace the description with your electricity provider from an imported transaction before activation.'),
        (N'water-sewer', N'Starter: Water and sewer', N'WATER BILL', N'Withdrawal', N'Water & Sewer', N'Utilities', 0,
            N'Inactive template. Replace the description with your utility provider from an imported transaction before activation.'),
        (N'auto-insurance', N'Starter: Auto insurance', N'AUTO INSURANCE', N'Withdrawal', N'Auto Insurance', N'Insurance', 0,
            N'Inactive template. Confirm the description refers to auto coverage before activation.'),
        (N'salary-wages', N'Starter: Salary and wages', N'PAYROLL DEPOSIT', N'Deposit', N'Salary & Wages', N'Income', 0,
            N'Inactive template. Confirm the employer and set Income to Yes only for earned income. A category label alone does not mark a deposit as income.'),
        (N'interest-income', N'Starter: Interest income', N'INTEREST PAID', N'Deposit', N'Interest Income', N'Income', 0,
            N'Inactive template. Confirm this is interest received, then explicitly set Income to Yes. A category label alone does not mark a deposit as income.'),
        (N'internal-transfer', N'Starter: Internal transfer', N'INTERNAL TRANSFER', NULL, N'Internal Transfer', N'Transfers', 0,
            N'Inactive template. Confirm both accounts are yours and reconcile both sides. Set Transfer to Yes only after review; a category label alone does not exclude spending or income.')
    ) AS starters(StarterRuleCode, RuleName, MatchDescription, MatchType, Category, SpendingClass, SafeForNewDatabase, Notes)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.FinancialRule AS existing
        WHERE existing.StarterRuleCode = starters.StarterRuleCode
            OR existing.RuleName = starters.RuleName
            OR (existing.MatchDescription = starters.MatchDescription
                AND ISNULL(existing.MatchType, N'') = ISNULL(starters.MatchType, N'')
                AND existing.DescriptionMatchMode = N'Exact'
                AND existing.TypeMatchMode = N'Exact')
    );

    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.23.0');
END;

COMMIT TRANSACTION;
