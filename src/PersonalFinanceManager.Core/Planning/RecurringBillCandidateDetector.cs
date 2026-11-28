using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Reporting;

namespace PersonalFinanceManager.Core.Planning;

public static class RecurringBillCandidateDetector
{
    public static IReadOnlyList<RecurringBillCandidate> Detect(ReportData data, int minimumTransactions, int minimumMonths)
    {
        FinancialReportCalculator calculator = new(data);

        return calculator.NetExpenses()
            .Where(row => row.Amount > 0M && !calculator.NeedsLinkReview(row.ImportedTransactionId) && !calculator.NeedsClassificationReview(row.ImportedTransactionId) && !string.IsNullOrWhiteSpace(row.Description))
            .GroupBy(row => (row.AccountId, Description: row.Description.Trim().ToUpperInvariant()))
            .Where(group => group.Count() >= Math.Max(2, minimumTransactions) && group.Select(row => (row.TransactionDate.Year, row.TransactionDate.Month)).Distinct().Count() >= Math.Max(2, minimumMonths))
            .Select(group =>
            {
                ExpenseDetailItem latest = group.OrderByDescending(row => row.TransactionDate).ThenByDescending(row => row.ImportedTransactionId).First();

                return new RecurringBillCandidate
                {
                    AccountId = latest.AccountId,
                    AccountNickname = latest.AccountNickname,
                    SourceDescription = latest.Description.Trim(),
                    Payee = string.IsNullOrWhiteSpace(latest.Merchant) ? latest.Description.Trim() : latest.Merchant.Trim(),
                    Category = latest.Category,
                    SpendingClass = latest.SpendingClass,
                    SuggestedRequirementType = SuggestRequirementType(latest.Category, latest.SpendingClass),
                    AverageAmount = decimal.Round(group.Average(row => row.Amount), 2, MidpointRounding.AwayFromZero),
                    MinimumAmount = group.Min(row => row.Amount),
                    MaximumAmount = group.Max(row => row.Amount),
                    FirstSeenDate = group.Min(row => row.TransactionDate),
                    LastSeenDate = latest.TransactionDate,
                    TransactionCount = group.Count(),
                    MonthCount = group.Select(row => (row.TransactionDate.Year, row.TransactionDate.Month)).Distinct().Count(),
                    SuggestedDueDay = latest.TransactionDate.Day,
                    SuggestedFrequency = BillSchedule.InferFrequency(group.Select(row => row.TransactionDate)),
                    AlreadyExists = data.Bills.Any(bill => bill.IsActive && bill.AccountId == latest.AccountId && string.Equals(bill.SourceDescription?.Trim(), latest.Description.Trim(), StringComparison.OrdinalIgnoreCase))
                };
            }).OrderByDescending(candidate => candidate.AverageAmount).ThenBy(candidate => candidate.Payee).ToArray();
    }

    public static RecurringBill CreateDraft(RecurringBillCandidate candidate, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        string? frequency = candidate.SuggestedFrequency;
        DateOnly? next = frequency is null ? null : BillSchedule.NextOnOrAfter(BillSchedule.Occurrence(candidate.LastSeenDate, frequency, 1, candidate.SuggestedDueDay), frequency, today, candidate.SuggestedDueDay);

        return new RecurringBill
        {
            AccountId = candidate.AccountId,
            BillName = candidate.Payee.Length > 200 ? candidate.Payee[..200] : candidate.Payee,
            Payee = candidate.Payee,
            Category = candidate.Category,
            SpendingClass = candidate.SpendingClass,
            RequirementType = candidate.SuggestedRequirementType,
            TypicalAmount = candidate.AverageAmount,
            DueDay = frequency is BillFrequencies.Monthly or BillFrequencies.Quarterly or BillFrequencies.Semiannual or BillFrequencies.Annual ? candidate.SuggestedDueDay : null,
            NextDueDate = next,
            Frequency = frequency ?? string.Empty,
            IsRequired = candidate.SuggestedRequirementType != BillRequirementTypes.Want,
            IsActive = true,
            SourceDescription = candidate.SourceDescription,
            FirstSeenDate = candidate.FirstSeenDate,
            LastSeenDate = candidate.LastSeenDate,
            TransactionCount = candidate.TransactionCount,
            Notes = frequency is null ? "Cadence is uncertain. Confirm the amount per occurrence, frequency, and next due date." : "Suggested from transaction history. Confirm the amount per occurrence, frequency, and next due date."
        };
    }

    private static string SuggestRequirementType(string? category, string? spendingClass)
    {
        string text = (category + " " + spendingClass).ToUpperInvariant();

        if (new[] { "LOAN", "CREDIT", "DEBT", "MORTGAGE" }.Any(value => text.Contains(value, StringComparison.Ordinal)))
        {
            return BillRequirementTypes.Debt;
        }

        if (new[] { "SAVING", "RETIREMENT", "INVEST" }.Any(value => text.Contains(value, StringComparison.Ordinal)))
        {
            return BillRequirementTypes.Savings;
        }

        if (new[] { "DINING", "ENTERTAINMENT", "SHOPPING", "RESTAURANT" }.Any(value => text.Contains(value, StringComparison.Ordinal)))
        {
            return BillRequirementTypes.Want;
        }

        return BillRequirementTypes.Need;
    }
}
