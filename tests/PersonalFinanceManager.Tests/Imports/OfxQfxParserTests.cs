using System.Text;
using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Tests.Imports;

[TestClass]
public sealed class OfxQfxParserTests
{
    [TestMethod]
    public void SgmlBankStatementPreservesAmountsAccountFitidAndSourceDates()
    {
        const string file = """
            OFXHEADER:100
            DATA:OFXSGML
            VERSION:102
            SECURITY:NONE
            ENCODING:USASCII
            CHARSET:1252
            COMPRESSION:NONE
            OLDFILEUID:NONE
            NEWFILEUID:NONE

            <OFX>
            <SIGNONMSGSRSV1><SONRS><STATUS><CODE>0<SEVERITY>INFO</STATUS><DTSERVER>20260402120000<LANGUAGE>ENG<FI><ORG>Example Bank<FID>123</FI><INTU.BID>123</SONRS></SIGNONMSGSRSV1>
            <BANKMSGSRSV1><STMTTRNRS><TRNUID>0<STATUS><CODE>0<SEVERITY>INFO</STATUS>
            <STMTRS><CURDEF>USD<BANKACCTFROM><BANKID>123456789<ACCTID>000012345678<ACCTTYPE>CHECKING</BANKACCTFROM>
            <BANKTRANLIST><DTSTART>20260401<DTEND>20260501
            <STMTTRN><TRNTYPE>POS<DTPOSTED>20260402003000.125[-5:EST]<DTUSER>20260401235900[-5:EST]<TRNAMT>-42.76<FITID>AbC-001<NAME>Grocer &amp; Market<MEMO>Weekly shop</STMTTRN>
            <STMTTRN><TRNTYPE>DIRECTDEP<DTPOSTED>20260402<TRNAMT>2500.00<FITID>Payroll-002<NAME>Payroll</STMTTRN>
            </BANKTRANLIST><LEDGERBAL><BALAMT>7457.24<DTASOF>20260501</LEDGERBAL><AVAILBAL><BALAMT>7350.24<DTASOF>20260501</AVAILBAL>
            </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """;

        ParseResult result = new OfxQfxParser().ParseText(file);

        Assert.IsEmpty(result.Errors);
        Assert.IsEmpty(result.Warnings);
        Assert.HasCount(2, result.Rows);
        Assert.AreEqual("000012345678", result.SourceAccountIdentity);
        Assert.AreEqual("CHECKING", result.SourceAccountType);
        Assert.AreEqual("USD", result.Currency);
        ParsedTransaction purchase = result.Rows[0];
        Assert.AreEqual(-42.76m, purchase.Amount);
        Assert.AreEqual("AbC-001", purchase.ExternalTransactionId);
        Assert.AreEqual("Grocer & Market", purchase.Description);
        Assert.AreEqual(new DateOnly(2026, 4, 1), purchase.Date);
        Assert.AreEqual(new DateOnly(2026, 4, 2), purchase.PostingDate);
        Assert.AreEqual(new TimeOnly(23, 59), purchase.Time);
        Assert.IsTrue(purchase.RowNumber > 10);
        Assert.IsTrue(purchase.Memo!.Contains("DTPOSTED: 20260402003000.125[-5:EST]", StringComparison.Ordinal));
        Assert.AreEqual("Direct Deposit", result.Rows[1].Type);
        Assert.AreEqual(2500m, result.Rows[1].Amount);
        Assert.AreEqual(result.Rows[1].Date, result.Rows[1].PostingDate);
        Assert.IsTrue(result.Rows.All(row => row.SourceAccountIdentity == result.SourceAccountIdentity && row.Currency == "USD"));
    }

