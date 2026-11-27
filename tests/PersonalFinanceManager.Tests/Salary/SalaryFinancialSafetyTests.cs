using PersonalFinanceManager.Core.Salary;

namespace PersonalFinanceManager.Tests.Salary;

[TestClass]
public sealed class SalaryFinancialSafetyTests
{
    [TestMethod]
    public void PostedSpouseNetAndGrossContextMustStillFundTheFullHouseholdTarget()
    {
        SalaryRequirementCalculator calculator = new();
        SalaryRequirementInput input = new()
        {
            TargetMonthlySpending = 6000M, SpouseMonthlyNetIncome = 2000M, SpouseAnnualGrossIncome = 40000M,
            FilingStatus = FilingStatus.MarriedFilingJointly, UseLegacyAllowanceEstimate = true, SpouseLegacyAllowances = 3
        };
        SalaryRequirementResult result = calculator.CalculateRequiredSalary(input);
        Assert.AreEqual(6000M, result.TargetMonthlyTakeHome);
        Assert.IsTrue(result.EstimatedMonthlyTakeHome >= 6000M);
        Assert.IsTrue(result.RequiredGrossAnnualSalary > 40000M);
        Assert.AreEqual(result.RequiredGrossAnnualSalary + 24000M, result.CashBeforeModeledTax);
        Assert.IsTrue(calculator.CalculateAnnualTakeHome(result.RequiredGrossAnnualSalary - 100M, input) < 72000M);
    }

    [TestMethod]
    public void IncreasingSpouseGrossContextDoesNotCreateAnotherCashContribution()
    {
        SalaryRequirementCalculator calculator = new();
        SalaryRequirementInput input = new() { TargetMonthlySpending = 6000M, SpouseMonthlyNetIncome = 2000M, FilingStatus = FilingStatus.MarriedFilingJointly };
        decimal withoutContext = calculator.CalculateRequiredSalary(input).RequiredGrossAnnualSalary;
        input.SpouseAnnualGrossIncome = 40000M;
        decimal withContext = calculator.CalculateRequiredSalary(input).RequiredGrossAnnualSalary;
        Assert.IsTrue(withContext >= withoutContext);
    }

    [TestMethod]
    public void GrossSpouseModeIgnoresThePostedNetInput()
    {
        SalaryRequirementCalculator calculator = new();
        SalaryRequirementInput input = new() { TargetMonthlySpending = 6000M, SpouseAnnualGrossIncome = 40000M,
            FilingStatus = FilingStatus.MarriedFilingJointly, SpouseIncomeMode = SpouseIncomeMode.EstimatedFromGross };
        SalaryRequirementResult original = calculator.CalculateRequiredSalary(input);
        input.SpouseMonthlyNetIncome = 2000M;
        SalaryRequirementResult withPostedNet = calculator.CalculateRequiredSalary(input);
        Assert.AreEqual(original.RequiredGrossAnnualSalary, withPostedNet.RequiredGrossAnnualSalary);
        Assert.AreEqual(0M, withPostedNet.SpouseMonthlyNetIncome);
        Assert.IsTrue(withPostedNet.EstimatedMonthlyTakeHome >= 6000M);
    }

    [TestMethod]
    public void SpouseNetCanCoverSpendingButCannotHideEmployeeDeductions()
    {
        SalaryRequirementCalculator calculator = new();
        SalaryRequirementInput input = new() { TargetMonthlySpending = 2000M, SpouseMonthlyNetIncome = 2000M };
        Assert.AreEqual(0M, calculator.CalculateRequiredSalary(input).RequiredGrossAnnualSalary);
        input.PostTaxMonthlyDeductions = 100M;
        SalaryRequirementResult result = calculator.CalculateRequiredSalary(input);
        Assert.IsTrue(result.RequiredGrossAnnualSalary > 0M);
        Assert.IsTrue(result.EstimatedMonthlyTakeHome >= 2000M);
    }
}
