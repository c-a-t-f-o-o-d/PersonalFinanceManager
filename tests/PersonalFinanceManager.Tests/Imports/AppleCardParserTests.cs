using PersonalFinanceManager.Core.Imports;

namespace PersonalFinanceManager.Tests.Imports
{
    [TestClass]
    public sealed class AppleCardParserTests
    {
        [TestMethod]
        public void ParseText_NormalizesPurchasesPaymentsAndCategories()
        {
            string csv = """
            Transaction Date,Clearing Date,Description,Merchant,Category,Type,Amount (USD),Purchased By
            07/26/2026,07/26/2026,"ACH DEPOSIT INTERNET TRANSFER FROM ACCOUNT ENDING IN 1234","Ach Deposit Internet Transfer From Account Ending In 1234","Payment","Payment","-41.87","Example User"
            07/23/2026,07/24/2026,"SAMPLE RESTAURANT 100 MAIN ST ANYTOWN TN USA","Sample Restaurant","Restaurants","Purchase","13.22","Example User"
            03/14/2025,03/14/2025,"SAMPLE MERCHANT (RETURN)","Sample Merchant (return)","Credit","Credit","-79.79","Example User"
            """;

            AppleCardParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.HasCount(3, result.Rows);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("Payment", result.Rows[0].Type);
            Assert.AreEqual(41.87m, result.Rows[0].Amount);
            Assert.AreEqual("Transfer", result.Rows[0].SourceCategory);
            Assert.AreEqual("Credit Card Payment", result.Rows[0].SourceCategoryType);
            Assert.AreEqual("Purchase", result.Rows[1].Type);
            Assert.AreEqual(-13.22m, result.Rows[1].Amount);
            Assert.AreEqual("Dining", result.Rows[1].SourceCategory);
            Assert.AreEqual("Restaurant", result.Rows[1].SourceCategoryType);
            Assert.AreEqual(79.79m, result.Rows[2].Amount);
            Assert.AreEqual("Refund", result.Rows[2].SourceCategory);
            Assert.IsTrue(result.Rows[0].ExternalTransactionId?.StartsWith("APPLECARD:", System.StringComparison.Ordinal));
        }

        [TestMethod]
        public void ParseText_ReportsInvalidAmount()
        {
            string csv = """
            Transaction Date,Clearing Date,Description,Merchant,Category,Type,Amount (USD),Purchased By
            07/26/2026,07/26/2026,"SAMPLE","Sample","Restaurants","Purchase","bad","Example User"
            """;

            AppleCardParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.IsEmpty(result.Rows);
            Assert.HasCount(1, result.Errors);
        }
    }
}
