namespace PersonalFinanceManager.Core.Imports;

public static class ImportClassification
{
    public const string UnclassifiedCredit = "Unclassified Credit";
    public const string UnclassifiedDebit = "Unclassified Debit";

    public static bool RequiresReview(string? type) => string.Equals(type, UnclassifiedCredit, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, UnclassifiedDebit, StringComparison.OrdinalIgnoreCase);

    public static bool IsExplicitIncome(string? type) => type?.Trim().ToUpperInvariant() is "DEPOSIT" or "DIRECT DEPOSIT" or "INTEREST" or "DIVIDEND";
}
