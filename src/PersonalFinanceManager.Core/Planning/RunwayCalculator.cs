using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Core.Planning;

public sealed class RunwayCalculator
{
    public RunwayAnalysisResult Calculate(IReadOnlyList<Account> accounts, IReadOnlyList<RecurringBill> bills, string scenario, decimal monthlyIncome, int projectionMonths, DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(bills);
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyIncome);

        if (projectionMonths is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(projectionMonths));
        }

        DateOnly endDate = asOfDate.AddMonths(projectionMonths);
        Account[] includedAccounts = accounts.Where(account => account.IsActive && IncludesAccount(account, scenario)).ToArray();
        Account[] knownAccounts = includedAccounts.Where(account => account.AvailableBalance.HasValue || account.CurrentBalance.HasValue).ToArray();
        List<string> warnings = new();

        if (knownAccounts.Length == 0)
        {
            warnings.Add("No cash balances are available. The starting balance and depletion date are unknown.");
        }

        if (knownAccounts.Length != includedAccounts.Length)
        {
            warnings.Add($"{includedAccounts.Length - knownAccounts.Length} included account(s) have no balance; the projection is incomplete.");
        }

        int undated = knownAccounts.Count(account => !account.BalanceAsOfDate.HasValue);
        int outdated = knownAccounts.Count(account => account.BalanceAsOfDate.HasValue && account.BalanceAsOfDate.Value < asOfDate.AddDays(-31));
        int futureDated = knownAccounts.Count(account => account.BalanceAsOfDate > asOfDate);

        if (undated > 0)
        {
            warnings.Add($"{undated} balance(s) have no as-of date. Confirm that these snapshots are current.");
        }

        if (outdated > 0)
        {
            warnings.Add($"{outdated} balance snapshot(s) are more than 31 days old. Imports do not update these snapshots.");
        }

        if (futureDated > 0)
        {
            warnings.Add($"{futureDated} balance snapshot(s) are dated after the projection start; review them.");
        }

        if (string.Equals(scenario, "Emergency", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Investment balances are assumed available at their entered value; sale delays, tax, and withdrawal penalties are not modeled.");
        }

        RunwayAnalysisResult result = new()
        {
            ScenarioName = scenario,
            AsOfDate = asOfDate,
            StartingAvailableBalance = knownAccounts.Sum(account => account.AvailableBalance ?? account.CurrentBalance ?? 0M),
            MonthlyIncome = monthlyIncome,
            HasKnownBalance = knownAccounts.Length > 0,
            HasCompleteInputs = knownAccounts.Length > 0 && knownAccounts.Length == includedAccounts.Length
        };

        Dictionary<DateOnly, decimal> scheduledExpenses = new();
        decimal unscheduledMonthly = 0M;
        RecurringBill[] requiredBills = bills.Where(BillSchedule.IsRequiredExpense).ToArray();
        IReadOnlyList<string> billIdentityWarnings = RecurringBillMatcher.OverlapWarnings(requiredBills);
        warnings.AddRange(billIdentityWarnings);

        if (billIdentityWarnings.Count > 0)
        {
            result.HasCompleteInputs = false;
        }

        foreach (RecurringBill bill in requiredBills)
        {
            if (bill.TypicalAmount <= 0M)
            {
                warnings.Add($"'{bill.BillName}' has no positive amount; the projection is incomplete.");
                result.HasCompleteInputs = false;
                continue;
            }

            string frequency;

            try
            {
                frequency = BillSchedule.NormalizeFrequency(bill.Frequency);
            }
            catch (ArgumentException)
            {
                warnings.Add($"'{bill.BillName}' needs a supported frequency; the projection is incomplete.");
                result.HasCompleteInputs = false;
                continue;
            }

            decimal monthly = BillSchedule.MonthlyEquivalent(bill.TypicalAmount, frequency);

            result.IncludedBills.Add(new RunwayBillItem
            {
                BillName = bill.BillName,
                Payee = bill.Payee,
                RequirementType = bill.RequirementType,
                Frequency = frequency,
                TypicalAmount = bill.TypicalAmount,
                MonthlyEquivalent = monthly
            });

            DateOnly? anchor = bill.NextDueDate;

            if (!anchor.HasValue && frequency == BillFrequencies.Monthly && bill.DueDay.HasValue)
            {
                DateOnly month = new(asOfDate.Year, asOfDate.Month, 1);
                DateOnly due = new(month.Year, month.Month, Math.Clamp(bill.DueDay.Value, 1, DateTime.DaysInMonth(month.Year, month.Month)));
                anchor = BillSchedule.NextOnOrAfter(due, frequency, asOfDate, bill.DueDay);
                warnings.Add($"'{bill.BillName}' uses its due day; earlier installments are assumed paid.");
            }

            if (!anchor.HasValue)
            {
                unscheduledMonthly += monthly;
                warnings.Add($"'{bill.BillName}' has no due-date anchor; its monthly equivalent is spread evenly through each projection month.");
                continue;
            }

            DateOnly originalAnchor = anchor.Value;
            int index = 0;

            if (originalAnchor < asOfDate)
            {
                scheduledExpenses[asOfDate] = scheduledExpenses.GetValueOrDefault(asOfDate) + bill.TypicalAmount;
                warnings.Add($"'{bill.BillName}' has an overdue next due date. One installment is reserved today; verify any additional arrears.");
            }

            DateOnly dueDate = originalAnchor;

            while (dueDate < endDate)
            {
                if (dueDate >= asOfDate)
                {
                    scheduledExpenses[dueDate] = scheduledExpenses.GetValueOrDefault(dueDate) + bill.TypicalAmount;
                }

                dueDate = BillSchedule.Occurrence(originalAnchor, frequency, ++index, bill.DueDay);
            }
        }

        result.MonthlyRequiredExpenses = result.IncludedBills.Sum(item => item.MonthlyEquivalent);
        decimal balance = result.StartingAvailableBalance;

        if (result.HasKnownBalance && balance <= 0M)
        {
            result.EstimatedDepletionDate = asOfDate;
        }

        for (int index = 0; index < projectionMonths; index++)
        {
            DateOnly periodStart = asOfDate.AddMonths(index);
            DateOnly periodEnd = asOfDate.AddMonths(index + 1);
            int days = periodEnd.DayNumber - periodStart.DayNumber;
            decimal starting = balance;
            decimal scheduled = 0M;

            for (DateOnly day = periodStart; day < periodEnd; day = day.AddDays(1))
            {
                decimal billAmount = scheduledExpenses.GetValueOrDefault(day);
                scheduled += billAmount;
                balance += (monthlyIncome - unscheduledMonthly) / days - billAmount;

                if (result.HasKnownBalance && !result.EstimatedDepletionDate.HasValue && decimal.Round(balance, 2, MidpointRounding.AwayFromZero) <= 0M)
                {
                    result.EstimatedDepletionDate = day;
                }
            }

            balance = decimal.Round(starting + monthlyIncome - unscheduledMonthly - scheduled, 2, MidpointRounding.AwayFromZero);

            result.ProjectionPoints.Add(new RunwayProjectionPoint
            {
                ProjectionDate = periodEnd,
                StartingBalance = starting,
                MoneyIn = monthlyIncome,
                RequiredExpenses = unscheduledMonthly + scheduled,
                EndingBalance = balance
            });
        }

        result.ProjectedEndingBalance = balance;

        result.EstimatedMonthsRemaining = result.EstimatedDepletionDate.HasValue
            ? decimal.Round(Math.Max(0, result.EstimatedDepletionDate.Value.DayNumber - asOfDate.DayNumber) / (365.2425M / 12M), 1, MidpointRounding.AwayFromZero)
            : projectionMonths;

        warnings.Insert(0, "Available balance takes precedence over current balance; overdrafts reduce cash. Monthly income is spread evenly because pay dates are not supplied.");
        result.Assumptions = warnings;
        return result;
    }

    public static bool IncludesAccount(Account account, string scenario)
    {
        if (AccountClassification.IsLiability(account.AccountType))
        {
            return false;
        }

        if (AccountClassification.IsCash(account.AccountType) && !AccountClassification.IsWallet(account.AccountType))
        {
            return true;
        }

        if (string.Equals(scenario, "Extended", StringComparison.OrdinalIgnoreCase))
        {
            return AccountClassification.IsWallet(account.AccountType);
        }

        if (string.Equals(scenario, "Emergency", StringComparison.OrdinalIgnoreCase))
        {
            return AccountClassification.IsWallet(account.AccountType) || AccountClassification.IsInvestment(account.AccountType);
        }

        return false;
    }
}
