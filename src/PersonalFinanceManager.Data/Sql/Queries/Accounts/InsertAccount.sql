INSERT INTO dbo.Account
(
    AccountNickname, Institution, BaseUrl, AccountSummaryUrl, AccountType, AccountOwner, AccountNumberProtected, AccountNumberLastFour,
    PrincipalBalance, AvailableBalance, CurrentBalance, OriginalLoanAmount, CurrentInterestRate, LastPaymentAmount,
    NextPaymentAmount, NextPaymentDueDate, NextPaymentInterest, NextPaymentEscrowAmount, LastEscrowAnalysisDate,
    CurrentEscrowAccountBalance, CurrentEscrowPayment, ProjectedEscrowPayment, ProjectedEscrowPaymentDate,
    LastPaymentInterestAmount, LastPaymentEscrow, RegularMonthlyPayment, LatePaymentFee, EscrowDeletionFee,
    LateCharges, LoanAssumptionFee, PartialReleaseFee, PrincipalReductionReAmortizationFee, LateAfterDays,
    LastPaymentDateReceived, Recurrence, PaidInFullDiscount, IsActive
)
OUTPUT INSERTED.AccountId
VALUES
(
    @AccountNickname, @Institution, @BaseUrl, @AccountSummaryUrl, @AccountType, @AccountOwner, @AccountNumberProtected, @AccountNumberLastFour,
    @PrincipalBalance, @AvailableBalance, @CurrentBalance, @OriginalLoanAmount, @CurrentInterestRate, @LastPaymentAmount,
    @NextPaymentAmount, @NextPaymentDueDate, @NextPaymentInterest, @NextPaymentEscrowAmount, @LastEscrowAnalysisDate,
    @CurrentEscrowAccountBalance, @CurrentEscrowPayment, @ProjectedEscrowPayment, @ProjectedEscrowPaymentDate,
    @LastPaymentInterestAmount, @LastPaymentEscrow, @RegularMonthlyPayment, @LatePaymentFee, @EscrowDeletionFee,
    @LateCharges, @LoanAssumptionFee, @PartialReleaseFee, @PrincipalReductionReAmortizationFee, @LateAfterDays,
    @LastPaymentDateReceived, @Recurrence, @PaidInFullDiscount, @IsActive
);
