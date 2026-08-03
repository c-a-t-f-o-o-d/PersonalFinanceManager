using PersonalFinanceManager.Data.Sql;

namespace PersonalFinanceManager.Tests.Sql
{
    [TestClass]
    public sealed class SqlResourceReaderTests
    {
        [TestMethod]
        public void Read_LoadsEmbeddedReportSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("Reports/GetExpenseMetricSummary.sql");

            Assert.IsTrue(sql.Contains("TotalExpenses", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("dbo.ImportedTransaction", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Read_LoadsEmbeddedTransactionSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("Transactions/GetTransactions.sql");

            Assert.IsTrue(sql.Contains("ImportedTransactionId", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("/*WHERE_CLAUSE*/", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Read_LoadsEmbeddedDuplicateReviewSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("DuplicateReview/GetImportDuplicateRows.sql");

            Assert.IsTrue(sql.Contains("DuplicateConfidence", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("/*REVIEW_KIND*/", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Read_LoadsEmbeddedImportServiceSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("ImportService/InsertImportRow.sql");

            Assert.IsTrue(sql.Contains("dbo.ImportRow", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("DuplicateConfidence", StringComparison.Ordinal));
        }



        [TestMethod]
        public void Read_LoadsEmbeddedDataHealthSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("DataHealth/Counts/MissingBalances.sql");

            Assert.IsTrue(sql.Contains("dbo.Account", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("CurrentBalance", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Read_LoadsEmbeddedTransactionLinkSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("TransactionLinks/GetLinks.sql");

            Assert.IsTrue(sql.Contains("dbo.TransactionLink", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("/*WHERE_CLAUSE*/", StringComparison.Ordinal));
        }



        [TestMethod]
        public void Read_LoadsEmbeddedAccountSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("Accounts/GetAccounts.sql");

            Assert.IsTrue(sql.Contains("dbo.Account", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("AccountNickname", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Read_LoadsEmbeddedRuleSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("Rules/InsertRule.sql");

            Assert.IsTrue(sql.Contains("dbo.FinancialRule", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("IncomeOwner", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Read_LoadsEmbeddedRecurringBillSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("RecurringBills/GetMonthlyCandidates.sql");

            Assert.IsTrue(sql.Contains("dbo.ImportedTransaction", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("AlreadyExists", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Read_LoadsEmbeddedBackupExportSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("BackupExport/GetHistory.sql");

            Assert.IsTrue(sql.Contains("dbo.DatabaseBackupHistory", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("BackupType", StringComparison.Ordinal));
        }



        [TestMethod]
        public void Read_LoadsEmbeddedDemoDataSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("Demo/LoadDemoData.sql");

            Assert.IsTrue(sql.Contains("Demo Checking", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("Demo data can only be loaded", StringComparison.Ordinal));
        }



        [TestMethod]
        public void Read_LoadsEmbeddedSchemaSql()
        {
            SqlResourceReader reader = new();

            string trackingSql = reader.Read("Schema/EnsureMigrationTrackingTable.sql");
            string statusSql = reader.Read("Schema/GetLatestMigration.sql");

            Assert.IsTrue(trackingSql.Contains("SchemaMigration", StringComparison.Ordinal));
            Assert.IsTrue(statusSql.Contains("AppliedAtUtc", StringComparison.Ordinal));
        }



        [TestMethod]
        public void Read_LoadsEmbeddedSalaryReportSql()
        {
            SqlResourceReader reader = new();

            string sql = reader.Read("Reports/Salary/GetSalarySpendingScopeOptions.sql");
            string fragment = reader.Read("Reports/Fragments/ExpenseWhere.sql");

            Assert.IsTrue(sql.Contains("SalarySpending", StringComparison.Ordinal) || sql.Contains("SpendingClass", StringComparison.Ordinal));
            Assert.IsTrue(sql.Contains("/*EXPENSE_WHERE*/", StringComparison.Ordinal));
            Assert.IsTrue(fragment.Contains("TransactionLink", StringComparison.Ordinal));
            Assert.IsTrue(fragment.Contains("/*ALIAS*/", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Read_ThrowsForMissingResource()
        {
            SqlResourceReader reader = new();

            bool exceptionThrown = false;

            try
            {
                reader.Read("Reports/Missing.sql");
            }
            catch (InvalidOperationException)
            {
                exceptionThrown = true;
            }

            Assert.IsTrue(exceptionThrown, "Expected InvalidOperationException.");
        }
    }
}
