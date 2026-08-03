using PersonalFinanceManager.Core.Imports;

namespace PersonalFinanceManager.Tests.Imports
{
    [TestClass]
    public sealed class ChaseCreditCardParserTests
    {
        [TestMethod]
        public void ParseText_ParsesSalesPaymentsFeesAndReturns()
        {
            string csv = """
            Transaction Date,Post Date,Description,Category,Type,Amount,Memo
            07/26/2026,07/27/2026,SAMPLE STORE,Shopping,Sale,-42.76,
            07/26/2026,07/26/2026,Payment Thank You - Web,,Payment,1307.22,
            12/31/2025,12/31/2025,SAMPLE RETURN,Shopping,Return,4.00,
            02/23/2025,02/23/2025,SAMPLE INTEREST,Fees & Adjustments,Fee,-50.70,
            """;

            ChaseCreditCardParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.HasCount(4, result.Rows);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual(new DateOnly(2026, 7, 26), result.Rows[0].Date);
            Assert.AreEqual(new DateOnly(2026, 7, 27), result.Rows[0].PostingDate);
            Assert.AreEqual("SAMPLE STORE", result.Rows[0].Description);
            Assert.AreEqual("Shopping", result.Rows[0].SourceCategory);
            Assert.AreEqual("Sale", result.Rows[0].Type);
            Assert.AreEqual(-42.76m, result.Rows[0].Amount);
            Assert.AreEqual("Payment", result.Rows[1].Type);
            Assert.AreEqual(1307.22m, result.Rows[1].Amount);
            Assert.IsTrue(result.Rows[0].ExternalTransactionId?.StartsWith("CHASE:", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ParseText_ReportsInvalidDate()
        {
            string csv = """
            Transaction Date,Post Date,Description,Category,Type,Amount,Memo
            bad-date,07/27/2026,SAMPLE STORE,Shopping,Sale,-42.76,
            """;

            ChaseCreditCardParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.IsEmpty(result.Rows);
            Assert.HasCount(1, result.Errors);
        }
    }
}
