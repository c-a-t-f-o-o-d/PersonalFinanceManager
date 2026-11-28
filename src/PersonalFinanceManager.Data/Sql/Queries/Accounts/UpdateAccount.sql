IF EXISTS
(
    SELECT 1 FROM dbo.Account WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountId = @AccountId AND AccountType <> @AccountType
)
AND EXISTS
(
    SELECT 1 FROM dbo.ImportedTransaction AS t
    JOIN dbo.TransactionLink AS link ON link.SourceTransactionId = t.ImportedTransactionId OR link.LinkedTransactionId = t.ImportedTransactionId
    WHERE t.AccountId = @AccountId AND link.LinkType <> N'ManualReview'
)
    THROW 50003, 'Remove financial links for this account before changing its account type, then reconcile them using the new type.', 1;

UPDATE dbo.Account
SET
    AccountNickname = @AccountNickname,
    Institution = @Institution,
    BaseUrl = @BaseUrl,
    AccountSummaryUrl = @AccountSummaryUrl,
    AccountType = @AccountType,
    AccountOwner = @AccountOwner,
    AccountNumberProtected = CASE WHEN @PreserveExistingAccountNumber = 1 THEN AccountNumberProtected ELSE @AccountNumberProtected END,
    AccountNumberLastFour = CASE WHEN @PreserveExistingAccountNumber = 1 THEN AccountNumberLastFour ELSE @AccountNumberLastFour END,
    PrincipalBalance = @PrincipalBalance,
    AvailableBalance = @AvailableBalance,
    CurrentBalance = @CurrentBalance,
    BalanceAsOfDate = @BalanceAsOfDate,
    OriginalLoanAmount = @OriginalLoanAmount,
    CurrentInterestRate = @CurrentInterestRate,
    LastPaymentAmount = @LastPaymentAmount,
    NextPaymentAmount = @NextPaymentAmount,
    NextPaymentDueDate = @NextPaymentDueDate,
    NextPaymentInterest = @NextPaymentInterest,
    NextPaymentEscrowAmount = @NextPaymentEscrowAmount,
    LastEscrowAnalysisDate = @LastEscrowAnalysisDate,
    CurrentEscrowAccountBalance = @CurrentEscrowAccountBalance,
    CurrentEscrowPayment = @CurrentEscrowPayment,
    ProjectedEscrowPayment = @ProjectedEscrowPayment,
    ProjectedEscrowPaymentDate = @ProjectedEscrowPaymentDate,
    LastPaymentInterestAmount = @LastPaymentInterestAmount,
    LastPaymentEscrow = @LastPaymentEscrow,
    RegularMonthlyPayment = @RegularMonthlyPayment,
    LatePaymentFee = @LatePaymentFee,
    EscrowDeletionFee = @EscrowDeletionFee,
    LateCharges = @LateCharges,
    LoanAssumptionFee = @LoanAssumptionFee,
    PartialReleaseFee = @PartialReleaseFee,
    PrincipalReductionReAmortizationFee = @PrincipalReductionReAmortizationFee,
    LateAfterDays = @LateAfterDays,
    LastPaymentDateReceived = @LastPaymentDateReceived,
    Recurrence = @Recurrence,
    PaidInFullDiscount = @PaidInFullDiscount,
    IsActive = @IsActive,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE AccountId = @AccountId;
