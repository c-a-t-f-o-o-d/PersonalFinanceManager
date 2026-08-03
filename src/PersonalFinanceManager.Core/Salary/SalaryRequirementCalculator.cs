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
            decimal spouseMonthlyNetIncome = Math.Max(0M, input.SpouseMonthlyNetIncome);
            decimal targetMonthlyTakeHome = Math.Max(0M, input.TargetMonthlySpending + input.MonthlyCushion - spouseMonthlyNetIncome);
            decimal targetAnnualTakeHome = targetMonthlyTakeHome * 12M;

            if (targetAnnualTakeHome <= 0M)
            {
                return BuildResult(0M, input, targetMonthlyTakeHome);
            }

            decimal low = 0M;
            decimal high = Math.Max(50000M, targetAnnualTakeHome * 2M);

            while (CalculateAnnualTakeHome(high, input) < targetAnnualTakeHome && high < 2000000M)
            {
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

            return computation.HouseholdGrossAnnualIncome
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
            decimal estimatedAnnualTakeHome = computation.HouseholdGrossAnnualIncome - totalTax - computation.AnnualPreTaxDeductions - computation.AnnualPostTaxDeductions;
            decimal payPeriods = Math.Max(1, input.PayPeriodsPerYear);
            decimal householdGross = computation.HouseholdGrossAnnualIncome;

            return new SalaryRequirementResult
            {
                RequiredGrossAnnualSalary = RoundCurrency(grossAnnualSalary),
                RequiredGrossMonthlySalary = RoundCurrency(grossAnnualSalary / 12M),
                RequiredGrossPerPayPeriod = RoundCurrency(grossAnnualSalary / payPeriods),
                SpouseAnnualGrossIncome = RoundCurrency(input.SpouseAnnualGrossIncome),
                SpouseMonthlyNetIncome = RoundCurrency(input.SpouseMonthlyNetIncome),
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
                EffectiveTaxRate = householdGross > 0M ? decimal.Round(totalTax / householdGross * 100M, 2, MidpointRounding.AwayFromZero) : 0M,
                EffectiveTotalWithholdingRate = householdGross > 0M ? decimal.Round((totalTax + computation.AnnualPreTaxDeductions + computation.AnnualPostTaxDeductions) / householdGross * 100M, 2, MidpointRounding.AwayFromZero) : 0M,
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
            decimal spouseGrossAnnualIncome = Math.Max(0M, input.SpouseAnnualGrossIncome);
            decimal householdGrossAnnualIncome = Math.Max(0M, grossAnnualSalary) + spouseGrossAnnualIncome;
            decimal annualFederalOnlyDeductions = input.PreTaxFederalOnlyMonthly * 12M;
            decimal annualFicaExemptDeductions = input.PreTaxFederalAndFicaMonthly * 12M;
            decimal annualPreTaxDeductions = annualFederalOnlyDeductions + annualFicaExemptDeductions;
            decimal annualPostTaxDeductions = input.PostTaxMonthlyDeductions * 12M;
            int totalLegacyAllowances = CalculateAppliedLegacyAllowances(input);
            decimal legacyAllowanceAdjustment = totalLegacyAllowances * input.LegacyAllowanceAnnualValue;
            decimal federalTaxableIncome = Math.Max(0M, householdGrossAnnualIncome - annualFederalOnlyDeductions - annualFicaExemptDeductions - GetStandardDeduction(input.FilingStatus) - legacyAllowanceAdjustment);
            SalaryRequirementCalculator calculator = new();
            decimal federalIncomeTax = calculator.CalculateFederalIncomeTax(federalTaxableIncome, input.FilingStatus);
            decimal employeeFicaWages = Math.Max(0M, grossAnnualSalary - annualFicaExemptDeductions);
            decimal spouseFicaWages = spouseGrossAnnualIncome;
            decimal socialSecurityTaxableWages = Math.Min(employeeFicaWages, SocialSecurityWageBase) + Math.Min(spouseFicaWages, SocialSecurityWageBase);
            decimal medicareTaxableWages = employeeFicaWages + spouseFicaWages;

            return new TaxComputation
            {
                AnnualPreTaxDeductions = annualPreTaxDeductions,
                AnnualPostTaxDeductions = annualPostTaxDeductions,
                HouseholdGrossAnnualIncome = householdGrossAnnualIncome,
                FederalTaxableIncome = federalTaxableIncome,
                FederalIncomeTax = federalIncomeTax,
                SocialSecurityTax = CalculateSocialSecurityTax(employeeFicaWages) + CalculateSocialSecurityTax(spouseFicaWages),
                MedicareTax = CalculateMedicareTax(medicareTaxableWages),
                AdditionalMedicareTax = CalculateAdditionalMedicareTax(medicareTaxableWages, input.FilingStatus),
                SocialSecurityTaxableWages = socialSecurityTaxableWages,
                MedicareTaxableWages = medicareTaxableWages,
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
            int spouseAllowances = input.SpouseAnnualGrossIncome > 0M ? Math.Max(0, input.SpouseLegacyAllowances) : 0;
            return yourAllowances + spouseAllowances;
        }

        private static IReadOnlyList<TaxBreakdownLine> BuildBreakdownLines(decimal grossAnnualSalary, SalaryRequirementInput input, TaxComputation computation, decimal totalTax, decimal estimatedAnnualTakeHome, decimal targetMonthlyTakeHome)
        {
            return
            [
                CreateLine("Required gross salary", grossAnnualSalary, "Minimum rounded annual salary estimate for your income input"),
                CreateLine("Spouse gross income", input.SpouseAnnualGrossIncome, "Optional gross household income input for tax context"),
                CreateLine("Spouse posted net income", input.SpouseMonthlyNetIncome * 12M, "Owner-classified spouse income already available for household spending"),
                CreateLine("Household gross income", computation.HouseholdGrossAnnualIncome, "Required salary plus spouse gross income"),
                CreateLine("Federal taxable income", computation.FederalTaxableIncome, "Household gross minus standard deduction, pre-tax deductions, and legacy allowance estimate"),
                CreateLine("Legacy W-4 allowances", -computation.LegacyAllowanceAdjustment, $"{computation.TotalLegacyAllowances} allowance(s) × {input.LegacyAllowanceAnnualValue:C0}; current W-4 forms do not use allowances"),
                CreateLine("Federal income tax", -computation.FederalIncomeTax, "Estimated annual federal income tax after standard deduction"),
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
