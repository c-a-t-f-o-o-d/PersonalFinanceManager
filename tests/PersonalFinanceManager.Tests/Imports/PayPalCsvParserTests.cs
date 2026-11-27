using PersonalFinanceManager.Core.Imports;

namespace PersonalFinanceManager.Tests.Imports;

[TestClass]
public sealed class PayPalCsvParserTests
{
    [TestMethod]
    public void Activity_UsesCompletionDateAndPreservesPostedPurchaseAndRefund()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(
            Row(("Transaction ID", "PURCHASE1"), ("Gross", "-40.00"), ("Net", "-40.00")),
            Row(("Transaction ID", "REFUND1"), ("Type", "Payment Refund"), ("Gross", "15.00"), ("Net", "15.00"), ("Balance Impact", "Credit"), ("Reference Txn ID", "PURCHASE1"))));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(2, result.Rows);
        Assert.AreEqual(-25m, result.Rows.Sum(row => row.Amount));
        Assert.AreEqual(new DateOnly(2026, 4, 5), result.Rows[0].Date);
        Assert.AreEqual(result.Rows[0].Date, result.Rows[0].PostingDate);
        Assert.AreEqual(new TimeOnly(13, 14, 15), result.Rows[0].Time);
        Assert.AreEqual("Purchase", result.Rows[0].Type);
        Assert.AreEqual("Refund", result.Rows[1].Type);
        StringAssert.Contains(result.Rows[1].Memo!, "Reference=PURCHASE1");
    }

    [TestMethod]
    [DataRow("-3.00")]
    [DataRow("3.00")]
    public void Activity_SeparatesFeesWithoutReducingGrossIncomeOrChangingNet(string fee)
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(
            ("Transaction ID", "RECEIPT1"), ("Type", "Payment Received"), ("Gross", "100.00"), ("Fee", fee), ("Net", "97.00"), ("Balance Impact", "Credit"))));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(2, result.Rows);
        Assert.AreEqual(100m, result.Rows[0].Amount);
        Assert.AreEqual("Deposit", result.Rows[0].Type);
        Assert.AreEqual("RECEIPT1", result.Rows[0].ExternalTransactionId);
        Assert.AreEqual(-3m, result.Rows[1].Amount);
        Assert.AreEqual("Fee", result.Rows[1].Type);
        Assert.AreEqual("Fees", result.Rows[1].SourceCategory);
        Assert.AreEqual("RECEIPT1:fee", result.Rows[1].ExternalTransactionId);
        Assert.AreEqual(97m, result.Rows.Sum(row => row.Amount));
    }

    [TestMethod]
    public void Activity_PurchaseFeeAndFeeRefundKeepTheirSignedBalanceMovements()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(
            Row(("Transaction ID", "BUY1"), ("Gross", "-20.00"), ("Fee", "-1.00"), ("Net", "-21.00")),
            Row(("Transaction ID", "REFUND1"), ("Type", "Payment Refund"), ("Gross", "20.00"), ("Fee", "1.00"), ("Net", "21.00"), ("Balance Impact", "Credit"))));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(4, result.Rows);
        Assert.AreEqual(0m, result.Rows.Sum(row => row.Amount));
        Assert.AreEqual(-1m, result.Rows.Single(row => row.ExternalTransactionId == "BUY1:fee").Amount);
        ParsedTransaction feeRefund = result.Rows.Single(row => row.ExternalTransactionId == "REFUND1:fee");
        Assert.AreEqual(1m, feeRefund.Amount);
        Assert.AreEqual("Refund", feeRefund.Type);
        Assert.AreEqual("Fees", feeRefund.SourceCategory);
    }

    [TestMethod]
    public void Activity_SkipsMemoAndUnpostedRowsWithWarnings()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(
            Row(("Transaction ID", "MEMO1"), ("Balance Impact", "Memo")),
            Row(("Transaction ID", "PENDING1"), ("Status", "Pending")),
            Row(("Transaction ID", "DENIED1"), ("Status", "Denied")),
            Row(("Transaction ID", "POSTED1"))));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(1, result.Rows);
        Assert.AreEqual("POSTED1", result.Rows[0].ExternalTransactionId);
        Assert.HasCount(3, result.Warnings.Where(warning => warning.RowNumber > 0));
    }

    [TestMethod]
    public void Activity_ReversedOriginalIsRetainedAlongsideItsRefund()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(
            Row(("Transaction ID", "ORIGINAL1"), ("Status", "Reversed")),
            Row(("Transaction ID", "RETURN1"), ("Type", "Payment Refund"), ("Gross", "10.00"), ("Net", "10.00"), ("Balance Impact", "Credit"))));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(2, result.Rows);
        Assert.AreEqual(0m, result.Rows.Sum(row => row.Amount));
    }

    [TestMethod]
    [DataRow("Credit", "-10.00")]
    [DataRow("Debit", "10.00")]
    public void Activity_RejectsBalanceDirectionConflicts(string impact, string amount)
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(("Balance Impact", impact), ("Gross", amount), ("Net", amount))));
        Assert.HasCount(1, result.Errors);
        Assert.IsEmpty(result.Rows);
    }

    [TestMethod]
    public void Activity_RejectsUnknownStatusAndMissingBalanceImpact()
    {
        ParseResult unknown = new PayPalActivityCsvParser().ParseText(Activity(Row(("Status", "Unrecognized status"))));
        Assert.HasCount(1, unknown.Errors);
        Assert.IsEmpty(unknown.Rows);
        ParseResult missing = new PayPalActivityCsvParser().ParseText(Activity(Row()).Replace("Balance Impact", "Other field", StringComparison.Ordinal));
        Assert.IsTrue(missing.Errors.Any(error => error.Message.Contains("Balance Impact", StringComparison.Ordinal)));
        Assert.IsEmpty(missing.Rows);
    }

    [TestMethod]
    public void Activity_UnsupportedPostedEventsRemainVisibleAndRequireClassification()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(
            Row(("Transaction ID", "UNKNOWN1"), ("Type", "Unrecognized credit"), ("Gross", "25.00"), ("Net", "25.00"), ("Balance Impact", "Credit")),
            Row(("Transaction ID", "UNKNOWN2"), ("Type", "Unrecognized debit"))));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(2, result.Rows);
        Assert.AreEqual("Unclassified Credit", result.Rows[0].Type);
        Assert.AreEqual("Unclassified Debit", result.Rows[1].Type);
        Assert.AreEqual(15m, result.Rows.Sum(row => row.Amount));
        Assert.HasCount(2, result.Warnings.Where(warning => warning.Message.Contains("requires classification", StringComparison.Ordinal)));
        StringAssert.Contains(result.Rows[0].Memo!, "Unrecognized credit");
    }

    [TestMethod]
    public void Activity_MerchantRefundIsNotAssumedToBeAConsumerExpense()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(("Type", "Payment Refund"))));
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual(-10m, result.Rows.Single().Amount);
        Assert.AreEqual("Unclassified Debit", result.Rows.Single().Type);
    }

    [TestMethod]
    [DataRow("T0300")]
    [DataRow("T0400")]
    [DataRow("T0700")]
    [DataRow("T1600")]
    [DataRow("T0200")]
    public void Activity_DocumentedFundingAndConversionCodesAreTransfers(string code)
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(("Type", "Provider label"), ("Transaction Event Code", code))));
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual("Transfer", result.Rows.Single().Type);
        StringAssert.Contains(result.Rows.Single().Memo!, "T-code=" + code);
    }

    [TestMethod]
    public void Activity_DocumentedBuyerCreditAndFeeCodesKeepTheirMeaning()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(
            Row(("Transaction ID", "REPAY1"), ("Type", "Provider label"), ("Transaction Event Code", "T1602")),
            Row(("Transaction ID", "FEE1"), ("Type", "Provider label"), ("Transaction Event Code", "T0107")),
            Row(("Transaction ID", "FEERETURN1"), ("Type", "Provider label"), ("Transaction Event Code", "T1109"), ("Gross", "10.00"), ("Net", "10.00"), ("Balance Impact", "Credit"))));

        Assert.IsEmpty(result.Errors);
        CollectionAssert.AreEqual(new[] { "Buyer Credit Payment", "Fee", "Refund" }, result.Rows.Select(row => row.Type).ToArray());
        Assert.AreEqual("Fees", result.Rows[2].SourceCategory);
    }

    [TestMethod]
    public void Activity_DoesNotMisclassifyCryptoOrUnknownCodesAsIncome()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(
            ("Type", "Payment Received"), ("Transaction Event Code", "T0021"), ("Gross", "10.00"), ("Net", "10.00"), ("Balance Impact", "Credit"))));

        Assert.IsEmpty(result.Errors);
        Assert.AreEqual("Unclassified Credit", result.Rows.Single().Type);
    }

    [TestMethod]
    public void Activity_PaymentSourceDoesNotInventASecondFundingRepresentation()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(("Payment Source", "Credit Card 4444"))));
        Assert.IsEmpty(result.Errors);
        Assert.IsNull(result.Rows.Single().FundingSource);
        StringAssert.Contains(result.Rows.Single().Memo!, "Payment source=Credit Card 4444");
        Assert.IsNull(result.SourceAccountIdentity);
        Assert.IsNull(result.Rows.Single().SourceAccountIdentity);
        Assert.AreEqual("PayPal Wallet", result.SourceAccountType);
        Assert.AreEqual("USD", result.Currency);
    }

    [TestMethod]
    public void Activity_IdenticalSourceIdsDeduplicateButDifferentIdsPreserveMultiplicity()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(("Transaction ID", "A1")), Row(("Transaction ID", "A1")), Row(("Transaction ID", "A2"))));
        Assert.IsEmpty(result.Errors);
        Assert.HasCount(2, result.Rows);
        Assert.AreEqual(-20m, result.Rows.Sum(row => row.Amount));
        Assert.IsTrue(result.Warnings.Any(warning => warning.Message.Contains("repeated row", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Activity_ConflictingSourceIdsAreReportedWithoutInsertingTheConflictingRow()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(("Transaction ID", "A1")), Row(("Transaction ID", "A1"), ("Gross", "-11.00"), ("Net", "-11.00"))));
        Assert.HasCount(1, result.Errors);
        Assert.HasCount(1, result.Rows);
        Assert.AreEqual(-10m, result.Rows[0].Amount);
    }

    [TestMethod]
    public void Activity_FeeIdentityCollisionRejectsTheEntireFile()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(("Transaction ID", "A1"), ("Fee", "-1.00"), ("Net", "-11.00")), Row(("Transaction ID", "A1:fee"))));
        Assert.HasCount(1, result.Errors);
        Assert.IsEmpty(result.Rows);
    }

    [TestMethod]
    public void Activity_FeeSuffixAndLongIdentifiersAreCheckedWithoutSilentTruncation()
    {
        ParseResult valid = new PayPalActivityCsvParser().ParseText(Activity(Row(("Transaction ID", "A1:fee"), ("Fee", "-1.00"), ("Net", "-11.00"))));
        Assert.IsEmpty(valid.Errors);
        CollectionAssert.AreEqual(new[] { "A1:fee", "A1:fee:fee" }, valid.Rows.Select(row => row.ExternalTransactionId).ToArray());
        ParseResult invalid = new PayPalActivityCsvParser().ParseText(Activity(Row(("Transaction ID", new string('A', 97)), ("Fee", "-1.00"), ("Net", "-11.00"))));
        Assert.HasCount(1, invalid.Errors);
        Assert.IsEmpty(invalid.Rows);
    }

    [TestMethod]
    [DataRow("EUR", "-10.00", "0.00", "-10.00")]
    [DataRow("USD", "-1.001", "0.00", "-1.001")]
    [DataRow("USD", "-10.00", "-1.00", "-12.00")]
    [DataRow("USD", "-12,34.56", "0.00", "-12,34.56")]
    public void Activity_RejectsUnsupportedCurrencyAndInconsistentAmounts(string currency, string gross, string fee, string net)
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(("Currency", currency), ("Gross", gross), ("Fee", fee), ("Net", net))));
        Assert.HasCount(1, result.Errors);
        Assert.IsEmpty(result.Rows);
    }

    [TestMethod]
    public void Activity_ThousandsQuotedNamesAndMultilineNotesRemainOneFinancialRecord()
    {
        ParseResult result = new PayPalActivityCsvParser().ParseText(Activity(Row(("Name", "Example, Inc."), ("Note", "First line\nSecond line"), ("Gross", "-1,234.56"), ("Net", "-1,234.56"))));
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual(-1234.56m, result.Rows.Single().Amount);
        StringAssert.Contains(result.Rows.Single().Description, "Example, Inc.");
        StringAssert.Contains(result.Rows.Single().Memo!, "First line\nSecond line");
    }

    [TestMethod]
    public void Activity_RejectsMalformedDatesAndOversizedSourceMetadata()
    {
        ParseResult date = new PayPalActivityCsvParser().ParseText(Activity(Row(("Date", "31/05/2026"))));
        Assert.HasCount(1, date.Errors);
        Assert.IsEmpty(date.Rows);
        ParseResult note = new PayPalActivityCsvParser().ParseText(Activity(Row(("Note", new string('n', 1001)))));
        Assert.HasCount(1, note.Errors);
        Assert.IsEmpty(note.Rows);
    }

    [TestMethod]
    public void Activity_SupportsReorderedColumnsAndRejectsDuplicateHeaders()
    {
        string text = Activity(Row());
        IReadOnlyList<CsvRecord> records = new CsvRecordReader().Read(text);
        string reordered = string.Join("\n", records.Select(record => string.Join(",", record.Fields.Reverse().Select(Cell))));
        ParseResult valid = new PayPalActivityCsvParser().ParseText(reordered);
        Assert.IsEmpty(valid.Errors);
        Assert.AreEqual(-10m, valid.Rows.Single().Amount);
        ParseResult invalid = new PayPalActivityCsvParser().ParseText(text.Replace("Payment Source", "Transaction ID", StringComparison.Ordinal));
        Assert.HasCount(1, invalid.Errors);
        Assert.IsEmpty(invalid.Rows);
    }

    [TestMethod]
    public void Statement_PurchaseFundingAndBankWithdrawalPreserveTheBalanceLedger()
    {
        ParseResult result = new PayPalBalanceStatementCsvParser().ParseText(Statement(
            Row(("Transaction ID", "BANK1"), ("Description", "Bank Deposit to PayPal Account"), ("Gross", "40.00"), ("Net", "40.00"), ("Balance", "40.00"), ("Bank Name", "Example Bank"), ("Bank Account", "1111")),
            Row(("Transaction ID", "BUY1"), ("Gross", "-30.00"), ("Net", "-30.00"), ("Balance", "10.00")),
            Row(("Transaction ID", "WITHDRAW1"), ("Description", "General Withdrawal - Bank Account"), ("Balance", "0.00"))));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(3, result.Rows);
        CollectionAssert.AreEqual(new[] { "Transfer", "Purchase", "Transfer" }, result.Rows.Select(row => row.Type).ToArray());
        Assert.AreEqual(0m, result.Rows.Sum(row => row.Amount));
        Assert.IsNull(result.SourceAccountIdentity);
        Assert.IsTrue(result.Rows.All(row => row.SourceAccountIdentity is null && row.FundingSource is null));
        StringAssert.Contains(result.Rows[0].Memo!, "Bank account=1111");
    }

    [TestMethod]
    public void Statement_ExplicitRefundAndFeesUseTheSameIdentifiersAcrossRepeatedParsing()
    {
        string text = Statement(Row(("Description", "Payment Refund"), ("Gross", "20.00"), ("Fee", "1.00"), ("Net", "21.00"), ("Balance", "21.00")));
        ParseResult first = new PayPalBalanceStatementCsvParser().ParseText(text);
        ParseResult again = new PayPalBalanceStatementCsvParser().ParseText(text);
        Assert.IsEmpty(first.Errors);
        Assert.HasCount(2, first.Rows);
        Assert.AreEqual(21m, first.Rows.Sum(row => row.Amount));
        Assert.IsTrue(first.Rows.All(row => row.Type == "Refund"));
        CollectionAssert.AreEqual(first.Rows.Select(row => row.ExternalTransactionId).ToArray(), again.Rows.Select(row => row.ExternalTransactionId).ToArray());
    }

    [TestMethod]
    public void Statement_RequiresItsOwnHeadersAndValidBalance()
    {
        ParseResult wrongLayout = new PayPalBalanceStatementCsvParser().ParseText(Activity(Row()));
        Assert.IsTrue(wrongLayout.Errors.Count > 0);
        Assert.IsEmpty(wrongLayout.Rows);
        ParseResult balance = new PayPalBalanceStatementCsvParser().ParseText(Statement(Row(("Balance", "not a balance"))));
        Assert.HasCount(1, balance.Errors);
        Assert.IsEmpty(balance.Rows);
    }

    [TestMethod]
    public void Parsers_ReturnActionableErrorsForEmptyMalformedAndHeaderlessFiles()
    {
        Assert.HasCount(1, new PayPalActivityCsvParser().ParseText(string.Empty).Errors);
        Assert.HasCount(1, new PayPalActivityCsvParser().ParseText("Date,\"Unterminated").Errors);
        ParseResult continuation = new PayPalBalanceStatementCsvParser().ParseText("04/05/2026,13:14:15,PDT,Payment,USD,-10,0,-10,90,A1");
        Assert.IsTrue(continuation.Errors.Count > 0);
        Assert.IsEmpty(continuation.Rows);
    }

    private static Dictionary<string, string> Row(params (string Key, string Value)[] fields)
    {
        return fields.ToDictionary(field => field.Key, field => field.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string Activity(params Dictionary<string, string>[] rows)
    {
        return Build(true, rows);
    }

    private static string Statement(params Dictionary<string, string>[] rows)
    {
        return Build(false, rows);
    }

    private static string Build(bool activity, params Dictionary<string, string>[] rows)
    {
        string[] headers = activity
            ? ["Date", "Time", "TimeZone", "Name", "Type", "Status", "Currency", "Gross", "Fee", "Net", "Transaction ID", "Balance Impact", "Transaction Event Code", "Payment Source", "Reference Txn ID", "Note", "From Email Address", "To Email Address"]
            : ["Date", "Time", "Time Zone", "Description", "Currency", "Gross", "Fee", "Net", "Balance", "Transaction ID", "Name", "Bank Name", "Bank Account", "Reference Txn ID"];

        Dictionary<string, string> defaults = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Date"] = "04/05/2026",
            ["Time"] = "13:14:15",
            ["TimeZone"] = "PDT",
            ["Time Zone"] = "PDT",
            ["Name"] = "Example Merchant",
            ["Type"] = "Express Checkout Payment",
            ["Description"] = "Express Checkout Payment",
            ["Status"] = "Completed",
            ["Currency"] = "USD",
            ["Gross"] = "-10.00",
            ["Fee"] = "0.00",
            ["Net"] = "-10.00",
            ["Balance"] = "90.00",
            ["Transaction ID"] = "EXAMPLE1",
            ["Balance Impact"] = "Debit"
        };

        return string.Join(",", headers.Select(Cell)) + "\n" + string.Join("\n", rows.Select(row => string.Join(",", headers.Select(header =>
            Cell(row.TryGetValue(header, out string? value) ? value : defaults.GetValueOrDefault(header) ?? string.Empty))))) + "\n";
    }

    private static string Cell(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
