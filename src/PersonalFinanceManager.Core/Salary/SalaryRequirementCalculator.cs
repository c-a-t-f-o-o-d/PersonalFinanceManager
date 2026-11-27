namespace PersonalFinanceManager.Core.Salary
{
    public sealed class SalaryRequirementCalculator
    {
        public const decimal SocialSecurityRate = 0.062M;
        public const decimal SocialSecurityWageBase = 184500M;
        public const decimal MedicareRate = 0.0145M;
        public const decimal AdditionalMedicareRate = 0.009M;
        public const decimal LegacyW4AllowanceAmountAnnual = 4300M;

        public SalaryRequirementResult CalculateRequiredSalary(SalaryRequirementInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            Validate(input);
            decimal targetMonthlyTakeHome = input.TargetMonthlySpending + input.MonthlyCushion;
            decimal targetAnnualTakeHome = targetMonthlyTakeHome * 12M;

            if (CalculateAnnualTakeHome(0M, input) >= targetAnnualTakeHome)
            {
                return BuildResult(0M, input, targetMonthlyTakeHome);
            }

            decimal low = 0M;
            decimal high = Math.Max(50000M, targetAnnualTakeHome * 2M);

            while (CalculateAnnualTakeHome(high, input) < targetAnnualTakeHome)
            {
                if (high >= 1000000000000M)
                {
                    throw new ArgumentOutOfRangeException(nameof(input), "The requested salary is outside the supported planning range.");
                }

                high *= 1.5M;
            }

            for (int index = 0; index < 80; index++)
            {
                decimal middle = (low + high) / 2M;

                if (CalculateAnnualTakeHome(middle, input) >= targetAnnualTakeHome)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }

            decimal roundedSalary = Math.Ceiling(high / 100M) * 100M;
            return BuildResult(roundedSalary, input, targetMonthlyTakeHome);
        }

        public decimal CalculateAnnualTakeHome(decimal grossAnnualSalary, SalaryRequirementInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            Validate(input);
            TaxComputation computation = ComputeTaxes(grossAnnualSalary, input);

            return computation.CashBeforeModeledTax
                - computation.FederalIncomeTax
                - computation.SocialSecurityTax
                - computation.MedicareTax
                - computation.AdditionalMedicareTax
                - computation.AnnualPreTaxDeductions
                - computation.AnnualPostTaxDeductions;
        }

        public decimal CalculateFederalIncomeTax(decimal taxableIncome, FilingStatus filingStatus)
        {
            decimal tax = 0M;

            foreach (TaxBracket bracket in GetTaxBrackets(filingStatus))
            {
                decimal upperBound = bracket.UpperBound ?? taxableIncome;

                if (taxableIncome <= bracket.LowerBound)
                {
                    continue;
                }

                decimal taxableInBracket = Math.Min(taxableIncome, upperBound) - bracket.LowerBound;

                if (taxableInBracket > 0M)
                {
                    tax += taxableInBracket * bracket.Rate;
                }
            }

            return RoundCurrency(tax);
        }

        public decimal CalculateFederalTaxableIncome(decimal grossAnnualSalary, FilingStatus filingStatus, decimal federalOnlyDeductions, decimal ficaExemptDeductions)
        {
            return Math.Max(0M, grossAnnualSalary - federalOnlyDeductions - ficaExemptDeductions - GetStandardDeduction(filingStatus));
        }

        public static decimal CalculateSocialSecurityTax(decimal socialSecurityTaxableWages)
        {
            return RoundCurrency(Math.Min(Math.Max(0M, socialSecurityTaxableWages), SocialSecurityWageBase) * SocialSecurityRate);
        }

        public static decimal CalculateMedicareTax(decimal medicareTaxableWages)
        {
            return RoundCurrency(Math.Max(0M, medicareTaxableWages) * MedicareRate);
        }

        public static decimal CalculateAdditionalMedicareTax(decimal medicareTaxableWages, FilingStatus filingStatus)
        {
            decimal threshold = filingStatus == FilingStatus.MarriedFilingJointly ? 250000M : 200000M;
            return RoundCurrency(Math.Max(0M, medicareTaxableWages - threshold) * AdditionalMedicareRate);
        }

        public static decimal GetStandardDeduction(FilingStatus filingStatus)
        {
            return filingStatus switch
            {
                FilingStatus.MarriedFilingJointly => 32200M,
                FilingStatus.HeadOfHousehold => 24150M,
                _ => 16100M
            };
        }

        public static IReadOnlyList<TaxBracket> GetTaxBrackets(FilingStatus filingStatus)
        {
            return filingStatus switch
            {
                FilingStatus.MarriedFilingJointly =>
                [
                    new(0M, 24800M, 0.10M),
                    new(24800M, 100800M, 0.12M),
                    new(100800M, 211400M, 0.22M),
                    new(211400M, 403550M, 0.24M),
                    new(403550M, 512450M, 0.32M),
                    new(512450M, 768700M, 0.35M),
                    new(768700M, null, 0.37M)
                ],
                FilingStatus.HeadOfHousehold =>
                [
                    new(0M, 17700M, 0.10M),
                    new(17700M, 67450M, 0.12M),
                    new(67450M, 105700M, 0.22M),
                    new(105700M, 201775M, 0.24M),
                    new(201775M, 256200M, 0.32M),
                    new(256200M, 640600M, 0.35M),
                    new(640600M, null, 0.37M)
                ],
                _ =>
                [
                    new(0M, 12400M, 0.10M),
                    new(12400M, 50400M, 0.12M),
                    new(50400M, 105700M, 0.22M),
                    new(105700M, 201775M, 0.24M),
                    new(201775M, 256225M, 0.32M),
                    new(256225M, 640600M, 0.35M),
                    new(640600M, null, 0.37M)
                ]
            };
        }

        private static SalaryRequirementResult BuildResult(decimal grossAnnualSalary, SalaryRequirementInput input, decimal targetMonthlyTakeHome)
        {
            TaxComputation computation = ComputeTaxes(grossAnnualSalary, input);
            decimal totalTax = computation.FederalIncomeTax + computation.SocialSecurityTax + computation.MedicareTax + computation.AdditionalMedicareTax;
            decimal estimatedAnnualTakeHome = computation.CashBeforeModeledTax - totalTax - computation.AnnualPreTaxDeductions - computation.AnnualPostTaxDeductions;
            decimal payPeriods = Math.Max(1, input.PayPeriodsPerYear);
            decimal householdGross = computation.HouseholdGrossAnnualIncome;
            decimal taxRateBase = input.SpouseIncomeMode == SpouseIncomeMode.PostedNet ? grossAnnualSalary : householdGross;

            return new SalaryRequirementResult
            {
                RequiredGrossAnnualSalary = RoundCurrency(grossAnnualSalary),
                RequiredGrossMonthlySalary = RoundCurrency(grossAnnualSalary / 12M),
                RequiredGrossPerPayPeriod = RoundCurrency(grossAnnualSalary / payPeriods),
                SpouseAnnualGrossIncome = RoundCurrency(input.SpouseAnnualGrossIncome),
                SpouseMonthlyNetIncome = input.SpouseIncomeMode == SpouseIncomeMode.PostedNet ? RoundCurrency(input.SpouseMonthlyNetIncome) : 0M,
                SpouseIncomeMode = input.SpouseIncomeMode,
                CashBeforeModeledTax = RoundCurrency(computation.CashBeforeModeledTax),
                HouseholdGrossAnnualIncome = RoundCurrency(householdGross),
                TargetMonthlyTakeHome = RoundCurrency(targetMonthlyTakeHome),
                EstimatedMonthlyTakeHome = RoundCurrency(estimatedAnnualTakeHome / 12M),
                EstimatedAnnualTakeHome = RoundCurrency(estimatedAnnualTakeHome),
                FederalIncomeTax = computation.FederalIncomeTax,
                SocialSecurityTax = computation.SocialSecurityTax,
                MedicareTax = computation.MedicareTax,
                AdditionalMedicareTax = computation.AdditionalMedicareTax,
                TennesseeIncomeTax = 0M,
                TotalEstimatedTax = totalTax,
                AnnualPreTaxDeductions = computation.AnnualPreTaxDeductions,
                AnnualPostTaxDeductions = computation.AnnualPostTaxDeductions,
                EffectiveTaxRate = taxRateBase > 0M ? decimal.Round(totalTax / taxRateBase * 100M, 2, MidpointRounding.AwayFromZero) : 0M,
                EffectiveTotalWithholdingRate = taxRateBase > 0M ? decimal.Round((totalTax + computation.AnnualPreTaxDeductions + computation.AnnualPostTaxDeductions) / taxRateBase * 100M, 2, MidpointRounding.AwayFromZero) : 0M,
                StandardDeduction = GetStandardDeduction(input.FilingStatus),
                FederalTaxableIncome = RoundCurrency(computation.FederalTaxableIncome),
                SocialSecurityTaxableWages = RoundCurrency(computation.SocialSecurityTaxableWages),
                MedicareTaxableWages = RoundCurrency(computation.MedicareTaxableWages),
                TotalLegacyWithholdingAllowances = computation.TotalLegacyAllowances,
                LegacyAllowanceDeductionEquivalent = computation.LegacyAllowanceAdjustment,
                LegacyAllowancesAppliedToFederalEstimate = computation.LegacyAllowanceAdjustment > 0M,
                BreakdownLines = BuildBreakdownLines(grossAnnualSalary, input, computation, totalTax, estimatedAnnualTakeHome, targetMonthlyTakeHome)
            };
        }

        private static TaxComputation ComputeTaxes(decimal grossAnnualSalary, SalaryRequirementInput input)
        {
            decimal employeeGross = Math.Max(0M, grossAnnualSalary);
            decimal spouseGross = input.FilingStatus == FilingStatus.MarriedFilingJointly ? input.SpouseAnnualGrossIncome : 0M;
            bool postedNet = input.SpouseIncomeMode == SpouseIncomeMode.PostedNet;
            decimal annualFederalOnlyDeductions = input.PreTaxFederalOnlyMonthly * 12M;
            decimal annualFicaExemptDeductions = input.PreTaxFederalAndFicaMonthly * 12M;
            int totalLegacyAllowances = CalculateAppliedLegacyAllowances(input);
            decimal legacyAllowanceAdjustment = totalLegacyAllowances * input.LegacyAllowanceAnnualValue;
            decimal householdTaxableIncome = Math.Max(0M, employeeGross + spouseGross - annualFederalOnlyDeductions - annualFicaExemptDeductions - GetStandardDeduction(input.FilingStatus) - legacyAllowanceAdjustment);
            SalaryRequirementCalculator calculator = new();
            decimal federalTax = calculator.CalculateFederalIncomeTax(householdTaxableIncome, input.FilingStatus);
            decimal employeeFicaWages = Math.Max(0M, employeeGross - annualFicaExemptDeductions);
            decimal spouseAllowances = input.UseLegacyAllowanceEstimate && spouseGross > 0M ? input.SpouseLegacyAllowances * input.LegacyAllowanceAnnualValue : 0M;
            decimal additionalMedicare = CalculateAdditionalMedicareTax(employeeFicaWages + spouseGross, input.FilingStatus);

            if (postedNet)
            {
                decimal spouseTaxableIncome = Math.Max(0M, spouseGross - GetStandardDeduction(input.FilingStatus) - spouseAllowances);
                federalTax = Math.Max(0M, federalTax - calculator.CalculateFederalIncomeTax(spouseTaxableIncome, input.FilingStatus));
                additionalMedicare = Math.Max(0M, additionalMedicare - CalculateAdditionalMedicareTax(spouseGross, input.FilingStatus));
            }

            decimal modeledSpouseWages = postedNet ? 0M : spouseGross;

            return new TaxComputation
            {
                AnnualPreTaxDeductions = annualFederalOnlyDeductions + annualFicaExemptDeductions,
                AnnualPostTaxDeductions = input.PostTaxMonthlyDeductions * 12M,
                HouseholdGrossAnnualIncome = employeeGross + spouseGross,
                CashBeforeModeledTax = employeeGross + (postedNet ? input.SpouseMonthlyNetIncome * 12M : spouseGross),
                FederalTaxableIncome = householdTaxableIncome,
                FederalIncomeTax = federalTax,
                SocialSecurityTax = CalculateSocialSecurityTax(employeeFicaWages) + CalculateSocialSecurityTax(modeledSpouseWages),
                MedicareTax = CalculateMedicareTax(employeeFicaWages + modeledSpouseWages),
                AdditionalMedicareTax = additionalMedicare,
                SocialSecurityTaxableWages = Math.Min(employeeFicaWages, SocialSecurityWageBase) + Math.Min(modeledSpouseWages, SocialSecurityWageBase),
                MedicareTaxableWages = employeeFicaWages + modeledSpouseWages,
                TotalLegacyAllowances = totalLegacyAllowances,
                LegacyAllowanceAdjustment = legacyAllowanceAdjustment
            };
        }

        private static int CalculateAppliedLegacyAllowances(SalaryRequirementInput input)
        {
            if (!input.UseLegacyAllowanceEstimate)
            {
                return 0;
            }

            int yourAllowances = Math.Max(0, input.YourLegacyAllowances);
            int spouseAllowances = input.FilingStatus == FilingStatus.MarriedFilingJointly && input.SpouseAnnualGrossIncome > 0M ? Math.Max(0, input.SpouseLegacyAllowances) : 0;
            return yourAllowances + spouseAllowances;
        }

        private static IReadOnlyList<TaxBreakdownLine> BuildBreakdownLines(decimal grossAnnualSalary, SalaryRequirementInput input, TaxComputation computation, decimal totalTax, decimal estimatedAnnualTakeHome, decimal targetMonthlyTakeHome)
        {
            return
            [
                CreateLine("Required gross salary", grossAnnualSalary, "Minimum rounded annual salary estimate for your income input"),
                CreateLine("Spouse gross income", input.SpouseAnnualGrossIncome, input.SpouseIncomeMode == SpouseIncomeMode.PostedNet ? "Tax-bracket context only; not counted again as available cash" : "Spouse cash modeled from gross income"),
                CreateLine("Spouse posted net income", input.SpouseIncomeMode == SpouseIncomeMode.PostedNet ? input.SpouseMonthlyNetIncome * 12M : 0M, "Counted once in posted-net mode; not used in gross-income mode"),
                CreateLine("Household gross income", computation.HouseholdGrossAnnualIncome, "Gross income for tax context"),
                CreateLine("Cash before modeled tax", computation.CashBeforeModeledTax, "Your gross plus either spouse posted net or spouse gross, according to the selected mode"),
                CreateLine("Federal taxable income", computation.FederalTaxableIncome, "Household gross minus standard deduction, pre-tax deductions, and legacy allowance estimate"),
                CreateLine("Legacy W-4 allowances", -computation.LegacyAllowanceAdjustment, $"{computation.TotalLegacyAllowances} allowance(s) × {input.LegacyAllowanceAnnualValue:C0}; current W-4 forms do not use allowances"),
                CreateLine("Federal income tax", -computation.FederalIncomeTax, input.SpouseIncomeMode == SpouseIncomeMode.PostedNet ? "Incremental tax on your modeled salary; spouse posted income is already net" : "Estimated household federal income tax after standard deduction"),
                CreateLine("Social Security", -computation.SocialSecurityTax, "6.2% up to the 2026 wage base per worker"),
                CreateLine("Medicare", -computation.MedicareTax, "1.45% on Medicare taxable wages"),
                CreateLine("Additional Medicare", -computation.AdditionalMedicareTax, "0.9% above the applicable household threshold"),
                CreateLine("Tennessee income tax", 0M, "Tennessee has no earned-income withholding"),
                CreateLine("Pre-tax deductions", -computation.AnnualPreTaxDeductions, "Monthly pre-tax inputs annualized"),
                CreateLine("Post-tax deductions", -computation.AnnualPostTaxDeductions, "Monthly post-tax inputs annualized"),
                CreateLine("Total estimated tax", -totalTax, "Federal income tax, Social Security, and Medicare"),
                CreateLine("Estimated take-home", estimatedAnnualTakeHome, "Estimated household after-tax and deduction take-home"),
                CreateLine("Target take-home", targetMonthlyTakeHome * 12M, "Selected spending basis plus cushion")
            ];
        }

        private static TaxBreakdownLine CreateLine(string item, decimal annualAmount, string notes)
        {
            return new TaxBreakdownLine
            {
                Item = item,
                AnnualAmount = RoundCurrency(annualAmount),
                MonthlyAmount = RoundCurrency(annualAmount / 12M),
                Notes = notes
            };
        }

        private static decimal RoundCurrency(decimal value)
        {
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static void Validate(SalaryRequirementInput input)
        {
            if (!Enum.IsDefined(input.SpouseIncomeMode))
            {
                throw new ArgumentOutOfRangeException(nameof(input.SpouseIncomeMode));
            }

            if (input.SpouseIncomeMode == SpouseIncomeMode.EstimatedFromGross && input.SpouseAnnualGrossIncome > 0M && input.FilingStatus != FilingStatus.MarriedFilingJointly)
            {
                throw new ArgumentException("Spouse gross-income modeling requires Married Filing Jointly. Use posted net income for separate tax households.", nameof(input));
            }

            if (input.TargetMonthlySpending < 0M)
            {
                throw new ArgumentOutOfRangeException(nameof(input.TargetMonthlySpending));
            }

            if (input.MonthlyCushion < 0M)
            {
                throw new ArgumentOutOfRangeException(nameof(input.MonthlyCushion));
            }

            if (input.PreTaxFederalOnlyMonthly < 0M || input.PreTaxFederalAndFicaMonthly < 0M || input.PostTaxMonthlyDeductions < 0M)
            {
                throw new ArgumentOutOfRangeException(nameof(input));
            }

            if (input.SpouseAnnualGrossIncome < 0M)
            {
                throw new ArgumentOutOfRangeException(nameof(input.SpouseAnnualGrossIncome));
            }

            if (input.SpouseMonthlyNetIncome < 0M)
            {
                throw new ArgumentOutOfRangeException(nameof(input.SpouseMonthlyNetIncome));
            }

            if (input.YourLegacyAllowances < 0 || input.SpouseLegacyAllowances < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(input));
            }

            if (input.LegacyAllowanceAnnualValue < 0M)
            {
                throw new ArgumentOutOfRangeException(nameof(input.LegacyAllowanceAnnualValue));
            }

            if (input.PayPeriodsPerYear <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(input.PayPeriodsPerYear));
            }
        }

        private sealed class TaxComputation
        {
            public decimal AnnualPreTaxDeductions { get; set; }
            public decimal AnnualPostTaxDeductions { get; set; }
            public decimal HouseholdGrossAnnualIncome { get; set; }
            public decimal CashBeforeModeledTax { get; set; }
            public decimal FederalTaxableIncome { get; set; }
            public decimal FederalIncomeTax { get; set; }
            public decimal SocialSecurityTax { get; set; }
            public decimal MedicareTax { get; set; }
            public decimal AdditionalMedicareTax { get; set; }
            public decimal SocialSecurityTaxableWages { get; set; }
            public decimal MedicareTaxableWages { get; set; }
            public int TotalLegacyAllowances { get; set; }
            public decimal LegacyAllowanceAdjustment { get; set; }
        }
    }
}
