SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.Account)
    OR EXISTS (SELECT 1 FROM dbo.ImportedTransaction)
    OR EXISTS (SELECT 1 FROM dbo.FinancialRule)
    OR EXISTS (SELECT 1 FROM dbo.RecurringBill)
BEGIN
    THROW 51001, 'Demo data can only be loaded into an empty PersonalFinanceManager database.', 1;
END;

DECLARE @Today date = CONVERT(date, GETDATE());
DECLARE @ThisMonth date = DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1);
DECLARE @PreviousMonth date = DATEADD(month, -1, @ThisMonth);
DECLARE @TwoMonthsAgo date = DATEADD(month, -2, @ThisMonth);

BEGIN TRANSACTION;

INSERT INTO dbo.Account
(
    AccountNickname,
    Institution,
    BaseUrl,
    AccountSummaryUrl,
    AccountType,
    AccountOwner,
    AccountNumberProtected,
    AccountNumberLastFour,
    PrincipalBalance,
    AvailableBalance,
    CurrentBalance,
    OriginalLoanAmount,
    CurrentInterestRate,
    LastPaymentAmount,
    NextPaymentAmount,
    NextPaymentDueDate,
    RegularMonthlyPayment,
    Recurrence,
    IsActive
)
VALUES
(N'Demo Checking', N'Demo Bank', N'https://example.invalid', NULL, N'Checking', N'Household', NULL, N'0001', NULL, 4250.80, 4250.80, NULL, 0.00000, NULL, NULL, NULL, NULL, NULL, 1),
(N'Demo Savings', N'Demo Bank', N'https://example.invalid', NULL, N'Savings', N'Household', NULL, N'0002', NULL, 12500.00, 12500.00, NULL, 3.25000, NULL, NULL, NULL, NULL, NULL, 1),
(N'Demo Rewards Card', N'Demo Credit', N'https://example.invalid', NULL, N'Credit Card', N'Joint', NULL, N'1000', 845.30, NULL, 845.30, NULL, 22.24000, 410.00, 35.00, DATEADD(day, 18, @Today), 35.00, N'Monthly', 1),
(N'Demo Mortgage', N'Demo Mortgage Co', N'https://example.invalid', NULL, N'Mortgage', N'Joint', NULL, N'2000', 208740.02, NULL, 208740.02, 270200.00, 3.50000, 1600.00, 1600.00, DATEADD(day, 12, @Today), 1600.00, N'Monthly', 1);

DECLARE @CheckingId int = (SELECT AccountId FROM dbo.Account WHERE AccountNickname = N'Demo Checking');
DECLARE @SavingsId int = (SELECT AccountId FROM dbo.Account WHERE AccountNickname = N'Demo Savings');
DECLARE @CardId int = (SELECT AccountId FROM dbo.Account WHERE AccountNickname = N'Demo Rewards Card');
DECLARE @MortgageId int = (SELECT AccountId FROM dbo.Account WHERE AccountNickname = N'Demo Mortgage');

UPDATE dbo.Account SET BalanceAsOfDate = @Today;

INSERT INTO dbo.FinancialRule
(
    RuleName,
    Priority,
    MatchDescription,
    MatchType,
    DescriptionMatchMode,
    TypeMatchMode,
    Category,
    SpendingClass,
    Merchant,
    IsIncome,
    IsTransfer,
    IsActive,
    Notes,
    IncomeOwner
)
VALUES
(N'Demo Payroll Deposit', 100, N'DEMO PAYROLL', N'Deposit', N'StartsWith', N'Exact', N'Payroll', N'Payroll', NULL, 1, 0, 1, N'Safe demo rule.', N'Household'),
(N'Demo Groceries', 200, N'DEMO GROCERY', N'Withdrawal', N'StartsWith', N'Exact', N'Dining', N'Groceries', NULL, 0, 0, 1, N'Safe demo rule.', NULL),
(N'Demo Restaurant', 210, N'DEMO RESTAURANT', N'Withdrawal', N'StartsWith', N'Exact', N'Dining', N'Restaurant', NULL, 0, 0, 1, N'Safe demo rule.', NULL),
(N'Demo Utilities', 220, N'DEMO ELECTRIC', N'Withdrawal', N'StartsWith', N'Exact', N'Utilities', N'Electricity', NULL, 0, 0, 1, N'Safe demo rule.', NULL),
(N'Demo Card Payment', 230, N'DEMO CREDIT CARD PAYMENT', N'Withdrawal', N'StartsWith', N'Exact', N'Transfer', N'Credit Card Payment', NULL, 0, 1, 1, N'Safe demo rule.', NULL),
(N'Demo Interest', 240, N'DEMO INTEREST', N'Deposit', N'StartsWith', N'Exact', N'Interest', N'Yield', NULL, 1, 0, 1, N'Safe demo rule.', NULL);

