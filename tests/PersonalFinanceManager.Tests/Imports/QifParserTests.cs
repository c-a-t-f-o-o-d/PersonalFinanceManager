using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Tests.Imports;

[TestClass]
public sealed class QifParserTests
{
    [TestMethod]
    public void PreservesSignedAmountsCategoriesAndRepeatedPurchases()
    {
        string activity = "D4/12/2026\nT-24.50\nPExample store\nLFood\nN1234\n^\n";
        ParseResult result = new QifParser().ParseText("!Type:Bank\n" + activity + activity);
        Assert.IsEmpty(result.Errors);
        Assert.HasCount(2, result.Rows);
        Assert.AreEqual(-49m, result.Rows.Sum(row => row.Amount));
        Assert.AreEqual("Food", result.Rows[0].SourceCategory);
        Assert.AreEqual("Reference: 1234", result.Rows[0].Memo);
        Assert.AreNotEqual(result.Rows[0].ExternalTransactionId, result.Rows[1].ExternalTransactionId);
        Assert.AreEqual(result.Rows[1].ExternalTransactionId, new QifParser().ParseText("!Type:Bank\n" + activity + activity).Rows[1].ExternalTransactionId);
    }

    [TestMethod]
    public void AccountHeaderIsValidatedBeforeImportIncludingAnEmptyStatement()
    {
        ParseResult result = new QifParser().ParseText("!Account\nNExample Checking\nTBank\n^\n!Type:Bank\n");
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual("BANK", result.SourceAccountType);
        Assert.ThrowsExactly<InvalidOperationException>(() => ImportAccountValidation.Validate(
            new Account { AccountNickname = "Different Checking", AccountType = "Checking" }, ImportProfileCodes.Qif, result));
        ImportAccountValidation.Validate(new Account { AccountNickname = "Example Checking", AccountType = "Checking" }, ImportProfileCodes.Qif, result);
    }

    [TestMethod]
    public void ReferenceIdentityRemainsStableAcrossOverlappingDownloads()
    {
        const string first = "D4/12/2026\nT-25\nPExample store\nN101\n^\n";
        const string second = "D4/12/2026\nT-25\nPExample store\nN102\n^\n";
        QifParser parser = new();
        string? existing = parser.ParseText("!Type:Bank\n" + second).Rows.Single().ExternalTransactionId;
        ParseResult combined = parser.ParseText("!Type:Bank\n" + first + second);
        Assert.AreNotEqual(existing, combined.Rows[0].ExternalTransactionId);
        Assert.AreEqual(existing, combined.Rows[1].ExternalTransactionId);
    }

    [TestMethod]
    public void CreditRepaymentsRequireAnExplicitTransferCategory()
    {
        ParseResult result = new QifParser().ParseText("!Type:CCard\nD2026-04-12\nT150.00\nPPayment\nL[Example Checking]\n^\nD2026-04-13\nT10.00\nPCredit\n^\n");
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual("Payment", result.Rows[0].Type);
        Assert.AreEqual("Transfer", result.Rows[0].SourceCategory);
        Assert.AreEqual(ImportClassification.UnclassifiedCredit, result.Rows[1].Type);
        Assert.IsTrue(result.Warnings.Any(warning => warning.RowNumber == result.Rows[1].RowNumber));
        Assert.ThrowsExactly<InvalidOperationException>(() => ImportAccountValidation.Validate(
            new Account { AccountNickname = "Example Checking", AccountType = "Checking" }, ImportProfileCodes.Qif, result));
    }

    [TestMethod]
    public void SplitTotalsAreImportedOnceAndMixedTransfersRequireReview()
    {
        ParseResult result = new QifParser().ParseText("!Type:Bank\nD4/12/2026\nT-100\nPExample payment\nS[Loan]\n$-70\nSInterest\n$-30\n^\n");
        Assert.IsEmpty(result.Errors);
        Assert.AreEqual(-100m, result.Rows.Single().Amount);
        Assert.AreEqual(ImportClassification.UnclassifiedDebit, result.Rows[0].Type);
        Assert.IsNull(result.Rows[0].SourceCategory);
        StringAssert.Contains(result.Rows[0].Memo!, "[Loan] -70");
    }

    [TestMethod]
    public void OpeningBalanceDoesNotBecomePeriodActivityOrDuplicateTheStatementBalance()
    {
        ParseResult result = new QifParser().ParseText("!Type:Bank\nD4/1/2026\nT250\nPOpening Balance\nL[Example Checking]\n^\nD4/2/2026\nT-10\nPExample purchase\n^\n");
        Assert.AreEqual(-10m, result.Rows.Single().Amount);
        Assert.IsTrue(result.Warnings.Any(warning => warning.Message.Contains("opening-balance initializer", StringComparison.Ordinal)));
        AccountReconciliation check = AccountReconciliationCalculator.Calculate(new AccountReconciliationRequest
        {
            AccountId = 1, FromDate = new(2026, 4, 1), ToDate = new(2026, 4, 30), OpeningBalance = 250m, ClosingBalance = 240m
        }, "Checking", result.Rows.Select(row => new ImportedTransaction { AccountId = 1, TransactionDate = row.Date, Amount = row.Amount }));
        Assert.IsTrue(check.IsBalanced);
    }

    [TestMethod]
    public void RejectsInvalidDatesAmountsSplitsAndDuplicateFields()
    {
        foreach (string entry in new[]
        {
            "D4/12/26\nT-1", "D31/12/2026\nT-1", "D4/12/2026\nT-1.001", "D4/12/2026\nT-1\nU-2",
            "D4/12/2026\nT-1\nT-1", "D4/12/2026\nT-100\nSFood\n$-50", "D4/12/2026\nT-100\n$-100"
        })
        {
            ParseResult result = new QifParser().ParseText("!Type:Bank\n" + entry + "\n^\n");
            Assert.IsEmpty(result.Rows, entry);
            Assert.IsNotEmpty(result.Errors, entry);
        }
    }

    [TestMethod]
    public void UnsupportedOrTruncatedSectionsRejectTheWholeFile()
    {
        const string valid = "!Type:Bank\nD4/12/2026\nT-1\nPExample store\n^\n";
        foreach (string suffix in new[] { "!Type:Invst\n", "!Type:Bank\n", "!Account\nNAnother\nTBank\n^\n", "D4/13/2026\nT-2\n" })
        {
            ParseResult result = new QifParser().ParseText(valid + suffix);
            Assert.IsEmpty(result.Rows);
            Assert.IsNotEmpty(result.Errors);
        }
    }

    [TestMethod]
    public void CurrencyAndDateAssumptionsAreVisible()
    {
        ParseResult result = new QifParser().ParseText("!Type:Cash\nD4/12/2026\nT-2\nPExample store\n^\n");
        Assert.AreEqual("USD", result.Currency);
        Assert.IsTrue(result.Warnings.Any(warning => warning.RowNumber == 0 && warning.Message.Contains("QIF does not declare currency", StringComparison.Ordinal)));
        Assert.AreEqual("CASH", result.SourceAccountType);
    }
}
