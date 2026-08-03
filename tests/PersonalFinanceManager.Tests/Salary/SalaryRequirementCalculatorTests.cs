using PersonalFinanceManager.Core.Salary;

namespace PersonalFinanceManager.Tests.Salary
{
    [TestClass]
    public sealed class SalaryRequirementCalculatorTests
    {
        [TestMethod]
        public void CalculateFederalIncomeTax_UsesProgressiveSingleBrackets()
        {
            SalaryRequirementCalculator calculator = new();
            decimal tax = calculator.CalculateFederalIncomeTax(60000M, FilingStatus.Single);
            Assert.AreEqual(7912M, tax);
        }

        [TestMethod]
        public void CalculateRequiredSalary_ProducesTakeHomeAtOrAboveTarget()
        {
            SalaryRequirementCalculator calculator = new();

            SalaryRequirementInput input = new()
            {
                TargetMonthlySpending = 6000M,
                MonthlyCushion = 500M,
                FilingStatus = FilingStatus.Single,
                PayPeriodsPerYear = 26
            };

            SalaryRequirementResult result = calculator.CalculateRequiredSalary(input);
            Assert.IsGreaterThanOrEqualTo(6500M, result.EstimatedMonthlyTakeHome);
            Assert.IsGreaterThan(result.EstimatedAnnualTakeHome, result.RequiredGrossAnnualSalary);
        }

        [TestMethod]
        public void SocialSecurityTax_CapsAtWageBase()
        {
            decimal tax = SalaryRequirementCalculator.CalculateSocialSecurityTax(300000M);
            Assert.AreEqual(11439M, tax);
        }

        [TestMethod]
        public void LegacyAllowances_ReduceFederalTaxableIncomeEstimate()
        {
            SalaryRequirementCalculator calculator = new();

            SalaryRequirementInput input = new()
            {
                TargetMonthlySpending = 6000M,
                FilingStatus = FilingStatus.MarriedFilingJointly,
                UseLegacyAllowanceEstimate = true,
                YourLegacyAllowances = 0,
                SpouseLegacyAllowances = 3,
                SpouseAnnualGrossIncome = 40000M,
                PayPeriodsPerYear = 26
            };

            SalaryRequirementResult result = calculator.CalculateRequiredSalary(input);
            Assert.AreEqual(3, result.TotalLegacyWithholdingAllowances);
            Assert.AreEqual(12900M, result.LegacyAllowanceDeductionEquivalent);
        }

        [TestMethod]
        public void LegacyAllowances_DoesNotApplySpouseAllowancesWithoutSpouseIncome()
        {
            SalaryRequirementCalculator calculator = new();

            SalaryRequirementInput input = new()
            {
                TargetMonthlySpending = 6000M,
                FilingStatus = FilingStatus.MarriedFilingJointly,
                UseLegacyAllowanceEstimate = true,
                YourLegacyAllowances = 0,
                SpouseLegacyAllowances = 3,
                SpouseAnnualGrossIncome = 0M,
                PayPeriodsPerYear = 26
            };

            SalaryRequirementResult result = calculator.CalculateRequiredSalary(input);
            Assert.AreEqual(0, result.TotalLegacyWithholdingAllowances);
            Assert.AreEqual(0M, result.LegacyAllowanceDeductionEquivalent);
        }

        [TestMethod]
        public void SpousePostedNetIncome_ReducesRequiredSalaryTarget()
        {
            SalaryRequirementCalculator calculator = new();

            SalaryRequirementInput withoutSpouseIncome = new()
            {
                TargetMonthlySpending = 6000M,
                FilingStatus = FilingStatus.MarriedFilingJointly,
                PayPeriodsPerYear = 26
            };

            SalaryRequirementInput withSpouseIncome = new()
            {
                TargetMonthlySpending = 6000M,
                SpouseMonthlyNetIncome = 2000M,
                FilingStatus = FilingStatus.MarriedFilingJointly,
                PayPeriodsPerYear = 26
            };

            SalaryRequirementResult higher = calculator.CalculateRequiredSalary(withoutSpouseIncome);
            SalaryRequirementResult lower = calculator.CalculateRequiredSalary(withSpouseIncome);
            Assert.IsLessThan(higher.RequiredGrossAnnualSalary, lower.RequiredGrossAnnualSalary);
            Assert.AreEqual(2000M, lower.SpouseMonthlyNetIncome);
        }
    }
}
