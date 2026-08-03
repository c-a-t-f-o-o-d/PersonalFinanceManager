namespace PersonalFinanceManager.Core.Salary
{
    public sealed class SalaryRequirementResult
    {
        public decimal RequiredGrossAnnualSalary { get; set; }
        public decimal RequiredGrossMonthlySalary { get; set; }
        public decimal RequiredGrossPerPayPeriod { get; set; }
        public decimal SpouseAnnualGrossIncome { get; set; }
        public decimal SpouseMonthlyNetIncome { get; set; }
        public decimal HouseholdGrossAnnualIncome { get; set; }
        public decimal TargetMonthlyTakeHome { get; set; }
        public decimal EstimatedMonthlyTakeHome { get; set; }
        public decimal EstimatedAnnualTakeHome { get; set; }
        public decimal FederalIncomeTax { get; set; }
        public decimal SocialSecurityTax { get; set; }
        public decimal MedicareTax { get; set; }
        public decimal AdditionalMedicareTax { get; set; }
        public decimal TennesseeIncomeTax { get; set; }
        public decimal TotalEstimatedTax { get; set; }
        public decimal AnnualPreTaxDeductions { get; set; }
        public decimal AnnualPostTaxDeductions { get; set; }
        public decimal EffectiveTaxRate { get; set; }
        public decimal EffectiveTotalWithholdingRate { get; set; }
        public decimal StandardDeduction { get; set; }
        public decimal FederalTaxableIncome { get; set; }
        public decimal SocialSecurityTaxableWages { get; set; }
        public decimal MedicareTaxableWages { get; set; }
        public int TotalLegacyWithholdingAllowances { get; set; }
        public decimal LegacyAllowanceDeductionEquivalent { get; set; }
        public bool LegacyAllowancesAppliedToFederalEstimate { get; set; }
        public IReadOnlyList<TaxBreakdownLine> BreakdownLines { get; set; } = [];
    }

    public sealed class TaxBreakdownLine
    {
        public string Item { get; set; } = string.Empty;
        public decimal AnnualAmount { get; set; }
        public decimal MonthlyAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
