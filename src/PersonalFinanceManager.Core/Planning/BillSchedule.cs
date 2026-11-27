using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Core.Planning;

public static class BillSchedule
{
    public static string NormalizeFrequency(string? frequency) => (frequency ?? string.Empty).Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant() switch
    {
        "WEEKLY" => BillFrequencies.Weekly,
        "BIWEEKLY" => BillFrequencies.Biweekly,
        "MONTHLY" => BillFrequencies.Monthly,
        "QUARTERLY" => BillFrequencies.Quarterly,
        "SEMIANNUAL" or "SEMIANNUALLY" or "TWICE YEARLY" => BillFrequencies.Semiannual,
        "ANNUAL" or "ANNUALLY" or "YEARLY" => BillFrequencies.Annual,
        _ => throw new ArgumentException("Choose a supported bill frequency.", nameof(frequency))
    };

    public static decimal MonthlyEquivalent(decimal amount, string? frequency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        decimal monthly = NormalizeFrequency(frequency) switch
        {
            BillFrequencies.Weekly => amount * 52M / 12M,
            BillFrequencies.Biweekly => amount * 26M / 12M,
            BillFrequencies.Quarterly => amount / 3M,
            BillFrequencies.Semiannual => amount / 6M,
            BillFrequencies.Annual => amount / 12M,
            _ => amount
        };

        return decimal.Round(monthly, 2, MidpointRounding.AwayFromZero);
    }

    public static bool IsRequiredExpense(RecurringBill bill)
    {
        return bill.IsActive && bill.IsRequired &&
            !string.Equals(bill.RequirementType?.Trim(), BillRequirementTypes.Ignore, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(bill.RequirementType?.Trim(), BillRequirementTypes.Transfer, StringComparison.OrdinalIgnoreCase);
    }

    public static DateOnly Occurrence(DateOnly anchor, string frequency, int occurrenceIndex, int? dueDay = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(occurrenceIndex);
        string cadence = NormalizeFrequency(frequency);

        if (cadence is BillFrequencies.Weekly or BillFrequencies.Biweekly)
        {
            return anchor.AddDays(occurrenceIndex * (cadence == BillFrequencies.Weekly ? 7 : 14));
        }

        int interval = cadence switch { BillFrequencies.Quarterly => 3, BillFrequencies.Semiannual => 6, BillFrequencies.Annual => 12, _ => 1 };
        DateOnly month = new DateOnly(anchor.Year, anchor.Month, 1).AddMonths(occurrenceIndex * interval);
        int day = Math.Clamp(dueDay ?? anchor.Day, 1, DateTime.DaysInMonth(month.Year, month.Month));
        return new DateOnly(month.Year, month.Month, day);
    }

    public static DateOnly NextOnOrAfter(DateOnly anchor, string frequency, DateOnly onOrAfter, int? dueDay = null)
    {
        string cadence = NormalizeFrequency(frequency);

        if (onOrAfter <= anchor)
        {
            return anchor;
        }

        int index;

        if (cadence is BillFrequencies.Weekly or BillFrequencies.Biweekly)
        {
            int interval = cadence == BillFrequencies.Weekly ? 7 : 14;
            index = (onOrAfter.DayNumber - anchor.DayNumber + interval - 1) / interval;
        }
        else
        {
            int interval = cadence switch { BillFrequencies.Quarterly => 3, BillFrequencies.Semiannual => 6, BillFrequencies.Annual => 12, _ => 1 };
            index = Math.Max(0, ((onOrAfter.Year - anchor.Year) * 12 + onOrAfter.Month - anchor.Month) / interval);
        }

        DateOnly next = index == 0 ? anchor : Occurrence(anchor, cadence, index, dueDay);

        while (next < onOrAfter)
        {
            next = Occurrence(anchor, cadence, ++index, dueDay);
        }
        return next;
    }

    public static string? InferFrequency(IEnumerable<DateOnly> transactionDates)
    {
        DateOnly[] dates = transactionDates.Order().ToArray();

        if (dates.Length < 3 || dates.Distinct().Count() != dates.Length)
        {
            return null;
        }

        int[] gaps = dates.Zip(dates.Skip(1), (first, second) => second.DayNumber - first.DayNumber).ToArray();

        if (gaps.All(gap => gap is >= 5 and <= 9))
        {
            return BillFrequencies.Weekly;
        }

        if (gaps.All(gap => gap is >= 12 and <= 16))
        {
            return BillFrequencies.Biweekly;
        }

        foreach ((string frequency, int months) in new[] { (BillFrequencies.Monthly, 1), (BillFrequencies.Quarterly, 3), (BillFrequencies.Semiannual, 6), (BillFrequencies.Annual, 12) })
        {
            if (dates.Zip(dates.Skip(1), (first, second) => Math.Abs(second.DayNumber - first.AddMonths(months).DayNumber) <= 4).All(match => match))
            {
                return frequency;
            }
        }

        return null;
    }
}
