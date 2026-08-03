using PersonalFinanceManager.Core.Imports;

namespace PersonalFinanceManager.Tests.Imports
{
    [TestClass]
    public sealed class RegionsBankParserTests
    {
        [TestMethod]
        public void ParseText_ParsesCheckingDebitsAndCredits()
        {
            string csv = """
            "Account","Transaction Date","Posted Date","No.","Description","Debit","Credit","Long Description"
            "FREE INT CHECKING * 1234","05/02/2026","05/04/2026","","Sample Store","-9.8700","","SAMPLE STORE ANYTOWN TN 1234"
            "FREE INT CHECKING * 1234","05/15/2026","05/15/2026","","Sample Payroll","","100.0000","SAMPLE PAYROLL"
            """;

            RegionsBankParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.HasCount(2, result.Rows);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual(new DateOnly(2026, 5, 2), result.Rows[0].Date);
            Assert.AreEqual(new DateOnly(2026, 5, 4), result.Rows[0].PostingDate);
            Assert.AreEqual(-9.87m, result.Rows[0].Amount);
            Assert.AreEqual("Withdrawal", result.Rows[0].Type);
            Assert.AreEqual("Sample Store", result.Rows[0].Description);
            Assert.AreEqual("Sample Store", result.Rows[0].Merchant);
            Assert.AreEqual(100.00m, result.Rows[1].Amount);
            Assert.AreEqual("Deposit", result.Rows[1].Type);
            Assert.AreEqual("Payroll", result.Rows[1].SourceCategory);
            Assert.AreEqual("Payroll", result.Rows[1].SourceCategoryType);
            Assert.IsTrue(result.Rows[0].ExternalTransactionId?.StartsWith("REGIONS:", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ParseText_MarksSavingsTransfersAndInterest()
        {
            string csv = """
            "Account","Transaction Date","Posted Date","No.","Description","Debit","Credit","Long Description"
            "JOINT SAVINGS * 1234","05/22/2026","05/22/2026","","EB From Checking 9999","","123.4500","EB FROM CHECKING # xxxxxx9999 REF# 000000 1234567"
            "JOINT SAVINGS * 1234","05/29/2026","05/29/2026","","Interest Payment","","0.2500","INTEREST PAYMENT"
            "JOINT SAVINGS * 1234","05/30/2026","05/30/2026","","EB To Checking 9999","-321.0000","","EB TO CHECKING # xxxxxx9999 REF# 000000 1234568"
            "FREE INT CHECKING * 1234","06/01/2026","06/01/2026","","EB To Savings 9999","-45.6700","","EB TO SAVINGS # xxxxxx9999 REF# 000000 1234569"
            """;

            RegionsBankParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.HasCount(4, result.Rows);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual("Transfer", result.Rows[0].SourceCategory);
            Assert.AreEqual("Checking -> Savings", result.Rows[0].SourceCategoryType);
            Assert.AreEqual("Interest", result.Rows[1].SourceCategory);
            Assert.AreEqual("Yield", result.Rows[1].SourceCategoryType);
            Assert.AreEqual("Transfer", result.Rows[2].SourceCategory);
            Assert.AreEqual("Savings -> Checking", result.Rows[2].SourceCategoryType);
            Assert.AreEqual("Checking -> Savings", result.Rows[3].SourceCategoryType);
        }

        [TestMethod]
        public void ParseText_ReportsRowsWithBothDebitAndCredit()
        {
            string csv = """
            "Account","Transaction Date","Posted Date","No.","Description","Debit","Credit","Long Description"
            "FREE INT CHECKING * 1234","05/02/2026","05/04/2026","","Sample Store","-9.8700","1.0000","SAMPLE STORE"
            """;

            RegionsBankParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.IsEmpty(result.Rows);
            Assert.HasCount(1, result.Errors);
        }

        [TestMethod]
        public void ParseText_DoesNotAutomaticallyExcludeVenmoTransfers()
        {
            string csv = """
            "Account","Transaction Date","Posted Date","No.","Description","Debit","Credit","Long Description"
            "FREE INT CHECKING * 1234","05/11/2026","05/12/2026","","Transfer to Venmo","-30.0000","","VENMO *SAMPLE PERSON 4829 Visa Direct NY 10014 1234"
            """;

            RegionsBankParser parser = new();
            ParseResult result = parser.ParseText(csv);
            Assert.HasCount(1, result.Rows);
            Assert.IsEmpty(result.Errors);
            Assert.IsNull(result.Rows[0].SourceCategory);
            Assert.IsNull(result.Rows[0].SourceCategoryType);
        }

    }
}
