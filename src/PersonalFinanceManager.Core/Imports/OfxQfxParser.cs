using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PersonalFinanceManager.Core.Imports;

/// <summary>Imports one USD bank or credit-card statement without applying OFX server instructions.</summary>
public sealed class OfxQfxParser : ITransactionParser
{
    private static readonly Regex _datePattern = new(@"^(?<date>[0-9]{8})(?:(?<time>[0-9]{6})(?:\.(?<fraction>[0-9]{1,3}))?)?(?:\[(?<offset>[+-]?[0-9]{1,2}(?:\.[0-9]{1,2})?)(?::[A-Za-z0-9 _+\-]{1,32})?\])?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex _amountPattern = new(@"^[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly HashSet<string> _transactionTypes = new(StringComparer.Ordinal)
    {
        "CREDIT", "DEBIT", "INT", "DIV", "FEE", "SRVCHG", "DEP", "ATM", "POS", "XFER", "CHECK", "PAYMENT",
        "CASH", "DIRECTDEP", "DIRECTDEBIT", "REPEATPMT", "OTHER"
    };

    public string ProfileCode => ImportProfileCodes.OfxQfx;

    public async Task<ParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length > OfxDocumentReader.MaximumFileSize)
        {
            return Failure(0, "The OFX/QFX file exceeds the 16 MiB import limit. Export a smaller date range.");
        }

        byte[] bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);

        if (stream.ReadByte() != -1)
        {
            return Failure(0, "The OFX/QFX file changed while it was being read. Export the statement again.");
        }