DECLARE @PayrollRuleId int = (SELECT RuleId FROM dbo.FinancialRule WHERE RuleName = N'Demo Payroll Deposit');
DECLARE @GroceriesRuleId int = (SELECT RuleId FROM dbo.FinancialRule WHERE RuleName = N'Demo Groceries');
DECLARE @RestaurantRuleId int = (SELECT RuleId FROM dbo.FinancialRule WHERE RuleName = N'Demo Restaurant');
DECLARE @UtilityRuleId int = (SELECT RuleId FROM dbo.FinancialRule WHERE RuleName = N'Demo Utilities');
DECLARE @CardPaymentRuleId int = (SELECT RuleId FROM dbo.FinancialRule WHERE RuleName = N'Demo Card Payment');
DECLARE @InterestRuleId int = (SELECT RuleId FROM dbo.FinancialRule WHERE RuleName = N'Demo Interest');

DECLARE @DemoTransactions TABLE
(
    AccountId int NOT NULL,
    TransactionDate date NOT NULL,
    Amount decimal(18,2) NOT NULL,
    [Type] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    AppliedRuleId int NULL,
    Category nvarchar(200) NULL,
    SpendingClass nvarchar(100) NULL,
    Merchant nvarchar(200) NULL,
    IsIncome bit NOT NULL,
    IsTransfer bit NOT NULL,
    IsManual bit NOT NULL,
    Notes nvarchar(1000) NULL,
    IncomeOwner nvarchar(50) NULL
);

INSERT INTO @DemoTransactions
VALUES
(@CheckingId, DATEADD(day, 1, @TwoMonthsAgo), 4200.00, N'Deposit', N'DEMO PAYROLL ACME COMPANY', @PayrollRuleId, N'Payroll', N'Payroll', N'ACME Company', 1, 0, 0, N'Demo income row.', N'Household'),
(@CheckingId, DATEADD(day, 15, @TwoMonthsAgo), 4200.00, N'Deposit', N'DEMO PAYROLL ACME COMPANY', @PayrollRuleId, N'Payroll', N'Payroll', N'ACME Company', 1, 0, 0, N'Demo income row.', N'Household'),
(@CheckingId, DATEADD(day, 3, @TwoMonthsAgo), -188.42, N'Withdrawal', N'DEMO GROCERY MARKET', @GroceriesRuleId, N'Dining', N'Groceries', N'Demo Grocery Market', 0, 0, 0, N'Demo grocery expense.', NULL),
(@CheckingId, DATEADD(day, 7, @TwoMonthsAgo), -66.18, N'Withdrawal', N'DEMO RESTAURANT', @RestaurantRuleId, N'Dining', N'Restaurant', N'Demo Restaurant', 0, 0, 0, N'Demo dining expense.', NULL),
(@CheckingId, DATEADD(day, 12, @TwoMonthsAgo), -142.55, N'Withdrawal', N'DEMO ELECTRIC COMPANY', @UtilityRuleId, N'Utilities', N'Electricity', N'Demo Electric Company', 0, 0, 0, N'Demo utility expense.', NULL),
(@CheckingId, DATEADD(day, 20, @TwoMonthsAgo), -1600.00, N'Withdrawal', N'DEMO MORTGAGE PAYMENT', NULL, N'Loan Payment', N'Mortgage', N'Demo Mortgage Co', 0, 0, 0, N'Demo required bill.', NULL),
(@CheckingId, DATEADD(day, 24, @TwoMonthsAgo), -410.00, N'Withdrawal', N'DEMO CREDIT CARD PAYMENT', @CardPaymentRuleId, N'Transfer', N'Credit Card Payment', N'Demo Credit', 0, 1, 0, N'Demo funding transfer.', NULL),
(@CardId, DATEADD(day, 24, @TwoMonthsAgo), 410.00, N'Payment', N'DEMO CREDIT CARD PAYMENT RECEIVED', @CardPaymentRuleId, N'Transfer', N'Credit Card Payment', N'Demo Credit', 0, 1, 0, N'Demo card payment received.', NULL),
(@SavingsId, DATEADD(day, 28, @TwoMonthsAgo), 33.94, N'Deposit', N'DEMO INTEREST PAID', @InterestRuleId, N'Interest', N'Yield', N'Demo Bank', 1, 0, 0, N'Demo interest row.', N'Household'),

