using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Core.Planning;

public static class RecurringBillMatcher
{
    public static bool SameBucket(ExpenseDetailItem item, RecurringBill bill)
    {
        return MatchesAccount(item.AccountId, bill) && Equal(item.Category, bill.Category) && Equal(item.SpendingClass, bill.SpendingClass);
    }

    public static bool Matches(ExpenseDetailItem item, RecurringBill bill)
    {
        return SameBucket(item, bill) && MatchesIdentity(item, bill);
    }

    public static bool MatchesIdentity(ExpenseDetailItem item, RecurringBill bill)
    {
        if (!MatchesAccount(item.AccountId, bill))
        {
            return false;
        }

        string[] descriptions = Descriptions(bill).ToArray();

        if (descriptions.Length > 0)
        {
            return descriptions.Any(description => Equal(item.Description, description));
        }

        return Payees(bill).Any(payee => Equal(item.Merchant, payee) || Equal(item.Description, payee));
    }

    public static bool MatchesStatementDescription(ExpenseDetailItem item, RecurringBill bill)
    {
        return MatchesAccount(item.AccountId, bill) && Descriptions(bill).Any(description => Equal(item.Description, description));
    }

    public static bool HasOverlappingIdentity(RecurringBill first, RecurringBill second)
    {
        if (!first.IsActive || !second.IsActive ||
            (first.AccountId.HasValue && second.AccountId.HasValue && first.AccountId != second.AccountId))
        {
            return false;
        }

        string[] firstDescriptions = Descriptions(first).ToArray();
        string[] secondDescriptions = Descriptions(second).ToArray();

        if (firstDescriptions.Length > 0 && secondDescriptions.Length > 0)
        {
            return Intersects(firstDescriptions, secondDescriptions);
        }

        if (firstDescriptions.Length == 0 && secondDescriptions.Length == 0)
        {
            return Intersects(Payees(first), Payees(second));
        }

        RecurringBill broadBill = firstDescriptions.Length == 0 ? first : second;
        RecurringBill specificBill = firstDescriptions.Length == 0 ? second : first;
        return Intersects(Payees(broadBill), Payees(specificBill).Concat(Descriptions(specificBill)));
    }

    public static IReadOnlyList<string> OverlapWarnings(IReadOnlyList<RecurringBill> bills)
    {
        List<string> warnings = new();

        for (int first = 0; first < bills.Count; first++)
        {
            for (int second = first + 1; second < bills.Count; second++)
            {
                if (HasOverlappingIdentity(bills[first], bills[second]))
                {
                    warnings.Add($"'{bills[first].BillName}' and '{bills[second].BillName}' have overlapping payment identities. Keep former and current names on one bill, or use distinct statement descriptions and accounts for separate obligations.");
                }
            }
        }

        return warnings;
    }

    public static void NormalizeAliases(RecurringBill bill)
    {
        bill.PayeeAliases = Normalize(bill.PayeeAliases, bill.Payee, 300, "Payee");
        bill.SourceDescriptionAliases = Normalize(bill.SourceDescriptionAliases, bill.SourceDescription, 1000, "Statement description");
    }

    public static void PreserveFormerNames(RecurringBill bill, string previousPayee, string? previousDescription,
        IReadOnlyList<string>? previousDescriptionAliases = null, bool descriptionConversionReviewed = false)
    {
        bool previouslyMatchedByPayee = string.IsNullOrWhiteSpace(previousDescription) &&
            (previousDescriptionAliases is null || previousDescriptionAliases.All(string.IsNullOrWhiteSpace));

        if (previouslyMatchedByPayee && Descriptions(bill).Any() &&
            (!descriptionConversionReviewed || string.IsNullOrWhiteSpace(bill.SourceDescription) ||
             !bill.SourceDescriptionAliases.Any(description => !string.IsNullOrWhiteSpace(description) && !Equal(description, bill.SourceDescription))))
        {
            throw new InvalidOperationException("This bill previously matched by payee. Keeping the old payee alone cannot preserve history after adding exact descriptions. Enter the current statement description and the former exact description(s) in Additional descriptions, then check 'I included the former exact statement descriptions'. For an intentional correction, clear 'Keep former names when replacing them'.");
        }

        bill.PayeeAliases = [.. bill.PayeeAliases, previousPayee];
        bill.SourceDescriptionAliases = [.. bill.SourceDescriptionAliases, previousDescription ?? string.Empty];
        NormalizeAliases(bill);
    }

    public static void AttachCandidate(RecurringBill bill, RecurringBillCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(candidate);

        if (!bill.IsActive)
        {
            throw new InvalidOperationException("Choose an active recurring bill.");
        }

        if (bill.AccountId != candidate.AccountId)
        {
            throw new InvalidOperationException("The candidate and bill must use the same account. Set the bill's account before attaching a statement description.");
        }

        if (!Equal(bill.Category, candidate.Category) || !Equal(bill.SpendingClass, candidate.SpendingClass))
        {
            throw new InvalidOperationException("The candidate and bill must use the same category and category type. Review the transaction classification first; adding an alias does not change it.");
        }

        if (!Descriptions(bill).Any())
        {
            throw new InvalidOperationException("First add the existing bill's exact statement description on its Matching tab. This preserves its historical payments when another description is attached.");
        }

        if (string.IsNullOrWhiteSpace(candidate.SourceDescription))
        {
            throw new InvalidOperationException("The candidate needs an exact statement description.");
        }

        bill.SourceDescriptionAliases = [.. bill.SourceDescriptionAliases, candidate.SourceDescription];
        NormalizeAliases(bill);
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string> values, string? primary, int maximumLength, string label)
    {
        string[] normalized = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
            .Where(value => !Equal(value, primary)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (normalized.Length > 50 || normalized.Any(value => value.Length > maximumLength))
        {
            throw new InvalidOperationException($"{label} aliases are limited to 50 entries of {maximumLength} characters each.");
        }

        return normalized;
    }

    private static IEnumerable<string> Descriptions(RecurringBill bill)
    {
        return bill.SourceDescriptionAliases.Prepend(bill.SourceDescription ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim());
    }

    private static IEnumerable<string> Payees(RecurringBill bill)
    {
        return bill.PayeeAliases.Prepend(bill.Payee).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim());
    }

    private static bool MatchesAccount(int accountId, RecurringBill bill)
    {
        return !bill.AccountId.HasValue || bill.AccountId == accountId;
    }

    private static bool Intersects(IEnumerable<string> first, IEnumerable<string> second)
    {
        return first.Intersect(second, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static bool Equal(string? first, string? second)
    {
        return string.Equals(first?.Trim(), second?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
