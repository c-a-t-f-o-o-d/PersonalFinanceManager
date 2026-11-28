namespace PersonalFinanceManager.Core.Models
{
    public sealed class Account
    {
        public int AccountId { get; set; }
        public string AccountNickname { get; set; } = string.Empty;
        public string Institution { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        public string? AccountSummaryUrl { get; set; }
        public string AccountType { get; set; } = string.Empty;
        public string AccountOwner { get; set; } = OwnerValues.Unassigned;
        public string? AccountNumber { get; set; }
        public string? AccountNumberLastFour { get; set; }
        public bool AccountNumberProtectedValueExists { get; set; }
        public bool AccountNumberCannotBeDecrypted { get; set; }
        public bool PreserveExistingAccountNumberOnSave { get; set; }
        public string? AccountNumberProtectionMessage { get; set; }
        public decimal? PrincipalBalance { get; set; }
        public decimal? AvailableBalance { get; set; }
        public decimal? CurrentBalance { get; set; }
        public DateOnly? BalanceAsOfDate { get; set; }
        public decimal? OriginalLoanAmount { get; set; }
        public decimal? CurrentInterestRate { get; set; }
        public decimal? LastPaymentAmount { get; set; }
        public decimal? NextPaymentAmount { get; set; }
        public DateOnly? NextPaymentDueDate { get; set; }
        public decimal? NextPaymentInterest { get; set; }
        public decimal? NextPaymentEscrowAmount { get; set; }
        public DateOnly? LastEscrowAnalysisDate { get; set; }
        public decimal? CurrentEscrowAccountBalance { get; set; }
        public decimal? CurrentEscrowPayment { get; set; }
        public decimal? ProjectedEscrowPayment { get; set; }
        public DateOnly? ProjectedEscrowPaymentDate { get; set; }
        public decimal? LastPaymentInterestAmount { get; set; }
        public decimal? LastPaymentEscrow { get; set; }
        public decimal? RegularMonthlyPayment { get; set; }
        public decimal? LatePaymentFee { get; set; }
        public string? EscrowDeletionFee { get; set; }
        public string? LateCharges { get; set; }
        public string? LoanAssumptionFee { get; set; }
        public string? PartialReleaseFee { get; set; }
        public string? PrincipalReductionReAmortizationFee { get; set; }
        public int? LateAfterDays { get; set; }
        public DateOnly? LastPaymentDateReceived { get; set; }
        public string? Recurrence { get; set; }
        public decimal? PaidInFullDiscount { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(AccountNickname) ? $"{Institution} {AccountType}".Trim() : AccountNickname;
    }
}