(@CheckingId, DATEADD(day, 1, @PreviousMonth), 4200.00, N'Deposit', N'DEMO PAYROLL ACME COMPANY', @PayrollRuleId, N'Payroll', N'Payroll', N'ACME Company', 1, 0, 0, N'Demo income row.', N'Household'),
(@CheckingId, DATEADD(day, 15, @PreviousMonth), 4200.00, N'Deposit', N'DEMO PAYROLL ACME COMPANY', @PayrollRuleId, N'Payroll', N'Payroll', N'ACME Company', 1, 0, 0, N'Demo income row.', N'Household'),
(@CheckingId, DATEADD(day, 4, @PreviousMonth), -212.33, N'Withdrawal', N'DEMO GROCERY MARKET', @GroceriesRuleId, N'Dining', N'Groceries', N'Demo Grocery Market', 0, 0, 0, N'Demo grocery expense.', NULL),
(@CheckingId, DATEADD(day, 9, @PreviousMonth), -83.90, N'Withdrawal', N'DEMO RESTAURANT', @RestaurantRuleId, N'Dining', N'Restaurant', N'Demo Restaurant', 0, 0, 0, N'Demo dining expense.', NULL),
(@CheckingId, DATEADD(day, 12, @PreviousMonth), -151.02, N'Withdrawal', N'DEMO ELECTRIC COMPANY', @UtilityRuleId, N'Utilities', N'Electricity', N'Demo Electric Company', 0, 0, 0, N'Demo utility expense.', NULL),
(@CheckingId, DATEADD(day, 20, @PreviousMonth), -1600.00, N'Withdrawal', N'DEMO MORTGAGE PAYMENT', NULL, N'Loan Payment', N'Mortgage', N'Demo Mortgage Co', 0, 0, 0, N'Demo required bill.', NULL),
(@CheckingId, DATEADD(day, 23, @PreviousMonth), -390.00, N'Withdrawal', N'DEMO CREDIT CARD PAYMENT', @CardPaymentRuleId, N'Transfer', N'Credit Card Payment', N'Demo Credit', 0, 1, 0, N'Demo funding transfer.', NULL),
(@CardId, DATEADD(day, 23, @PreviousMonth), 390.00, N'Payment', N'DEMO CREDIT CARD PAYMENT RECEIVED', @CardPaymentRuleId, N'Transfer', N'Credit Card Payment', N'Demo Credit', 0, 1, 0, N'Demo card payment received.', NULL),
(@SavingsId, DATEADD(day, 28, @PreviousMonth), 34.15, N'Deposit', N'DEMO INTEREST PAID', @InterestRuleId, N'Interest', N'Yield', N'Demo Bank', 1, 0, 0, N'Demo interest row.', N'Household'),

(@CheckingId, DATEADD(day, 1, @ThisMonth), 4200.00, N'Deposit', N'DEMO PAYROLL ACME COMPANY', @PayrollRuleId, N'Payroll', N'Payroll', N'ACME Company', 1, 0, 0, N'Demo income row.', N'Household'),
(@CheckingId, DATEADD(day, 8, @ThisMonth), -196.21, N'Withdrawal', N'DEMO GROCERY MARKET', @GroceriesRuleId, N'Dining', N'Groceries', N'Demo Grocery Market', 0, 0, 0, N'Demo grocery expense.', NULL),
(@CheckingId, DATEADD(day, 10, @ThisMonth), -72.44, N'Withdrawal', N'DEMO RESTAURANT', @RestaurantRuleId, N'Dining', N'Restaurant', N'Demo Restaurant', 0, 0, 0, N'Demo dining expense.', NULL),
(@CheckingId, DATEADD(day, 12, @ThisMonth), -148.77, N'Withdrawal', N'DEMO ELECTRIC COMPANY', @UtilityRuleId, N'Utilities', N'Electricity', N'Demo Electric Company', 0, 0, 0, N'Demo utility expense.', NULL),
(@CheckingId, DATEADD(day, 20, @ThisMonth), -1600.00, N'Withdrawal', N'DEMO MORTGAGE PAYMENT', NULL, N'Loan Payment', N'Mortgage', N'Demo Mortgage Co', 0, 0, 0, N'Demo required bill.', NULL);

