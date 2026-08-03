using PersonalFinanceManager.Core.Imports;

namespace PersonalFinanceManager.Tests.Imports
{
    [TestClass]
    public sealed class PayPalStatementParserTests
    {
        [TestMethod]
        public void ParseText_ParsesPayPalStatementRowsAndFundingSources()
        {
            string text = """
            PAYPAL ACCOUNT
            ACCOUNT ACTIVITY
            DATE DESCRIPTION CURRENCY AMOUNT FEES TOTAL*
            04/01/2026
            PreApproved Payment Bill User Payment:
            SAMPLE SUBSCRIPTION
            USD
            -6.57
            0.00
            -6.57
            04/04/2026
            Buyer Credit Payment
            USD
            -8.77
            0.00
            -8.77
            ALLY BANK - Checking x-1234
            USD
            ID: ABC123
            PayPal Smart Connect
            USD
            ID: DEF456
            *For each transaction in your Account Activity
            """;

            PayPalStatementParser parser = new();
            ParseResult result = parser.ParseText(text);
            Assert.HasCount(2, result.Rows);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("PreApproved Payment", result.Rows[0].Type);
            Assert.AreEqual("SAMPLE SUBSCRIPTION", result.Rows[0].Description);
            Assert.AreEqual("ALLY BANK - Checking x-1234", result.Rows[0].FundingSource);
            Assert.AreEqual("ABC123", result.Rows[0].ExternalTransactionId);
            Assert.AreEqual("Buyer Credit Payment", result.Rows[1].Type);
            Assert.AreEqual("PayPal Smart Connect", result.Rows[1].FundingSource);
        }
    }
}
