using PersonalFinanceManager.Core.Imports;

namespace PersonalFinanceManager.Tests.Imports
{
    [TestClass]
    public sealed class AllyTransactionParserTests
    {
        [TestMethod]
        public void ParseText_ParsesQuotedDescriptionWithCommas()
        {
            string csv = "Date, Time, Amount, Type, Description\r\n2026-01-02, 03:04:05, -12.34, Withdrawal, \"SAMPLE STORE, SAMPLE CITY\"\r\n";
            AllyTransactionParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.HasCount(1, result.Rows);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("SAMPLE STORE, SAMPLE CITY", result.Rows[0].Description);
            Assert.AreEqual("Withdrawal", result.Rows[0].Type);
        }

        [TestMethod]
        public void ParseText_TrimsHeadersWithLeadingSpaces()
        {
            string csv = "Date, Time, Amount, Type, Description\n2026-01-02, 03:04:05, 100.00, Deposit, SAMPLE PAYROLL\n";
            AllyTransactionParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.HasCount(1, result.Rows);
            Assert.AreEqual(100.00m, result.Rows[0].Amount);
        }

        [TestMethod]
        public void ParseText_TrimsFieldPaddingButPreservesDescriptionContent()
        {
            string csv = "Date, Time, Amount, Type, Description\r\n2026-01-02, 03:04:05, -12.34, Withdrawal, SAMPLE STORE LOCATION\r\n";
            AllyTransactionParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.HasCount(1, result.Rows);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("Withdrawal", result.Rows[0].Type);
            Assert.AreEqual("SAMPLE STORE LOCATION", result.Rows[0].Description);
        }
    }
}
