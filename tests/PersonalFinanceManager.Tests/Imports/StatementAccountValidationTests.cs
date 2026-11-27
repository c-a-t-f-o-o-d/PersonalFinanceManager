using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Tests.Imports;

[TestClass]
public sealed class StatementAccountValidationTests
{
    [TestMethod]
    public void HeaderCurrencyAndAccountIdentityAreCheckedWithoutTransactionRows()
    {
        Account account = new() { AccountNickname = "Example Checking", AccountType = "Checking", AccountNumber = "00001111", AccountNumberLastFour = "1111" };
        Assert.ThrowsExactly<InvalidOperationException>(() => ImportAccountValidation.Validate(account, ImportProfileCodes.OfxQfx,
            new ParseResult { Currency = "EUR", SourceAccountIdentity = "00001111", SourceAccountType = "CHECKING" }));
        Assert.ThrowsExactly<InvalidOperationException>(() => ImportAccountValidation.Validate(account, ImportProfileCodes.OfxQfx,
            new ParseResult { Currency = "USD", SourceAccountIdentity = "00002222", SourceAccountType = "CHECKING" }));
        ImportAccountValidation.Validate(account, ImportProfileCodes.OfxQfx,
            new ParseResult { Currency = "USD", SourceAccountIdentity = "00001111", SourceAccountType = "CHECKING" });
    }

    [TestMethod]
    public void AStatementCannotCrossBankAndCreditAccountKinds()
    {
        foreach ((string source, string target) in new[] { ("CREDITCARD", "Checking"), ("SAVINGS", "Credit Card"), ("CHECKING", "Digital Wallet"), ("CASH", "Investment"),
            ("SAVINGS", "Checking"), ("CHECKING", "Savings"), ("MONEYMRKT", "Checking") })
            Assert.ThrowsExactly<InvalidOperationException>(() => ImportAccountValidation.Validate(
                new Account { AccountNickname = "Example", AccountType = target }, ImportProfileCodes.OfxQfx,
                new ParseResult { Currency = "USD", SourceAccountType = source }));
    }

    [TestMethod]
    public void GenericCreditProfileEnforcesPaymentDirectionAndLiabilityAccount()
    {
        ParseResult result = new CreditAccountCsvParser().ParseText("Date,Amount,Type,Description\n2026-04-01,100,Payment,Example repayment\n2026-04-01,-100,Payment,Wrong direction\n");
        Assert.HasCount(1, result.Rows);
        Assert.HasCount(1, result.Errors);
        Assert.ThrowsExactly<InvalidOperationException>(() => ImportAccountValidation.Validate(
            new Account { AccountType = "Checking" }, ImportProfileCodes.CreditAccountCsv, result));
        ImportAccountValidation.Validate(new Account { AccountType = "Line of Credit" }, ImportProfileCodes.CreditAccountCsv, result);
    }
}
