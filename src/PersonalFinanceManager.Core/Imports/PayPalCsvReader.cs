using System.Globalization;
using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Core.Imports;

internal static class PayPalCsvReader
{
    private static readonly HashSet<string> _postedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed",
        "Processed",
        "Reversed",
        "Refunded",
        "Partially refunded"
    };

    private static readonly HashSet<string> _unpostedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending",
        "Processing",
        "Created",
        "Unverified",
        "Active",
        "Denied",
        "Expired",
        "Voided",
        "Canceled",
        "Cancelled",
        "Error"
    };

    private static readonly HashSet<string> _paymentDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "General Payment", "Payment Sent", "Payment Received", "Payment", "Mass Payment", "Subscription Payment",
        "PreApproved Payment", "PreApproved Payment Bill User Payment", "Preapproved Payment From A User For A Recurring Bill",
        "eBay Auction Payment", "Credit Card Payment", "Express Checkout Payment", "Website Payment",
        "Standard Web Checkout Payment", "Postage Payment", "Gift Certificate Payment (Purchase)",
        "Third-party Auction Payment", "Mobile Payment", "Virtual Terminal Payment", "Donation Payment", "PoS Payment",
        "Generic Instrument Funded Payment", "Business Payment"
    };

    private static readonly HashSet<string> _transferDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bank Deposit to PayPal Account",
        "Bank Deposit to PP Account",
        "Transfer from Bank",
        "Transfer to Bank",
        "General Withdrawal - Bank Account",
        "General Withdrawal – Bank Account",
        "General Withdrawal — Bank Account",
        "Withdrawal to Bank Account",
        "General Withdrawal",
        "Manual Withdrawal",
        "Auto-sweep",
        "Bank Deposit",
        "PayPal Balance Manager Funding of PayPal Account",
        "ACH Funding for Funds Recovery from Account Balance",
        "Electronic Funds Transfer Funding",
        "General Credit Card Deposit",
        "Credit Card Deposit for Negative PayPal Account Balance",
        "General Credit Card Withdrawal",
        "Reversal of ACH Withdrawal Transaction",
        "Reversal of ACH Deposit",
        "General Currency Conversion",
        "Currency Conversion",
        "User-initiated Currency Conversion",
        "Conversion to Cover Negative Balance",
        "General Intraaccount Transfer",
        "Consolidation Transfer",
        "General Withdrawal to Non-bank Institution",
        "PayPal Buyer Credit Payment Funding",
        "BML Credit - Transfer from BML",
        "Buyer Credit Payment Withdrawal - Transfer to BML"
    };

    private static readonly HashSet<string> _refundDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Refund",
        "Payment Refund",
        "General Payment Refund",
        "Mass Payment Refund",
        "Payment Refund Referenced to an Authorization Transaction",
        "Reversal of Debit Card Transaction"
    };

    private static readonly HashSet<string> _feeDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "General Non-payment-related Fee",
        "Website Payments Pro Account Monthly Fee",
        "Fee for Foreign ACH Withdrawal",
        "Fee for WorldLink Check Withdrawal",
        "Fee for Mass Pay Request",
        "Chargeback Fee",
        "Payment Fee",
        "Partner Fee",
        "Dispute Fee",
        "Custody Fee",
        "Bank Return Fee",
        "Campaign Fee",
        "Gross Billed Fees",
        "Fee Refund",
        "Fee Reversal"
    };

    public static ParseResult Parse(string text, bool activityDownload)
    {
        ParseResult result = new() { SourceAccountType = "PayPal Wallet", Currency = "USD" };
        result.Warnings.Add(new ParseError(0, "This PayPal CSV does not identify the owning account. Select the matching PayPal wallet; counterparty emails and bank details are not the wallet's account identity."));
        IReadOnlyList<CsvRecord> records;

        try
        {
            records = new CsvRecordReader().Read(text).Where(record => record.Fields.Any(field => !string.IsNullOrWhiteSpace(field))).ToArray();
        }
        catch (FormatException exception)
        {
            result.Errors.Add(new ParseError(1, exception.Message));
            return result;
        }

        if (records.Count == 0)
        {
            result.Errors.Add(new ParseError(1, "The file is empty."));
            return result;
        }

        Dictionary<string, int> headers = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < records[0].Fields.Count; index++)
        {
            string header = records[0].Fields[index].Trim().TrimStart('\uFEFF');

            if (!headers.TryAdd(header, index))
            {
                result.Errors.Add(new ParseError(records[0].PhysicalStartLine, $"The header '{header}' occurs more than once."));
                return result;
            }
        }

        string[] required = activityDownload
            ? ["Date", "Time", "TimeZone", "Name", "Type", "Status", "Currency", "Gross", "Fee", "Net", "Transaction ID", "Balance Impact"]
            : ["Date", "Time", "Time Zone", "Description", "Currency", "Gross", "Fee", "Net", "Balance", "Transaction ID", "Name"];

        foreach (string name in required.Where(name => !headers.ContainsKey(name)))
        {
            result.Errors.Add(new ParseError(records[0].PhysicalStartLine, $"Missing '{name}'. Use the English US PayPal {(activityDownload ? "Activity Download CSV with Balance Impact selected" : "monthly/custom statement CSV, including its header row")}."));
        }

        if (result.Errors.Count != 0)
        {
            return result;
        }

        string Get(CsvRecord row, string name) => headers.TryGetValue(name, out int index) && index < row.Fields.Count ? row.Fields[index].Trim() : string.Empty;
        HashSet<string> sourceIds = records.Skip(1).Select(row => Get(row, "Transaction ID")).Where(id => id.Length != 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (CsvRecord row in records.Skip(1))
        {
            if (ImportAmountParser.TryParse(Get(row, "Fee"), out decimal fee) && fee != 0 && sourceIds.Contains(Get(row, "Transaction ID") + ":fee"))
            {
                result.Errors.Add(new ParseError(row.PhysicalStartLine, "A source Transaction ID conflicts with a derived fee identifier. No rows were imported; resolve the conflicting IDs in the source export."));
            }
        }

        if (result.Errors.Count != 0)
        {
            return result;
        }

        Dictionary<string, string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (CsvRecord row in records.Skip(1))
        {
            if (row.Fields.Count != records[0].Fields.Count)
            {
                result.Errors.Add(new ParseError(row.PhysicalStartLine, "The row does not have the same number of columns as the header."));
                continue;
            }

            string Field(string name)
            {
                return Get(row, name);
            }

            if (activityDownload)
            {
                string impact = Field("Balance Impact");
                string status = Field("Status");

                if (impact.Equals("Memo", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add(new ParseError(row.PhysicalStartLine, "Skipped a Memo entry: PayPal marks it as non-balance-impacting activity."));
                    continue;
                }

                if (!impact.Equals("Debit", StringComparison.OrdinalIgnoreCase) && !impact.Equals("Credit", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(new ParseError(row.PhysicalStartLine, "Balance Impact must be Debit, Credit, or Memo; the ledger direction cannot be inferred."));
                    continue;
                }

                if (_unpostedStatuses.Contains(status))
                {
                    result.Warnings.Add(new ParseError(row.PhysicalStartLine, $"Skipped status '{status}'. Import the final posted activity after PayPal resolves it."));
                    continue;
                }

                if (!_postedStatuses.Contains(status))
                {
                    result.Errors.Add(new ParseError(row.PhysicalStartLine, $"Unsupported activity status '{status}'. Export posted balance-affecting transactions."));
                    continue;
                }
            }

            if (!Field("Currency").Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add(new ParseError(row.PhysicalStartLine, "Only USD PayPal activity is supported. Export the USD balance separately; currency conversion is not performed."));
                continue;
            }

            if (!DateOnly.TryParseExact(Field("Date"), ["MM/dd/yyyy", "M/d/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date) ||
                !TimeOnly.TryParseExact(Field("Time"), ["HH:mm:ss", "H:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time) ||
                string.IsNullOrWhiteSpace(Field(activityDownload ? "TimeZone" : "Time Zone")))
            {
                result.Errors.Add(new ParseError(row.PhysicalStartLine, "Use US completion dates (MM/dd/yyyy), time HH:mm:ss, and the source time zone. Dates are retained in the statement's local time."));
                continue;
            }

            string feeText = Field("Fee");

            if (!ImportAmountParser.TryParse(Field("Gross"), out decimal gross) ||
                !ImportAmountParser.TryParse(string.IsNullOrWhiteSpace(feeText) ? "0" : feeText, out decimal fee) ||
                !ImportAmountParser.TryParse(Field("Net"), out decimal net) ||
                (gross + fee != net && gross - fee != net))
            {
                result.Errors.Add(new ParseError(row.PhysicalStartLine, "Gross, Fee, and Net must be valid USD amounts with no more than two decimal places, and Gross plus the signed fee must reconcile to Net."));
                continue;
            }

            if (activityDownload && ((Field("Balance Impact").Equals("Debit", StringComparison.OrdinalIgnoreCase) && net > 0) ||
                (Field("Balance Impact").Equals("Credit", StringComparison.OrdinalIgnoreCase) && net < 0)))
            {
                result.Errors.Add(new ParseError(row.PhysicalStartLine, "The signed Net amount conflicts with Balance Impact."));
                continue;
            }

            if ((!activityDownload || Field("Balance").Length != 0) && !ImportAmountParser.TryParse(Field("Balance"), out _))
            {
                result.Errors.Add(new ParseError(row.PhysicalStartLine, "Balance must be a valid USD amount."));
                continue;
            }

            string id = Field("Transaction ID");
            string sourceType = Field(activityDownload ? "Type" : "Description");
            string counterparty = Field("Name");
            decimal feeMovement = net - gross;

            if (id.Length == 0 || id.Length > (feeMovement == 0 ? 100 : 96) || sourceType.Length == 0)
            {
                result.Errors.Add(new ParseError(row.PhysicalStartLine, "A source Transaction ID and transaction type are required. IDs cannot exceed 100 characters (96 when a separate fee is present)."));
                continue;
            }

            string description = counterparty.Length == 0 ? sourceType : sourceType + ": " + counterparty;

            string memo = string.Join("; ", new[]
            {
                "PayPal type=" + sourceType, "Gross=" + gross.ToString("0.00", CultureInfo.InvariantCulture),
                "Fee=" + fee.ToString("0.00", CultureInfo.InvariantCulture), "Net=" + net.ToString("0.00", CultureInfo.InvariantCulture),
                Value("Time zone", Field(activityDownload ? "TimeZone" : "Time Zone")), Value("Status", Field("Status")),
                Value("T-code", Field("Transaction Event Code")), Value("Reference", Field("Reference Txn ID")),
                Value("Payment source", Field("Payment Source")), Value("Bank", Field("Bank Name")), Value("Bank account", Field("Bank Account")),
                Value("From", Field("From Email Address")), Value("To", Field("To Email Address")),
                Value("Subject", Field("Subject")), Value("Note", Field("Note"))
            }.Where(value => value is not null));

            if (description.Length > 1000 || counterparty.Length > 200 || memo.Length > 1000)
            {
                result.Errors.Add(new ParseError(row.PhysicalStartLine, "The source description, counterparty name, or combined source metadata exceeds the supported length. Shorten the optional note/subject fields without changing the financial values."));
                continue;
            }

            string signature = string.Join("\u001f", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), time.ToString("HH:mm:ss", CultureInfo.InvariantCulture), description, memo);

            if (seen.TryGetValue(id, out string? previous))
            {
                if (previous != signature)
                {
                    result.Errors.Add(new ParseError(row.PhysicalStartLine, "The same Transaction ID has conflicting financial values or metadata. Resolve the conflict before importing this row."));
                }
                else
                {
                    result.Warnings.Add(new ParseError(row.PhysicalStartLine, "Skipped a repeated row with the same PayPal Transaction ID and identical values."));
                }

                continue;
            }

            seen.Add(id, signature);
            string type = Classify(sourceType, Field("Transaction Event Code"), gross);

            if (ImportClassification.RequiresReview(type))
            {
                result.Warnings.Add(new ParseError(row.PhysicalStartLine, $"PayPal type '{sourceType}' requires classification. Its signed amount is retained as {type}; review whether it is income, a refund, a fee, or a transfer before relying on financial estimates."));
            }

            result.Rows.Add(Create(row.PhysicalStartLine, date, time, gross, type, description, id, counterparty, memo, type is "Fee" ||
                IsFee(sourceType, Field("Transaction Event Code")) ? "Fees" : null));

            if (feeMovement != 0)
            {
                result.Rows.Add(Create(row.PhysicalStartLine, date, time, feeMovement, feeMovement < 0
                    ? "Fee"
                    : "Refund", "PayPal fee: " + (counterparty.Length == 0 ? sourceType : counterparty), id + ":fee", "PayPal", memo, "Fees"));
            }
        }

        return result;
    }

    private static ParsedTransaction Create(int row, DateOnly date, TimeOnly time, decimal amount, string type, string description,
        string id, string merchant, string memo, string? category) => new()
        {
            RowNumber = row,
            Date = date,
            PostingDate = date,
            Time = time,
            Amount = amount,
            Type = type,
            Description = description,
            ExternalTransactionId = id,
            Merchant = string.IsNullOrWhiteSpace(merchant) ? null : merchant,
            Memo = memo,
            Currency = "USD",
            SourceCategory = category,
            SourceCategoryType = category
        };

    private static string? Value(string label, string value) => string.IsNullOrWhiteSpace(value) ? null : label + "=" + value;

    private static string Classify(string description, string eventCode, decimal gross)
    {
        string type = Regex.Replace(description.Trim().TrimEnd(':'), @"\s+", " ");
        string code = eventCode.Trim().ToUpperInvariant();

        if (code.Length != 0)
        {
            if (code is "T0000" or "T0001" or "T0002" or "T0003" or "T0004" or "T0005" or "T0006" or "T0007" or "T0008"
                or "T0009" or "T0010" or "T0011" or "T0012" or "T0013" or "T0015" or "T0018" or "T0019" or "T0022" or "T0023")
            {
                return gross > 0 ? "Deposit" : "Purchase";
            }

            if (code is "T0300" or "T0301" or "T0302" or "T0303" or "T0400" or "T0401" or "T0402" or "T0403"
                or "T0600" or "T0700" or "T0701" or "T1101" or "T1104" or "T1600" or "T1601" or "T1603" or "T1700"
                or "T1701" or "T1801" or "T2000" or "T2001" or "T2002" or "T2003" or "T0200" or "T0201" or "T0202")
            {
                return "Transfer";
            }

            if (code is "T1602" or "T1800")
            {
                return "Buyer Credit Payment";
            }

            if (code is "T1102" or "T1107" or "T1115" or "T1120")
            {
                return gross >= 0 ? "Refund" : ImportClassification.UnclassifiedDebit;
            }

            if (IsFee(type, code))
            {
                return gross > 0 ? "Refund" : "Fee";
            }

            if (code is "T0500" or "T0501" or "T0503")
            {
                return gross > 0 ? "Refund" : "Purchase";
            }

            if (code is "T0801" or "T0807" or "T0808" or "T0014")
            {
                return gross >= 0 ? "Refund" : ImportClassification.UnclassifiedDebit;
            }

            return gross > 0 ? ImportClassification.UnclassifiedCredit : ImportClassification.UnclassifiedDebit;
        }

        if (_paymentDescriptions.Contains(type))
        {
            return gross > 0 ? "Deposit" : "Purchase";
        }

        if (_transferDescriptions.Contains(type))
        {
            return "Transfer";
        }

        if (type.Equals("Buyer Credit Payment", StringComparison.OrdinalIgnoreCase) || type.Equals("General Buyer Credit Payment", StringComparison.OrdinalIgnoreCase))
        {
            return "Buyer Credit Payment";
        }

        if (_refundDescriptions.Contains(type))
        {
            return gross >= 0 ? "Refund" : ImportClassification.UnclassifiedDebit;
        }

        if (IsFee(type, code))
        {
            return gross > 0 ? "Refund" : "Fee";
        }

        if (type.Equals("General PayPal Debit Card Transaction", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("Virtual PayPal Debit Card Transaction", StringComparison.OrdinalIgnoreCase))
        {
            return gross > 0 ? "Refund" : "Purchase";
        }

        return gross > 0 ? ImportClassification.UnclassifiedCredit : ImportClassification.UnclassifiedDebit;
    }

    private static bool IsFee(string type, string code)
    {
        return code
            is "T0100"
            or "T0101"
            or "T0102"
            or "T0103"
            or "T0104"
            or "T0106"
            or "T0107"
            or "T0111"
            or "T0112"
            or "T0113"
            or "T0114"
            or "T0115"
            or "T0116"
            or "T0117"
            or "T0118"
            or "T0121"
            or "T1108"
            or "T1109" ||
            (code.Length == 0 && _feeDescriptions.Contains(type));
    }
}