INSERT INTO dbo.ImportedTransaction
(
    AccountId,
    ImportBatchId,
    TransactionDate,
    PostingDate,
    TransactionTime,
    Amount,
    [Type],
    [Description],
    AppliedRuleId,
    Category,
    SpendingClass,
    Merchant,
    IsIncome,
    IsTransfer,
    IsManual,
    Notes,
    RawRowHash,
    ExternalTransactionId,
    FundingSource,
    SourceCategory,
    Memo,
    IncomeOwner,
    UpdatedAtUtc
)
SELECT
    AccountId,
    NULL,
    TransactionDate,
    TransactionDate,
    NULL,
    Amount,
    [Type],
    [Description],
    AppliedRuleId,
    Category,
    SpendingClass,
    Merchant,
    IsIncome,
    IsTransfer,
    IsManual,
    Notes,
    CONVERT(char(64), HASHBYTES('SHA2_256', CONCAT(N'DEMO|', AccountId, N'|', TransactionDate, N'|', Amount, N'|', [Description])), 2),
    CONCAT(N'DEMO:', AccountId, N':', CONVERT(nvarchar(20), TransactionDate, 112), N':', ABS(CHECKSUM([Description], Amount))),
    NULL,
    N'Demo',
    Notes,
    IncomeOwner,
    SYSUTCDATETIME()
FROM @DemoTransactions;

INSERT INTO dbo.TransactionLink
(
    SourceTransactionId,
    LinkedTransactionId,
    LinkType,
    ConfidenceScore,
    Notes
)
SELECT card.ImportedTransactionId,
       checking.ImportedTransactionId,
       N'CreditCardPaymentFunding',
       100,
       N'Demo credit card payment funding link.'
FROM dbo.ImportedTransaction AS checking
INNER JOIN dbo.ImportedTransaction AS card
    ON card.TransactionDate = checking.TransactionDate
   AND card.Amount = -checking.Amount
   AND card.AccountId = @CardId
WHERE checking.AccountId = @CheckingId
  AND checking.[Description] = N'DEMO CREDIT CARD PAYMENT';

INSERT INTO dbo.RecurringBill
(
    AccountId,
    BillName,
    Payee,
    Category,
    SpendingClass,
    RequirementType,
    TypicalAmount,
    DueDay,
    NextDueDate,
    Frequency,
    IsRequired,
    IsActive,
    SourceDescription,
    FirstSeenDate,
    LastSeenDate,
    TransactionCount,
    Notes
)
VALUES
(@CheckingId, N'Demo Mortgage', N'Demo Mortgage Co', N'Loan Payment', N'Mortgage', N'Need', 1600.00, 20, DATEADD(day, 20, @ThisMonth), N'Monthly', 1, 1, N'DEMO MORTGAGE PAYMENT', DATEADD(day, 20, @TwoMonthsAgo), DATEADD(day, 20, @ThisMonth), 3, N'Demo required bill.'),
(@CheckingId, N'Demo Electric', N'Demo Electric Company', N'Utilities', N'Electricity', N'Need', 150.00, 12, DATEADD(day, 12, @ThisMonth), N'Monthly', 1, 1, N'DEMO ELECTRIC COMPANY', DATEADD(day, 12, @TwoMonthsAgo), DATEADD(day, 12, @ThisMonth), 3, N'Demo utility bill.'),
(@CheckingId, N'Demo Streaming', N'Demo Streaming', N'Subscriptions', N'Entertainment', N'Want', 18.99, 5, DATEADD(day, 5, @ThisMonth), N'Monthly', 0, 1, N'DEMO STREAMING', NULL, NULL, 0, N'Demo optional subscription.'),
(@CardId, N'Demo Credit Card Minimum', N'Demo Credit', N'Transfer', N'Credit Card Payment', N'Debt', 35.00, 18, DATEADD(day, 18, @ThisMonth), N'Monthly', 1, 1, N'DEMO CREDIT CARD PAYMENT', NULL, NULL, 0, N'Demo card minimum payment.');

COMMIT TRANSACTION;