        return Parse(() => OfxDocumentReader.Read(bytes));
    }

    public ParseResult ParseText(string text)
    {
        return Parse(() => OfxDocumentReader.Read(text));
    }

    private static ParseResult Parse(Func<XElement> read)
    {
        ParseResult result = new();

        try
        {
            ReadStatement(read(), result);
        }
        catch (OfxFormatException ex)
        {
            result.Rows.Clear();
            result.Errors.Add(new ParseError(ex.Line, ex.Message + " No transactions from this file were accepted."));
        }
        catch (XmlException ex)
        {
            result.Rows.Clear();
            result.Errors.Add(new ParseError(ex.LineNumber, "Invalid OFX XML: " + ex.Message + " No transactions from this file were accepted."));
        }
        catch (DecoderFallbackException)
        {
            result.Rows.Clear();
            result.Errors.Add(new ParseError(0, "The file contains characters that do not match its declared encoding. No transactions from this file were accepted."));
        }

        return result;
    }

    private static void ReadStatement(XElement root, ParseResult result)
    {
        if (root.Name.LocalName != "OFX")
        {
            throw Error(root, "An OFX statement root is required.");
        }

        string ns = root.Name.NamespaceName;

        if (ns is not ("" or "http://ofx.net/types/2003/04") || root.Descendants().Any(element => element.Name.NamespaceName != ns))
        {
            throw Error(root, "The statement uses an unsupported XML namespace.");
        }

        XElement[] elements = root.DescendantsAndSelf().ToArray();
        XElement? unsupported = elements.FirstOrDefault(element => element.Name.LocalName is "BANKTRANLISTP" or "STMTTRNP" or "CORRECTFITID" or "CORRECTACTION" or "REVERSALFITID" or "LOANPMTINFO" or "INV401KSOURCE");

        if (unsupported is not null)
        {
            throw Error(unsupported, "Pending transactions, correction/reversal instructions, and investment or loan breakdowns are not supported. Export a single posted bank or credit-card transaction list without these instructions.");
        }

        if (root.Elements().Any(element => element.Name.LocalName is not ("SIGNONMSGSRSV1" or "BANKMSGSRSV1" or "CREDITCARDMSGSRSV1")))
        {
            throw Error(root, "Only bank and credit-card statement responses are supported; investment, loan, and other OFX message sets cannot be imported.");
        }

        if (root.Elements().Count(element => element.Name.LocalName is "BANKMSGSRSV1" or "CREDITCARDMSGSRSV1") != 1)
        {
            throw Error(root, "Import exactly one bank or credit-card response per file.");
        }

        foreach (XElement status in elements.Where(element => element.Name.LocalName == "STATUS"))
        {
            if (Required(status, "CODE") != "0" || Required(status, "SEVERITY") != "INFO")
            {
                throw Error(status, "The financial institution returned an unsuccessful or incomplete statement status.");
            }
        }

        XElement[] statements = elements.Where(element => element.Name.LocalName is "STMTRS" or "CCSTMTRS").ToArray();

        if (statements.Length != 1)
        {
            throw Error(root, "Import exactly one account statement per file. Export each account separately.");
        }

        XElement statement = statements[0];
        bool creditCard = statement.Name.LocalName == "CCSTMTRS";
        string wrapperName = creditCard ? "CCSTMTTRNRS" : "STMTTRNRS";
        string messageName = creditCard ? "CREDITCARDMSGSRSV1" : "BANKMSGSRSV1";
        XElement? wrapper = statement.Parent;

        if (wrapper?.Name.LocalName != wrapperName ||
            wrapper.Parent?.Name.LocalName != messageName ||
            wrapper.Parent.Parent != root ||
            elements.Count(element => element.Name.LocalName is "STMTTRNRS" or "CCSTMTTRNRS") != 1)
        {
            throw Error(statement, "The statement response structure is incomplete or contains multiple accounts.");
        }

        if (wrapper.Parent.Elements().Count() != 1 ||
            wrapper.Elements().Any(element => element.Name.LocalName != statement.Name.LocalName && element.Name.LocalName is not ("TRNUID" or "STATUS" or "CLTCOOKIE")))
        {
            throw Error(wrapper, "The file contains additional response messages that this statement importer cannot apply.");
        }

        _ = One(wrapper, "STATUS", true);
        string currency = Required(statement, "CURDEF");

        if (currency != "USD")
        {
            throw Error(statement, "OFX/QFX imports require an explicit USD statement currency.");
        }

        foreach (XElement overrideCurrency in elements.Where(element => element.Name.LocalName == "CURRENCY"))
        {
            if (Required(overrideCurrency, "CURSYM") != "USD" || !TryNumber(Required(overrideCurrency, "CURRATE"), out decimal rate) || rate != 1M)
            {
                throw Error(overrideCurrency, "Foreign-currency amount overrides are not supported. Export amounts already converted to USD.");
            }
        }

        XElement account = One(statement, creditCard ? "CCACCTFROM" : "BANKACCTFROM", true)!;

        if (statement.Elements().Count(element => element.Name.LocalName is "BANKACCTFROM" or "CCACCTFROM") != 1)
        {
            throw Error(statement, "The statement must identify exactly one source account.");
        }

        string accountId = Required(account, "ACCTID", 100);
        string accountType = creditCard ? "CREDITCARD" : Required(account, "ACCTTYPE");

        if (!creditCard)
        {
            _ = Required(account, "BANKID", 100);

            if (accountType is not ("CHECKING" or "SAVINGS" or "MONEYMRKT"))
            {
                throw Error(account, "Supported bank account types are CHECKING, SAVINGS, and MONEYMRKT. Credit lines, certificates, and investment accounts need their own supported import format.");
            }
        }

        result.SourceAccountIdentity = accountId;
        result.SourceAccountType = accountType;
        result.Currency = currency;

        XElement list = One(statement, "BANKTRANLIST", true)!;
        OfxDate start = ReadDate(Required(list, "DTSTART"), list, "DTSTART");
        OfxDate end = ReadDate(Required(list, "DTEND"), list, "DTEND");

        if (start.Date > end.Date)
        {
            throw Error(list, "The statement transaction period ends before it starts.");
        }

        XElement[] transactions = list.Elements().Where(element => element.Name.LocalName == "STMTTRN").ToArray();

        if (elements.Count(element => element.Name.LocalName == "STMTTRN") != transactions.Length ||
            list.Elements().Any(element => element.Name.LocalName is not ("DTSTART" or "DTEND" or "STMTTRN")))
        {
            throw Error(list, "The statement contains transaction data outside the supported posted transaction list.");
        }

        Dictionary<string, ParsedTransaction> ids = new(StringComparer.OrdinalIgnoreCase);

        foreach (XElement transaction in transactions)
        {
            ParsedTransaction row = ReadTransaction(transaction, creditCard, accountId, result);
            string id = row.ExternalTransactionId!;

            if (ids.TryGetValue(id, out ParsedTransaction? previous) &&
                (previous.ExternalTransactionId != id ||
                previous.Date != row.Date ||
                previous.PostingDate != row.PostingDate ||
                previous.Time != row.Time ||
                previous.Amount != row.Amount ||
                previous.Type != row.Type ||
                previous.Description != row.Description ||
                previous.Memo != row.Memo))
            {
                throw Error(transaction, "The same FITID identifies conflicting transactions, or IDs differ only in letter case. Obtain a corrected export before importing.");
            }

            ids[id] = row;
            result.Rows.Add(row);
        }
    }

    private static ParsedTransaction ReadTransaction(XElement transaction, bool creditCard, string accountId, ParseResult result)
    {
        string nativeType = Required(transaction, "TRNTYPE");

        if (!_transactionTypes.Contains(nativeType))
        {
            throw Error(transaction, "The transaction has an unsupported or pending OFX TRNTYPE.");
        }

        string postedText = Required(transaction, "DTPOSTED");
        OfxDate posted = ReadDate(postedText, transaction, "DTPOSTED");
        string? userText = Optional(transaction, "DTUSER");
        OfxDate date = userText is null ? posted : ReadDate(userText, transaction, "DTUSER");
        string? availableText = Optional(transaction, "DTAVAIL");

        if (availableText is not null)
        {
            _ = ReadDate(availableText, transaction, "DTAVAIL");
        }

        string amountText = Required(transaction, "TRNAMT");
        int decimalPoint = amountText.IndexOfAny(['.', ',']);

        if ((decimalPoint >= 0 && amountText.AsSpan(decimalPoint + 1).Length > 2 && amountText[(decimalPoint + 3)..].Any(digit => digit != '0')) ||
            !TryNumber(amountText, out decimal amount) ||
            amount is < -9999999999999999.99M or > 9999999999999999.99M ||
            decimal.Round(amount, 2) != amount)
        {
            throw Error(transaction, "TRNAMT must be a signed amount with no thousands separators and no fraction smaller than one cent.");
        }

        string fitId = Required(transaction, "FITID", 100);
        string? name = Optional(transaction, "EXTDNAME", 1000) ?? Optional(transaction, "NAME", 1000);
        XElement? payee = One(transaction, "PAYEE", false);

        if (payee is not null)
        {
            name ??= Optional(payee, "NAME", 1000);
        }

        string? memo = Optional(transaction, "MEMO", 1000);
        string description = name ?? memo ?? "OFX " + nativeType;
        string type = MapType(nativeType, amount, creditCard);

        if (ImportClassification.RequiresReview(type))
        {
            result.Warnings.Add(new ParseError(Line(transaction), "This posted transaction does not identify its financial purpose. Classify it as income, refund, repayment, transfer, or expense before relying on income, expense, or salary totals."));
        }

        List<string> details = new();

        if (memo is not null)
        {
            details.Add(memo);
        }

        details.Add("OFX type: " + nativeType);
        details.Add("DTPOSTED: " + postedText);

        if (userText is not null)
        {
            details.Add("DTUSER: " + userText);
        }

        if (availableText is not null)
        {
            details.Add("DTAVAIL: " + availableText);
        }

        foreach (string tag in new[] { "CHECKNUM", "REFNUM", "SRVRTID", "SIC" })
        {
            string? value = Optional(transaction, tag, 255);

            if (value is not null)
            {
                details.Add(tag + ": " + value);
            }
        }

        XElement? bankTo = One(transaction, "BANKACCTTO", false);
        XElement? cardTo = One(transaction, "CCACCTTO", false);

        if (bankTo is not null && cardTo is not null)
        {
            throw Error(transaction, "A transaction cannot identify two transfer destination accounts.");
        }

        XElement? destination = bankTo ?? cardTo;

        if (destination is not null)
        {
            details.Add(destination.Name.LocalName + " ACCTID: " + Required(destination, "ACCTID", 100));

            if (bankTo is not null)
            {
                details.Add("Destination BANKID: " + Required(destination, "BANKID", 100));
                details.Add("Destination ACCTTYPE: " + Required(destination, "ACCTTYPE", 100));
            }
        }

        XElement? originalCurrency = One(transaction, "ORIGCURRENCY", false);

        if (originalCurrency is not null)
        {
            if (One(transaction, "CURRENCY", false) is not null)
            {
                throw Error(transaction, "A transaction cannot supply both CURRENCY and ORIGCURRENCY.");
            }

            string symbol = Required(originalCurrency, "CURSYM");
            string rateText = Required(originalCurrency, "CURRATE");

            if (symbol.Length != 3 || symbol.Any(letter => letter is < 'A' or > 'Z') || !TryNumber(rateText, out decimal rate) || rate <= 0M)
            {
                throw Error(originalCurrency, "The original-currency metadata is invalid.");
            }

            details.Add("Original currency: " + symbol + "; rate: " + rateText + "; statement amount is USD.");
        }

        string fullMemo = string.Join(Environment.NewLine, details);

        if (fullMemo.Length > 1000)
        {
            throw Error(transaction, "Transaction metadata exceeds the 1,000-character memo limit and cannot be preserved without truncation.");
        }

        return new ParsedTransaction
        {
            RowNumber = Line(transaction),
            Date = date.Date,
            PostingDate = posted.Date,
            Time = date.Time,
            Amount = amount,
            Type = type,
            Description = description,
            Merchant = name is { Length: <= 200 } ? name : null,
            ExternalTransactionId = fitId,
            SourceAccountIdentity = accountId,
            Currency = "USD",
            Memo = fullMemo
        };
    }

    private static string MapType(string type, decimal amount, bool creditCard)
    {
        if (type is "CREDIT" or "OTHER" ||
            (amount > 0M && type is "ATM" or "CASH" or "CHECK" or "REPEATPMT" or "DIRECTDEBIT") ||
            (amount > 0M && type == "PAYMENT" && !creditCard) ||
            (creditCard && type is "DEP" or "DIRECTDEP" or "DIV"))
        {
            return amount > 0M ? ImportClassification.UnclassifiedCredit : ImportClassification.UnclassifiedDebit;
        }

        return type switch
        {
            "DEBIT" => amount > 0M ? "Refund" : "Debit",
            "POS" => amount > 0M ? "Refund" : "Purchase",
            "INT" => amount < 0M ? "Interest Charge" : creditCard ? "Refund" : "Interest",
            "FEE" or "SRVCHG" => amount > 0M ? "Refund" : "Fee",
            "DIV" => "Dividend",
            "DEP" => "Deposit",
            "DIRECTDEP" => "Direct Deposit",
            "XFER" => "Transfer",
            "PAYMENT" => "Payment",
            "ATM" => "ATM",
            "CASH" => "Cash Withdrawal",
            "CHECK" => "Check",
            "DIRECTDEBIT" => "Direct Debit",
            "REPEATPMT" => "Recurring Payment",
            _ => throw new InvalidOperationException("Unsupported OFX transaction type.")
        };
    }

    private static OfxDate ReadDate(string value, XElement element, string field)
    {
        Match match = _datePattern.Match(value);

        if (!match.Success || !DateOnly.TryParseExact(match.Groups["date"].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            throw Error(element, field + " is not a valid OFX date or timestamp.");
        }

        TimeOnly? time = null;

        if (match.Groups["time"].Success)
        {
            if (!TimeOnly.TryParseExact(match.Groups["time"].Value, "HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly parsed))
            {
                throw Error(element, field + " contains an unsupported time of day.");
            }

            time = parsed.Add(TimeSpan.FromMilliseconds(match.Groups["fraction"].Success ? int.Parse(match.Groups["fraction"].Value.PadRight(3, '0'), CultureInfo.InvariantCulture) : 0));
        }

        if (match.Groups["offset"].Success &&
            (!decimal.TryParse(match.Groups["offset"].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal offset) ||
            offset is < -14M or > 14M))
        {
            throw Error(element, field + " contains an invalid time-zone offset.");
        }

        return new OfxDate(date, time);
    }

    private static bool TryNumber(string value, out decimal number)
    {
        number = 0M;

        return value.Length <= 32 && _amountPattern.IsMatch(value) && decimal.TryParse(value.Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
    }

    private static XElement? One(XElement parent, string name, bool required)
    {
        XElement[] matches = parent.Elements().Where(element => element.Name.LocalName == name).Take(2).ToArray();

        if (matches.Length > 1 || (required && matches.Length == 0))
        {
            throw Error(parent, "Expected " + (required ? "exactly one " : "at most one ") + name + " field.");
        }

        return matches.FirstOrDefault();
    }

    private static string Required(XElement parent, string name, int maximumLength = 1000)
    {
        return Optional(parent, name, maximumLength) ?? throw Error(parent, name + " is required.");
    }

    private static string? Optional(XElement parent, string name, int maximumLength = 1000)
    {
        XElement? element = One(parent, name, false);

        if (element is null)
        {
            return null;
        }

        if (element.HasElements)
        {
            throw Error(element, name + " must contain a scalar value.");
        }

        string value = element.Value.Trim();

        if (value.Length > maximumLength)
        {
            throw Error(element, name + " exceeds the supported " + maximumLength.ToString(CultureInfo.InvariantCulture) + "-character limit.");
        }

        return value.Length == 0 ? null : value;
    }

    private static ParseResult Failure(int line, string message)
    {
        ParseResult result = new();
        result.Errors.Add(new ParseError(line, message));
        return result;
    }

    private static int Line(XElement element) => element.Annotation<OfxSourceLine>()?.Line ?? ((IXmlLineInfo)element).LineNumber;
    private static OfxFormatException Error(XElement element, string message) => new(Line(element), message);
    private sealed record OfxDate(DateOnly Date, TimeOnly? Time);
}
