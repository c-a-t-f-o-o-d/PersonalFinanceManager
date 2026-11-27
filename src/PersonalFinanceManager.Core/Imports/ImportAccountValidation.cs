using PersonalFinanceManager.Core.Models;
using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Core.Imports;

public static class ImportAccountValidation
{
    public static void Validate(Account account, string profileCode, ParseResult parse)
    {
        if ((!string.IsNullOrWhiteSpace(parse.Currency) &&
            !string.Equals(parse.Currency, "USD", StringComparison.OrdinalIgnoreCase)) ||
            parse.Rows.Any(row => !string.Equals(row.Currency, "USD", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("This account ledger supports USD only. Convert foreign-currency activity explicitly before importing; currencies cannot be combined as equal dollars.");
        }

        bool requiresLiability = profileCode is ImportProfileCodes.AppleCardCsv or
            ImportProfileCodes.ChaseCreditCardCsv or
            ImportProfileCodes.PayPalSmartConnectCsv or
            ImportProfileCodes.CreditAccountCsv ||
            string.Equals(parse.SourceAccountType, "CREDITCARD", StringComparison.OrdinalIgnoreCase);

        if (requiresLiability && !AccountClassification.IsLiability(account.AccountType))
        {
            throw new InvalidOperationException("This card/credit statement must be imported into its liability account, not the bank account that pays it.");
        }

        if (profileCode is ImportProfileCodes.PayPalStatement or
            ImportProfileCodes.PayPalActivityCsv or
            ImportProfileCodes.PayPalBalanceStatementCsv &&
            !AccountClassification.IsWallet(account.AccountType))
        {
            throw new InvalidOperationException("Import wallet activity into its wallet account. Import credit statements into their separate liability account.");
        }

        bool requiresBank = profileCode is ImportProfileCodes.AllyCheckingCsv or
            ImportProfileCodes.RegionsBankCsv ||
            parse.SourceAccountType?.ToUpperInvariant() is "BANK" or "CHECKING" or "SAVINGS" or "MONEYMRKT";

        if (requiresBank && (!AccountClassification.IsCash(account.AccountType) || AccountClassification.IsWallet(account.AccountType)))
        {
            throw new InvalidOperationException("Select the checking or savings account shown on this bank export.");
        }

        string selectedType = account.AccountType.Replace('_', ' ').Replace('-', ' ').ToUpperInvariant();

        bool subtypeMismatch = parse.SourceAccountType?.ToUpperInvariant() switch
        {
            "CHECKING" => !selectedType.Contains("CHECKING", StringComparison.Ordinal),
            "SAVINGS" => !selectedType.Contains("SAVINGS", StringComparison.Ordinal),
            "MONEYMRKT" => !selectedType.Contains("MONEY MARKET", StringComparison.Ordinal),
            _ => false
        };

        if (subtypeMismatch)
        {
            throw new InvalidOperationException("The statement's bank account type conflicts with the selected checking, savings, or money-market account.");
        }

        if (string.Equals(parse.SourceAccountType, "CASH", StringComparison.OrdinalIgnoreCase) && !AccountClassification.IsCash(account.AccountType))
        {
            throw new InvalidOperationException("Select a cash account for this cash activity export.");
        }

        string[] identities = parse.Rows
            .Select(row => row.SourceAccountIdentity?.Trim())
            .Append(parse.SourceAccountIdentity?.Trim())
            .OfType<string>()
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (identities.Any(identity => !Matches(account, identity)))
        {
            throw new InvalidOperationException("The file contains an account identifier that does not match the selected account. Split mixed-account files and select the matching account.");
        }

        if (identities.Length == 0 && parse.Rows.Count > 0)
        {
            parse.Warnings.Add(new ParseError(0, "The file has no explicit account identifier. Verify the selected account against the statement before accepting the import."));
        }
    }

    private static bool Matches(Account account, string identity)
    {
        if (identity.Equals(account.AccountNickname, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(account.AccountNumber) &&
            identity.Equals(account.AccountNumber, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(account.AccountNumberLastFour))
        {
            return false;
        }

        Match suffix = Regex.Match(identity, @"(?:[*xX#]+\s*|^)(?<lastFour>\d{4})$", RegexOptions.CultureInvariant);

        if (!suffix.Success || suffix.Groups["lastFour"].Value != account.AccountNumberLastFour)
        {
            return false;
        }

        if (identity.Contains("SAVINGS", StringComparison.OrdinalIgnoreCase) && !account.AccountType.Contains("SAVINGS", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if ((identity.Contains("CHECKING", StringComparison.OrdinalIgnoreCase) ||
            identity.Contains("CHECK", StringComparison.OrdinalIgnoreCase)) &&
            !account.AccountType.Contains("CHECKING", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
