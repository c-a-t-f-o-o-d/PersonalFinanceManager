namespace PersonalFinanceManager.Core.Models;

public static class AccountClassification
{
    public static bool IsLiability(string? accountType)
    {
        string type = Normalize(accountType);
        return type == "CREDIT" || type.Contains("CREDIT CARD", StringComparison.Ordinal)
            || type.Contains("CREDITCARD", StringComparison.Ordinal)
            || type.Contains("CREDIT LINE", StringComparison.Ordinal)
            || type.Contains("PAYPAL CREDIT", StringComparison.Ordinal)
            || type.Contains("LOAN", StringComparison.Ordinal)
            || type.Contains("MORTGAGE", StringComparison.Ordinal)
            || type.Contains("SMART CONNECT", StringComparison.Ordinal)
            || type.Contains("SMARTCONNECT", StringComparison.Ordinal)
            || type.Contains("LINE OF CREDIT", StringComparison.Ordinal);
    }

    public static bool IsWallet(string? accountType)
    {
        string type = Normalize(accountType);
        return !IsLiability(type) && (type.Contains("PAYPAL", StringComparison.Ordinal)
            || type.Contains("WALLET", StringComparison.Ordinal)
            || type.Contains("VENMO", StringComparison.Ordinal)
            || type == "APPLE CASH");
    }

    public static bool IsInvestment(string? accountType)
    {
        string type = Normalize(accountType);
        return !IsLiability(type) && (type.Contains("INVESTMENT", StringComparison.Ordinal)
            || type.Contains("BROKERAGE", StringComparison.Ordinal)
            || type.Contains("401", StringComparison.Ordinal)
            || type == "IRA" || type.EndsWith(" IRA", StringComparison.Ordinal)
            || type.Contains("RETIREMENT", StringComparison.Ordinal));
    }

    public static bool IsCash(string? accountType)
    {
        string type = Normalize(accountType);
        return !IsLiability(type) && !IsInvestment(type) && (type.Contains("CHECKING", StringComparison.Ordinal)
            || type.Contains("SAVINGS", StringComparison.Ordinal)
            || type == "CASH" || type.Contains("MONEY MARKET", StringComparison.Ordinal)
            || IsWallet(type));
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().Replace('_', ' ').Replace('-', ' ').ToUpperInvariant();
}
