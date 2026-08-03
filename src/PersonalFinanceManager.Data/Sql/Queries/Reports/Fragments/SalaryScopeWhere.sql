ISNULL(NULLIF(LTRIM(RTRIM(/*ALIAS*/.Category)), N''), N'') NOT IN
(
    N'Ignore',
    N'Transfer',
    N'Payroll',
    N'Cashback',
    N'Interest',
    N'Paypal',
    N'Debt (Credit Card)',
    N'Credit Card Payment',
    N'Loan Payment',
    N'Refund'
)
AND ISNULL(NULLIF(LTRIM(RTRIM(/*ALIAS*/.SpendingClass)), N''), N'') NOT IN
(
    N'Ignore',
    N'Transfer',
    N'Payment',
    N'PayPal Funding',
    N'Credit Card Payment',
    N'Checking -> Savings',
    N'Savings -> Checking',
    N'Savings -> External',
    N'External -> Savings',
    N'Checking -> Retirement',
    N'Venmo -> Checking',
    N'Auto Loan Payment',
    N'Mortgage',
    N'Payroll',
    N'Unemployment',
    N'Yield'
)
