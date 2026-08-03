namespace PersonalFinanceManager.Core.Salary
{
    public sealed class SalaryRequirementInput
    {
        public decimal TargetMonthlySpending { get; set; }
        public decimal MonthlyCushion { get; set; }
        public decimal PreTaxFederalOnlyMonthly { get; set; }
        public decimal PreTaxFederalAndFicaMonthly { get; set; }
        public decimal PostTaxMonthlyDeductions { get; set; }
        public decimal SpouseAnnualGrossIncome { get; set; }
        public decimal SpouseMonthlyNetIncome { get; set; }
        public bool UseLegacyAllowanceEstimate { get; set; }
        public int YourLegacyAllowances { get; set; }
        public int SpouseLegacyAllowances { get; set; }
        public decimal LegacyAllowanceAnnualValue { get; set; } = SalaryRequirementCalculator.LegacyW4AllowanceAmountAnnual;
        public FilingStatus FilingStatus { get; set; } = FilingStatus.Single;
        public int PayPeriodsPerYear { get; set; } = 26;

        public int EmployeeLegacyW4Allowances
        {
            get
            {
                return YourLegacyAllowances;
            }
            set
            {
                YourLegacyAllowances = value;
            }
        }

        public int SpouseLegacyW4Allowances
        {
            get
            {
                return SpouseLegacyAllowances;
            }
            set
            {
                SpouseLegacyAllowances = value;
            }
        }
    }
}
