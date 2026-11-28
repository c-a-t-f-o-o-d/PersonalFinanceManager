SELECT AccountId, AccountNickname, Institution, BaseUrl, AccountSummaryUrl, AccountType,
       AccountNumberLastFour, PrincipalBalance, AvailableBalance, CurrentBalance, BalanceAsOfDate, OriginalLoanAmount,
       CurrentInterestRate, LastPaymentAmount, NextPaymentAmount, NextPaymentDueDate,
       NextPaymentInterest, NextPaymentEscrowAmount, LastEscrowAnalysisDate,
       CurrentEscrowAccountBalance, CurrentEscrowPayment, ProjectedEscrowPayment,
       ProjectedEscrowPaymentDate, LastPaymentInterestAmount, LastPaymentEscrow,
       RegularMonthlyPayment, LatePaymentFee, EscrowDeletionFee, LateCharges,
       LoanAssumptionFee, PartialReleaseFee, PrincipalReductionReAmortizationFee,
       LateAfterDays, LastPaymentDateReceived, Recurrence, PaidInFullDiscount,
       IsActive, CreatedAtUtc, UpdatedAtUtc
FROM dbo.Account
ORDER BY Institution, AccountNickname;