    [TestMethod]
    public void XmlCreditCardStatementDistinguishesPurchasesPaymentsRefundsAndInterest()
    {
        string file = XmlStatement(
            Transaction("POS", "-42.76", "purchase") + Transaction("PAYMENT", "500.00", "payment")
            + Transaction("POS", "12.50", "refund") + Transaction("INT", "-6.10", "interest")
            + Transaction("PAYMENT", "-500.00", "reversed-payment") + Transaction("CREDIT", "15.00", "generic-credit"), true);

        ParseResult result = new OfxQfxParser().ParseText(file);

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(6, result.Rows);
        CollectionAssert.AreEqual(new[] { "Purchase", "Payment", "Refund", "Interest Charge", "Payment", "Unclassified Credit" }, result.Rows.Select(row => row.Type).ToArray());
        CollectionAssert.AreEqual(new[] { -42.76m, 500m, 12.5m, -6.1m, -500m, 15m }, result.Rows.Select(row => row.Amount).ToArray());
        Assert.AreEqual("CREDITCARD", result.SourceAccountType);
        Assert.HasCount(1, result.Warnings);
        Assert.IsTrue(result.Rows[5].Memo!.Contains("OFX type: CREDIT", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GenericCreditsDoNotGuessIncomeRefundOrRepaymentFromDescription()
    {
        string rows = Transaction("CREDIT", "500", "credit").Replace("Example merchant", "PAYMENT THANK YOU", StringComparison.Ordinal)
            + Transaction("OTHER", "25", "other") + Transaction("OTHER", "-8", "other-debit")
            + Transaction("ATM", "100", "atm") + Transaction("PAYMENT", "250", "incoming-payment");

        ParseResult result = new OfxQfxParser().ParseText(XmlStatement(rows));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(5, result.Rows);
        Assert.HasCount(5, result.Warnings);
        Assert.IsTrue(result.Rows.Where(row => row.Amount > 0).All(row => row.Type == ImportClassification.UnclassifiedCredit));
        Assert.AreEqual(ImportClassification.UnclassifiedDebit, result.Rows.Single(row => row.Amount < 0).Type);
        Assert.AreEqual(867m, result.Rows.Sum(row => row.Amount));
    }

    [TestMethod]
    public void ExplicitIncomeReversalsAndInterestChargesRetainTheirSignedMeaning()
    {
        string rows = Transaction("DEP", "-100", "deposit-reversal") + Transaction("DIRECTDEP", "-500", "payroll-reversal")
            + Transaction("DIV", "-10", "dividend-reversal") + Transaction("INT", "-3", "interest-paid") + Transaction("INT", "2", "interest-earned")
            + Transaction("XFER", "-75", "transfer");

        ParseResult result = new OfxQfxParser().ParseText(XmlStatement(rows));

        Assert.IsEmpty(result.Errors);
        Assert.IsEmpty(result.Warnings);
        CollectionAssert.AreEqual(new[] { "Deposit", "Direct Deposit", "Dividend", "Interest Charge", "Interest", "Transfer" }, result.Rows.Select(row => row.Type).ToArray());
        CollectionAssert.AreEqual(new[] { -100m, -500m, -10m, -3m, 2m, -75m }, result.Rows.Select(row => row.Amount).ToArray());
    }

    [TestMethod]
    public void ExplicitStatementIdentityIsCheckedEvenWhenThereAreNoTransactions()
    {
        ParseResult result = new OfxQfxParser().ParseText(XmlStatement(string.Empty));

        Assert.IsEmpty(result.Errors);
        Assert.IsEmpty(result.Rows);
        Assert.AreEqual("000012345678", result.SourceAccountIdentity);
        Assert.ThrowsExactly<InvalidOperationException>(() => ImportAccountValidation.Validate(
            new Account { AccountType = "Checking", AccountNickname = "Different", AccountNumber = "000099995555", AccountNumberLastFour = "5555" }, ImportProfileCodes.OfxQfx, result));
        ImportAccountValidation.Validate(new Account { AccountType = "Checking", AccountNumber = "000012345678", AccountNumberLastFour = "5678" }, ImportProfileCodes.OfxQfx, result);
    }

    [TestMethod]
    public void CardStatementCannotBeImportedIntoItsPayingCheckingAccount()
    {
        ParseResult result = new OfxQfxParser().ParseText(XmlStatement(Transaction("PAYMENT", "500", "payment"), true));

        Assert.IsEmpty(result.Errors);
        Assert.ThrowsExactly<InvalidOperationException>(() => ImportAccountValidation.Validate(
            new Account { AccountType = "Checking", AccountNumber = "000012345678", AccountNumberLastFour = "5678" }, ImportProfileCodes.OfxQfx, result));
        ImportAccountValidation.Validate(new Account { AccountType = "Credit Card", AccountNumber = "000012345678", AccountNumberLastFour = "5678" }, ImportProfileCodes.OfxQfx, result);
    }

    [TestMethod]
    public void OriginalForeignCurrencyDoesNotConvertAnAlreadyConvertedUsdAmountAgain()
    {
        string row = Transaction("POS", "-110.00", "foreign-purchase", "<ORIGCURRENCY><CURRATE>1.10</CURRATE><CURSYM>EUR</CURSYM></ORIGCURRENCY>");

        ParseResult result = new OfxQfxParser().ParseText(XmlStatement(row));

        Assert.IsEmpty(result.Errors);
        Assert.AreEqual(-110m, result.Rows.Single().Amount);
        Assert.AreEqual("USD", result.Rows.Single().Currency);
        Assert.IsTrue(result.Rows.Single().Memo!.Contains("Original currency: EUR; rate: 1.10; statement amount is USD.", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("<CURRENCY><CURRATE>1.10</CURRATE><CURSYM>EUR</CURSYM></CURRENCY>")]
    [DataRow("<CURRENCY><CURRATE>1.10</CURRATE><CURSYM>USD</CURSYM></CURRENCY>")]
    [DataRow("<ORIGCURRENCY><CURRATE>0</CURRATE><CURSYM>EUR</CURSYM></ORIGCURRENCY>")]
    [DataRow("<CURRENCY><CURRATE>1</CURRATE><CURSYM>USD</CURSYM></CURRENCY><ORIGCURRENCY><CURRATE>1</CURRATE><CURSYM>USD</CURSYM></ORIGCURRENCY>")]
    public void UnsupportedCurrencySemanticsRejectTheWholeStatement(string currency)
    {
        AssertRejected(XmlStatement(Transaction("DEBIT", "-10", "valid") + Transaction("POS", "-20", "foreign", currency)));
    }

    [TestMethod]
    public void MissingOrForeignDefaultCurrencyIsRejected()
    {
        AssertRejected(XmlStatement(Transaction("DEBIT", "-10", "valid")).Replace("<CURDEF>USD</CURDEF>", "", StringComparison.Ordinal));
        AssertRejected(XmlStatement(Transaction("DEBIT", "-10", "valid")).Replace("<CURDEF>USD</CURDEF>", "<CURDEF>CAD</CURDEF>", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("<CORRECTFITID>original</CORRECTFITID><CORRECTACTION>REPLACE</CORRECTACTION>")]
    [DataRow("<CORRECTFITID>original</CORRECTFITID><CORRECTACTION>DELETE</CORRECTACTION>")]
    [DataRow("<REVERSALFITID>original</REVERSALFITID>")]
    [DataRow("<LOANPMTINFO><PRINAMT>8</PRINAMT><INTAMT>2</INTAMT></LOANPMTINFO>")]
    public void CorrectionOrUnsupportedBreakdownNeverPartiallyImportsOriginalRows(string instruction)
    {
        AssertRejected(XmlStatement(Transaction("DEBIT", "-10", "original") + Transaction("DEBIT", "-20", "changed", instruction)));
    }

    [TestMethod]
    public void PendingTransactionsAreNotInstalledAsPostedLedgerRows()
    {
        string file = XmlStatement(Transaction("DEBIT", "-10", "posted"));
        AssertRejected(file.Replace("</BANKTRANLIST>", "</BANKTRANLIST><BANKTRANLISTP><STMTTRNP><TRNTYPE>HOLD</TRNTYPE><DTTRAN>20260403</DTTRAN><TRNAMT>-50</TRNAMT></STMTTRNP></BANKTRANLISTP>", StringComparison.Ordinal));
        AssertRejected(XmlStatement(Transaction("HOLD", "-10", "pending")));
    }

    [TestMethod]
    public void InvestmentAndMultipleAccountDocumentsCannotBePartiallyImported()
    {
        string valid = XmlStatement(Transaction("DEBIT", "-10", "valid"));
        AssertRejected(valid.Replace("</OFX>", "<INVSTMTMSGSRSV1><INVSTMTTRNRS><INVSTMTRS/></INVSTMTTRNRS></INVSTMTMSGSRSV1></OFX>", StringComparison.Ordinal));
        string bankSection = valid[valid.IndexOf("<BANKMSGSRSV1>", StringComparison.Ordinal)..valid.IndexOf("</OFX>", StringComparison.Ordinal)];
        AssertRejected(valid.Replace("</OFX>", bankSection.Replace("000012345678", "000087654321", StringComparison.Ordinal) + "</OFX>", StringComparison.Ordinal));
        AssertRejected(valid.Replace("<ACCTTYPE>CHECKING</ACCTTYPE>", "<ACCTTYPE>CREDITLINE</ACCTTYPE>", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("-1.001")]
    [DataRow("1,000.00")]
    [DataRow("1.000,00")]
    [DataRow("1e2")]
    [DataRow("(12.50)")]
    [DataRow("10000000000000000")]
    [DataRow("0.0100000000000000000000000000001")]
    public void InexactOrOutOfRangeAmountsRejectTheWholeFile(string amount)
    {
        AssertRejected(XmlStatement(Transaction("DEBIT", "-5", "valid") + Transaction("DEBIT", amount, "invalid")));
    }

    [TestMethod]
    public void OfxDecimalCommaIsAFractionNotAThousandsSeparator()
    {
        ParseResult result = new OfxQfxParser().ParseText(XmlStatement(Transaction("DEBIT", "-19,95", "comma")
            + Transaction("DEBIT", "-.50", "fraction") + Transaction("DEP", "+1200.000", "trailing-zeros")));

        Assert.IsEmpty(result.Errors);
        CollectionAssert.AreEqual(new[] { -19.95m, -.5m, 1200m }, result.Rows.Select(row => row.Amount).ToArray());
    }

    [TestMethod]
    public void RequiredFitidIsNotInventedTruncatedOrReplacedByAnOccurrenceNumber()
    {
        string row = Transaction("DEBIT", "-10", "native-Case-ID");
        ParseResult result = new OfxQfxParser().ParseText(XmlStatement(row + row));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(2, result.Rows);
        Assert.IsTrue(result.Rows.All(transaction => transaction.ExternalTransactionId == "native-Case-ID"));
        AssertRejected(XmlStatement(row.Replace("<FITID>native-Case-ID</FITID>", "", StringComparison.Ordinal)));
        AssertRejected(XmlStatement(Transaction("DEBIT", "-10", new string('A', 101))));
        AssertRejected(XmlStatement(row + Transaction("DEBIT", "-11", "native-Case-ID")));
        AssertRejected(XmlStatement(row + Transaction("DEBIT", "-10", "NATIVE-CASE-ID")));
    }

    [TestMethod]
    public void BackdatedPostedItemsAreNotLostOutsideTheServerSearchWindow()
    {
        string row = Transaction("DEBIT", "-10", "late-posted").Replace("20260402", "20260331235959.999[14:LINT]", StringComparison.Ordinal);

        ParseResult result = new OfxQfxParser().ParseText(XmlStatement(row));

        Assert.IsEmpty(result.Errors);
        Assert.AreEqual(new DateOnly(2026, 3, 31), result.Rows.Single().Date);
        Assert.AreEqual(new DateOnly(2026, 3, 31), result.Rows.Single().PostingDate);
        Assert.AreEqual(new TimeOnly(23, 59, 59, 999), result.Rows.Single().Time);
    }

    [TestMethod]
    [DataRow("20260230")]
    [DataRow("20260402246000")]
    [DataRow("20260402000000[99:BAD]")]
    [DataRow("20260402garbage")]
    public void InvalidDatesDoNotFallBackToTodayOrDiscardTheirInvalidSuffix(string date)
    {
        AssertRejected(XmlStatement(Transaction("DEBIT", "-10", "valid") + Transaction("DEBIT", "-20", "bad-date").Replace("20260402", date, StringComparison.Ordinal)));
    }

    [TestMethod]
    public void DuplicateRequiredFieldsAndFailureStatusAreRejected()
    {
        string file = XmlStatement(Transaction("DEBIT", "-10", "valid"));
        AssertRejected(file.Replace("<TRNAMT>-10</TRNAMT>", "<TRNAMT>-10</TRNAMT><TRNAMT>-20</TRNAMT>", StringComparison.Ordinal));
        AssertRejected(file.Replace("<CODE>0</CODE>", "<CODE>2000</CODE>", StringComparison.Ordinal));
        AssertRejected(file.Replace("<DTEND>20260501</DTEND>", "<DTEND>20260301</DTEND>", StringComparison.Ordinal));
        AssertRejected(file.Replace("<ACCTID>000012345678</ACCTID>", "", StringComparison.Ordinal));
        AssertRejected(file.Replace("</BANKMSGSRSV1>", "<INTRATRNRS><STATUS><CODE>0</CODE><SEVERITY>INFO</SEVERITY></STATUS></INTRATRNRS></BANKMSGSRSV1>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TransferDestinationAndCheckReferencesRemainAvailableForReconciliation()
    {
        string metadata = "<CHECKNUM>1234</CHECKNUM><REFNUM>REF-12</REFNUM><BANKACCTTO><BANKID>111222333</BANKID><ACCTID>000099991234</ACCTID><ACCTTYPE>SAVINGS</ACCTTYPE></BANKACCTTO>";

        ParseResult result = new OfxQfxParser().ParseText(XmlStatement(Transaction("XFER", "-100", "transfer", metadata)));

        Assert.IsEmpty(result.Errors);
        Assert.AreEqual("Transfer", result.Rows.Single().Type);
        Assert.IsTrue(result.Rows.Single().Memo!.Contains("BANKACCTTO ACCTID: 000099991234", StringComparison.Ordinal));
        Assert.IsTrue(result.Rows.Single().Memo!.Contains("CHECKNUM: 1234", StringComparison.Ordinal));
        Assert.IsTrue(result.Rows.Single().Memo!.Contains("REFNUM: REF-12", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TruncatedSgmlAndWrongClosersCannotProduceAPartialStatement()
    {
        string valid = SgmlFromXml(XmlStatement(Transaction("DEBIT", "-10", "valid")));
        ParseResult parsed = new OfxQfxParser().ParseText(valid);
        Assert.IsEmpty(parsed.Errors);
        Assert.HasCount(1, parsed.Rows);
        AssertRejected(valid.Replace("</BANKTRANLIST>", "", StringComparison.Ordinal));
        AssertRejected(valid.Replace("</STMTTRN>", "</WRONG>", StringComparison.Ordinal));
        AssertRejected(valid[..valid.LastIndexOf("</OFX>", StringComparison.Ordinal)]);
    }

    [TestMethod]
    public void XmlDtdsAndExternalEntitiesAreRejectedWithoutResolution()
    {
        string file = XmlStatement(Transaction("DEBIT", "-10", "valid"));
        AssertRejected(file.Replace("<OFX>", "<!DOCTYPE OFX [<!ENTITY external SYSTEM 'file:///nonexistent-financial-data'>]><OFX>", StringComparison.Ordinal)
            .Replace("Example merchant", "&external;", StringComparison.Ordinal));
        AssertRejected(file.Replace("<OFX>", "<!DOCTYPE OFX SYSTEM 'https://example.invalid/remote.dtd'><OFX>", StringComparison.Ordinal));
        AssertRejected(SgmlFromXml(file).Replace("<OFX>", "<!DOCTYPE OFX SYSTEM 'https://example.invalid/remote.dtd'><OFX>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void XmlNamespacesAndCdataAreHandledWithoutFetchingSchemas()
    {
        string file = XmlStatement(Transaction("DEBIT", "-10", "valid")).Replace("<OFX>", "<OFX xmlns='http://ofx.net/types/2003/04' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:schemaLocation='http://ofx.net/types/2003/04 https://example.invalid/schema.xsd'>", StringComparison.Ordinal)
            .Replace("Example merchant", "<![CDATA[A & B <Market>]]>", StringComparison.Ordinal);

        ParseResult result = new OfxQfxParser().ParseText(file);

        Assert.IsEmpty(result.Errors);
        Assert.AreEqual("A & B <Market>", result.Rows.Single().Description);
        AssertRejected(file.Replace("http://ofx.net/types/2003/04", "https://unknown.example/ofx", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Windows1252SgmlAndUtf16XmlHonorDeclaredCharacterEncoding()
    {
        string sgml = SgmlFromXml(XmlStatement(Transaction("DEBIT", "-10", "accent"))).Replace("Example merchant", "Café – shop", StringComparison.Ordinal);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ParseResult western = await ParseBytesAsync(Encoding.GetEncoding(1252).GetBytes(sgml));
        string xml = XmlStatement(Transaction("DEBIT", "-10", "unicode")).Replace("encoding=\"UTF-8\"", "encoding=\"UTF-16\"", StringComparison.Ordinal)
            .Replace("Example merchant", "Café 東京", StringComparison.Ordinal);
        ParseResult unicode = await ParseBytesAsync(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(xml)).ToArray());

        Assert.IsEmpty(western.Errors);
        Assert.IsEmpty(unicode.Errors);
        Assert.AreEqual("Café – shop", western.Rows.Single().Description);
        Assert.AreEqual("Café 東京", unicode.Rows.Single().Description);
    }

    [TestMethod]
    public async Task InvalidUtf8IsRejectedInsteadOfReplacingCharactersInTransactionIdentities()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(XmlStatement(Transaction("DEBIT", "-10", "valid")));
        int position = Array.IndexOf(bytes, (byte)'v', Array.IndexOf(bytes, (byte)'F'));
        bytes[position] = 0xFF;

        ParseResult result = await ParseBytesAsync(bytes);

        Assert.IsEmpty(result.Rows);
        Assert.IsNotEmpty(result.Errors);
    }

    [TestMethod]
    public void OversizedOrDeepDocumentsAreRejected()
    {
        AssertRejected(new string(' ', 16 * 1024 * 1024 + 1) + "<OFX/>");
        string file = XmlStatement(Transaction("DEBIT", "-10", "valid"));
        AssertRejected(file.Replace("Example merchant", string.Concat(Enumerable.Repeat("<NODE>", 60)) + "text" + string.Concat(Enumerable.Repeat("</NODE>", 60)), StringComparison.Ordinal));
    }

    private static void AssertRejected(string file)
    {
        ParseResult result = new OfxQfxParser().ParseText(file);
        Assert.IsEmpty(result.Rows, "An invalid statement must not expose rows that ImportService could install.");
        Assert.IsNotEmpty(result.Errors);
    }

    private static string Transaction(string type, string amount, string id, string extra = "")
        => $"<STMTTRN><TRNTYPE>{type}</TRNTYPE><DTPOSTED>20260402</DTPOSTED><TRNAMT>{amount}</TRNAMT><FITID>{id}</FITID><NAME>Example merchant</NAME>{extra}</STMTTRN>";

    private static string XmlStatement(string transactions, bool card = false)
    {
        string messages = card ? "CREDITCARDMSGSRSV1" : "BANKMSGSRSV1";
        string wrapper = card ? "CCSTMTTRNRS" : "STMTTRNRS";
        string statement = card ? "CCSTMTRS" : "STMTRS";
        string account = card ? "<CCACCTFROM><ACCTID>000012345678</ACCTID></CCACCTFROM>"
            : "<BANKACCTFROM><BANKID>123456789</BANKID><ACCTID>000012345678</ACCTID><ACCTTYPE>CHECKING</ACCTTYPE></BANKACCTFROM>";
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <?OFX OFXHEADER="200" VERSION="230" SECURITY="NONE" OLDFILEUID="NONE" NEWFILEUID="NONE"?>
            <OFX>
            <{messages}><{wrapper}><TRNUID>0</TRNUID><STATUS><CODE>0</CODE><SEVERITY>INFO</SEVERITY></STATUS>
            <{statement}><CURDEF>USD</CURDEF>{account}<BANKTRANLIST><DTSTART>20260401</DTSTART><DTEND>20260501</DTEND>{transactions}</BANKTRANLIST>
            <LEDGERBAL><BALAMT>1000</BALAMT><DTASOF>20260501</DTASOF></LEDGERBAL></{statement}></{wrapper}></{messages}>
            </OFX>
            """;
    }

    private static string SgmlFromXml(string xml)
    {
        string body = xml[xml.IndexOf("<OFX>", StringComparison.Ordinal)..];
        string[] leaves = ["TRNUID", "CODE", "SEVERITY", "CURDEF", "BANKID", "ACCTID", "ACCTTYPE", "DTSTART", "DTEND", "TRNTYPE", "DTPOSTED", "TRNAMT", "FITID", "NAME", "BALAMT", "DTASOF"];
        foreach (string leaf in leaves) body = body.Replace("</" + leaf + ">", "\n", StringComparison.Ordinal);
        return "OFXHEADER:100\nDATA:OFXSGML\nVERSION:102\nSECURITY:NONE\nENCODING:USASCII\nCHARSET:1252\nCOMPRESSION:NONE\nOLDFILEUID:NONE\nNEWFILEUID:NONE\n\n" + body;
    }

    private static async Task<ParseResult> ParseBytesAsync(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".qfx");
        try
        {
            await File.WriteAllBytesAsync(path, bytes);
            return await new OfxQfxParser().ParseAsync(path);
        }
        finally { File.Delete(path); }
    }
}
