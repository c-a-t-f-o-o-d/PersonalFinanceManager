using PersonalFinanceManager.Core.Imports;

namespace PersonalFinanceManager.Tests.Imports;

[TestClass]
public sealed class ImportIntegrityTests
{
    [TestMethod]
    public void PayPal_ThousandsSeparatorPreservesBothTransactions()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement(
            "04/01/2026\nPreApproved Payment Bill User Payment: LARGE PURCHASE\nUSD\n-1,234.56\n0.00\n-1,234.56\n" +
            "04/02/2026\nPreApproved Payment Bill User Payment: SMALL PURCHASE\nUSD\n-10.00\n0.00\n-10.00"));

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(2, result.Rows);
        Assert.AreEqual(-1244.56m, result.Rows.Sum(row => row.Amount));
        Assert.AreEqual(new DateOnly(2026, 4, 2), result.Rows[1].Date);
        Assert.AreEqual("LARGE PURCHASE", result.Rows[0].Description);
    }

    [TestMethod]
    public void PayPal_InvalidAmountCannotConsumeTheFollowingTransaction()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement(
            "04/01/2026\nBAD PURCHASE\nUSD\n-1,23.45\n0.00\n-1,23.45\n" +
            "04/02/2026\nGOOD PURCHASE\nUSD\n-10.00\n0.00\n-10.00"));

        Assert.HasCount(1, result.Errors);
        Assert.HasCount(1, result.Rows);
        Assert.AreEqual(new DateOnly(2026, 4, 2), result.Rows[0].Date);
        Assert.AreEqual(-10m, result.Rows[0].Amount);
        Assert.AreEqual("GOOD PURCHASE", result.Rows[0].Description);
    }

    [TestMethod]
    public void PayPal_InlineFundingSupportsDifferentBanksAndPreservesIds()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement(
            "04/01/2026\nPURCHASE A\nUSD\n-10.00\n0.00\n-10.00\nVISA - Credit Card x-1234\nUSD\nID: A1C2B\n" +
            "04/02/2026\nPURCHASE B\nUSD\n-20.00\n0.00\n-20.00\nOTHER BANK - Checking x-5678\nUSD\nID: B456"));

        Assert.IsEmpty(result.Errors);
        Assert.IsEmpty(FundingWarnings(result));
        Assert.AreEqual("VISA - Credit Card x-1234", result.Rows[0].FundingSource);
        Assert.AreEqual("A1C2B", result.Rows[0].ExternalTransactionId);
        Assert.AreEqual("OTHER BANK - Checking x-5678", result.Rows[1].FundingSource);
        Assert.AreEqual("B456", result.Rows[1].ExternalTransactionId);
    }

    [TestMethod]
    public void PayPal_DetachedFundingIsNotAssignedByPosition()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement(
            "04/01/2026\nPURCHASE A\nUSD\n-10.00\n0.00\n-10.00\n" +
            "04/02/2026\nPURCHASE B\nUSD\n-20.00\n0.00\n-20.00\n" +
            "VISA - Credit Card x-1234\nUSD\nID: A123\nALLY BANK - Checking x-5678\nUSD\nID: B456"));

        Assert.HasCount(2, result.Rows);
        Assert.IsEmpty(result.Errors);
        Assert.HasCount(2, FundingWarnings(result));
        Assert.IsTrue(result.Rows.All(row => row.FundingSource is null));
        Assert.IsTrue(result.Rows.All(row => row.ExternalTransactionId!.StartsWith("PAYPALSTATEMENT:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PayPal_DetachedFundingCanUseUniqueAmountEvidence()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement(
            "04/01/2026\nPURCHASE A\nUSD\n-10.00\n0.00\n-10.00\n" +
            "04/02/2026\nPURCHASE B\nUSD\n-20.00\n0.00\n-20.00\n" +
            "ALLY BANK - Checking x-5678\n20.00\nUSD\nID: B456\nVISA - Credit Card x-1234\n10.00\nUSD\nID: A123"));

        Assert.IsEmpty(result.Errors);
        Assert.IsEmpty(FundingWarnings(result));
        Assert.AreEqual("A123", result.Rows[0].ExternalTransactionId);
        Assert.AreEqual("B456", result.Rows[1].ExternalTransactionId);
    }

    [TestMethod]
    public void PayPal_DuplicateFundingAmountsRemainUnassigned()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement(
            "04/01/2026\nPURCHASE A\nUSD\n-10.00\n0.00\n-10.00\n" +
            "04/02/2026\nPURCHASE B\nUSD\n-10.00\n0.00\n-10.00\n" +
            "ALLY BANK - Checking x-5678\n10.00\nUSD\nID: B456\nVISA - Credit Card x-1234\n10.00\nUSD\nID: A123"));

        Assert.HasCount(2, result.Rows);
        Assert.HasCount(2, FundingWarnings(result));
        Assert.IsTrue(result.Rows.All(row => row.FundingSource is null));
    }

    [TestMethod]
    public void PayPal_MissingFirstFundingDoesNotShiftTheLastFunding()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement(
            "04/01/2026\nPURCHASE A\nUSD\n-10.00\n0.00\n-10.00\n" +
            "04/02/2026\nPURCHASE B\nUSD\n-20.00\n0.00\n-20.00\nALLY BANK - Checking x-5678\nUSD\nID: B456"));

        Assert.HasCount(2, result.Rows);
        Assert.IsTrue(result.Rows.All(row => row.FundingSource is null));
        Assert.HasCount(2, FundingWarnings(result));
    }

    [TestMethod]
    public void PayPal_UnreconciledStatementAmountsAreRejected()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement("04/01/2026\nPURCHASE\nUSD\n-10.00\n0.00\n-20.00"));
        Assert.IsEmpty(result.Rows);
        Assert.HasCount(1, result.Errors);
    }

    [TestMethod]
    public void PayPal_PartialFundingDoesNotTreatTheWholePurchaseAsOneSource()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement("04/01/2026\nPURCHASE\nUSD\n-20.00\n0.00\n-20.00\nBANK - Checking x-1234\n10.00\nUSD\nID: ABC123"));
        Assert.HasCount(1, result.Rows);
        Assert.AreEqual(-20m, result.Rows[0].Amount);
        Assert.IsNull(result.Rows[0].FundingSource);
        Assert.HasCount(1, FundingWarnings(result));
    }

    [TestMethod]
    public void Ally_IdenticalPurchasesHaveDistinctStableOccurrenceIds()
    {
        const string csv = "Date,Time,Amount,Type,Description\n2026-01-02,,-12.34,Withdrawal,SAMPLE STORE\n2026-01-02,,-12.34,Withdrawal,SAMPLE STORE\n";
        ParseResult first = new AllyTransactionParser().ParseText(csv);
        ParseResult repeated = new AllyTransactionParser().ParseText(csv);

        Assert.HasCount(2, first.Rows);
        Assert.AreNotEqual(first.Rows[0].ExternalTransactionId, first.Rows[1].ExternalTransactionId);
        Assert.AreEqual(first.Rows[0].ExternalTransactionId, repeated.Rows[0].ExternalTransactionId);
        Assert.AreEqual(first.Rows[1].ExternalTransactionId, repeated.Rows[1].ExternalTransactionId);
        Assert.AreEqual(-24.68m, first.Rows.Sum(row => row.Amount));
    }

    [TestMethod]
    public void NormalizedActivity_PreservesAccountAndFundingIdentity()
    {
        const string csv = "Date,Amount,Type,Description,Account,Transaction ID,Funding Source,Currency\n2026-04-01,-45.00,Purchase,SAMPLE STORE,PayPal Wallet,PP123,PayPal Smart Connect,USD\n";
        ParseResult result = new AccountActivityCsvParser().ParseText(csv);

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(1, result.Rows);
        Assert.AreEqual("PayPal Wallet", result.Rows[0].SourceAccountIdentity);
        Assert.AreEqual("PP123", result.Rows[0].ExternalTransactionId);
        Assert.AreEqual("PayPal Smart Connect", result.Rows[0].FundingSource);
        Assert.AreEqual(-45m, result.Rows[0].Amount);
    }

    [TestMethod]
    public void SmartConnect_NormalizedPurchasesAndPaymentsKeepTheirSigns()
    {
        const string csv = "Date,Amount,Type,Description\n2026-04-01,-45.00,Purchase,SAMPLE STORE\n2026-04-10,45.00,Payment,BANK PAYMENT\n2026-04-11,10.00,Refund,RETURN\n";
        ParseResult result = new PayPalSmartConnectCsvParser().ParseText(csv);

        Assert.IsEmpty(result.Errors);
        Assert.HasCount(3, result.Rows);
        Assert.AreEqual(-45m, result.Rows[0].Amount);
        Assert.AreEqual(45m, result.Rows[1].Amount);
        Assert.AreEqual(10m, result.Rows[2].Amount);
    }

    [TestMethod]
    public void SmartConnect_ConflictingSignIsRejectedInsteadOfGuessed()
    {
        ParseResult result = new PayPalSmartConnectCsvParser().ParseText("Date,Amount,Type,Description\n2026-04-01,-45.00,Payment,PAYMENT\n");
        Assert.IsEmpty(result.Rows);
        Assert.HasCount(1, result.Errors);
    }

    [TestMethod]
    public void NormalizedActivity_ConflictingSourceIdIsReported()
    {
        ParseResult result = new AccountActivityCsvParser().ParseText("Date,Amount,Type,Description,Transaction ID\n2026-04-01,-45.00,Purchase,STORE,ID1\n2026-04-01,-46.00,Purchase,STORE,ID1\n");
        Assert.HasCount(1, result.Rows);
        Assert.HasCount(1, result.Errors);
    }

    [TestMethod]
    public void NormalizedActivity_DoesNotAcceptForeignCurrencyOrSubcentRounding()
    {
        ParseResult result = new AccountActivityCsvParser().ParseText("Date,Amount,Type,Description,Currency\n2026-04-01,-45.001,Purchase,STORE,USD\n2026-04-01,-45.00,Purchase,STORE,EUR\n");
        Assert.IsEmpty(result.Rows);
        Assert.HasCount(2, result.Errors);
    }

    [TestMethod]
    public void AppleCard_OtherDebitAndReversalPreserveNativeAmountSign()
    {
        const string csv = "Transaction Date,Clearing Date,Description,Merchant,Category,Type,Amount (USD),Purchased By\n04/01/2026,04/01/2026,INTEREST,Issuer,Other,Other,12.34,Example User\n04/02/2026,04/02/2026,REVERSAL,Issuer,Other,Other,-12.34,Example User\n";
        ParseResult result = new AppleCardParser().ParseText(csv);
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual(-12.34m, result.Rows[0].Amount);
        Assert.AreEqual(12.34m, result.Rows[1].Amount);
    }

    [TestMethod]
    public void AmountParser_RejectsValuesThatCannotBeStoredWithoutChange()
    {
        Assert.IsFalse(ImportAmountParser.TryParse("12,34.56", out _));
        Assert.IsFalse(ImportAmountParser.TryParse("1.001", out _));
        Assert.IsTrue(ImportAmountParser.TryParse("123.4500", out decimal exactCents));
        Assert.AreEqual(123.45m, exactCents);
        Assert.IsFalse(ImportAmountParser.TryParse("10000000000000000.00", out _));
        Assert.IsFalse(ImportAmountParser.TryParse(decimal.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture), out _));
        Assert.IsTrue(ImportAmountParser.TryParse("(1,234.56)", out decimal parsed));
        Assert.AreEqual(-1234.56m, parsed);
    }

    [TestMethod]
    public void PayPal_AlwaysIdentifiesNativeActivityAsAnIncompleteBalanceLedger()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement("04/01/2026\nPurchase: SAMPLE STORE\nUSD\n-20.00\n0.00\n-20.00\nBANK - Checking x-1234\nUSD\nID: ABC123"));
        Assert.HasCount(1, result.Rows);
        Assert.IsTrue(result.Warnings.Any(warning => warning.RowNumber == 0 && warning.Message.Contains("not a complete PayPal balance ledger", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PayPal_PaymentRefundPreservesItsMeaningAndSourceDescription()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement("04/01/2026\nPayment Refund:\nSAMPLE STORE\nUSD\n20.00\n0.00\n20.00"));
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual("Refund", result.Rows.Single().Type);
        Assert.AreEqual("Payment Refund: SAMPLE STORE", result.Rows.Single().Memo);
    }

    [TestMethod]
    public void PayPal_ExplicitBankFundingCreditIsATransfer()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement("04/01/2026\nBank Deposit to PP Account\nUSD\n20.00\n0.00\n20.00"));
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual("Transfer", result.Rows.Single().Type);
        Assert.AreEqual("Bank Deposit to PP Account", result.Rows.Single().Memo);
    }

    [TestMethod]
    public void PayPal_UnrecognizedPositiveActivityRequiresExplicitClassification()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement("04/01/2026\nUNKNOWN ADJUSTMENT\nUSD\n20.00\n0.00\n20.00"));
        Assert.IsEmpty(result.Rows);
        Assert.HasCount(1, result.Errors);
        Assert.IsTrue(result.Errors[0].Message.Contains("explicit Deposit, Refund, Transfer, or Payment", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PayPal_UnrecognizedDebitRemainsVisibleWithItsSourceDescriptor()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement("04/01/2026\nUNKNOWN DEBIT\nUSD\n-20.00\n0.00\n-20.00"));
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual("PayPal Unclassified", result.Rows.Single().Type);
        Assert.AreEqual("UNKNOWN DEBIT", result.Rows.Single().Memo);
        Assert.IsTrue(result.Warnings.Any(warning => warning.Message.Contains("activity type is unrecognized", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PayPal_PositivePreapprovedActivityDoesNotGuessIncomeOrRefund()
    {
        ParseResult result = new PayPalStatementParser().ParseText(Statement("04/01/2026\nPreApproved Payment Bill User Payment: SAMPLE STORE\nUSD\n20.00\n0.00\n20.00"));
        Assert.IsEmpty(result.Rows);
        Assert.HasCount(1, result.Errors);
    }

    private static IReadOnlyList<ParseError> FundingWarnings(ParseResult result) => result.Warnings.Where(warning => warning.RowNumber > 0 && warning.Message.Contains("funding", StringComparison.OrdinalIgnoreCase)).ToArray();
    private static string Statement(string activity) => "PAYPAL ACCOUNT\nACCOUNT ACTIVITY\nDATE DESCRIPTION CURRENCY AMOUNT FEES TOTAL*\n" + activity;
}
