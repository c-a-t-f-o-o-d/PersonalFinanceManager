namespace PersonalFinanceManager.Core.Reporting;

public sealed record ReportPeriod(DateOnly? FromDate, DateOnly? ToDate, bool HasObservedTransactions)
{
    public int DayCount => FromDate.HasValue && ToDate.HasValue ? ToDate.Value.DayNumber - FromDate.Value.DayNumber + 1 : 0;
    public int MonthCount => FromDate.HasValue && ToDate.HasValue ? (ToDate.Value.Year - FromDate.Value.Year) * 12 + ToDate.Value.Month - FromDate.Value.Month + 1 : 0;
    public decimal MonthEquivalent
    {
        get
        {
            if (!FromDate.HasValue || !ToDate.HasValue) return 0M;
            decimal months = 0M;
            foreach (DateOnly month in Months())
            {
                int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
                DateOnly first = month < FromDate.Value ? FromDate.Value : month;
                DateOnly last = month.AddDays(daysInMonth - 1);
                if (last > ToDate.Value) last = ToDate.Value;
                months += (last.DayNumber - first.DayNumber + 1M) / daysInMonth;
            }
            return months;
        }
    }

    public string CoverageNote => HasObservedTransactions
        ? "Averages use the selected calendar interval, including zero-activity months. Import coverage is not verified; missing statement periods may change the estimate."
        : "No transactions are available for this interval. Income and historical spending coverage are unknown.";

    public IEnumerable<DateOnly> Months()
    {
        if (!FromDate.HasValue || !ToDate.HasValue) yield break;
        DateOnly month = new(FromDate.Value.Year, FromDate.Value.Month, 1);
        while (month <= ToDate.Value)
        {
            yield return month;
            if (month.Year == 9999 && month.Month == 12) yield break;
            month = month.AddMonths(1);
        }
    }

    public static ReportPeriod Create(DateOnly? fromDate, DateOnly? toDate, IEnumerable<DateOnly> transactionDates)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ArgumentException("The start date must be on or before the end date.");

        DateOnly[] dates = transactionDates.Where(date => (!fromDate.HasValue || date >= fromDate) && (!toDate.HasValue || date <= toDate)).ToArray();
        if (dates.Length == 0) return new ReportPeriod(fromDate, toDate, false);

        DateOnly first = dates.Min();
        DateOnly last = dates.Max();
        return new ReportPeriod(fromDate ?? new DateOnly(first.Year, first.Month, 1),
            toDate ?? new DateOnly(last.Year, last.Month, DateTime.DaysInMonth(last.Year, last.Month)), true);
    }
}
