using System.Data;
using System.Globalization;
using System.IO.Compression;
using Microsoft.Data.SqlClient;
using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Database;
using PersonalFinanceManager.Data.Logging;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Data.Security;
using PersonalFinanceManager.Data.Services;
using PersonalFinanceManager.Data.Sql;

namespace PersonalFinanceManager.Tests.Integration;

[TestClass]
[DoNotParallelize]
public sealed class FinancialTrackingSqlTests
{
    private const string DatabasePrefix = "PFM_Integrity_";
    private string _databaseName = string.Empty;
    private string _directory = string.Empty;
    private SqlConnectionFactory? _connections;
    private DatabaseInitializer _initializer = null!;
    private DatabaseInitializationResult _initialization = null!;
    private AccountRepository _accounts = null!;
    private RuleRepository _rules = null!;
    private ImportService _imports = null!;
    private TransactionRepository _transactions = null!;
    private TransactionLinkRepository _links = null!;
    private DuplicateReviewRepository _duplicates = null!;
    private ReportRepository _reports = null!;

    [TestInitialize]
    public async Task CreateIsolatedDatabase()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("PFM_RUN_SQL_TESTS") != "1")
        {
            Assert.Inconclusive("SQL integration tests require Windows LocalDB and PFM_RUN_SQL_TESTS=1.");
        }

        _databaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
        _directory = Path.Combine(Path.GetTempPath(), "PersonalFinanceManager.Tests", _databaseName);
        Directory.CreateDirectory(_directory);
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = _databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Pooling = false,
            ConnectTimeout = 30
        };
        _connections = new SqlConnectionFactory(builder.ConnectionString);
        _initializer = new DatabaseInitializer(_connections, new FileLogger());
        _initialization = await _initializer.InitializeAsync();
        Assert.IsTrue(_initialization.ExecutedCount > 0, "Migration files must be copied to the test output directory.");
        _accounts = new AccountRepository(_connections, new AccountDataProtector());
        _rules = new RuleRepository(_connections);
        _imports = new ImportService(_connections, _rules);
        _transactions = new TransactionRepository(_connections);
        _links = new TransactionLinkRepository(_connections);
        _duplicates = new DuplicateReviewRepository(_connections);
        _reports = new ReportRepository(_connections);
    }

    [TestCleanup]
    public async Task DropIsolatedDatabase()
    {
        if (_connections is not null && _databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal)
            && Guid.TryParseExact(_databaseName[DatabasePrefix.Length..], "N", out _))
        {
            await using SqlConnection master = _connections.CreateMasterConnection();
            await master.OpenAsync();
            await using SqlCommand command = master.CreateCommand();
            command.CommandTimeout = 60;
            command.CommandText = """
                IF DB_ID(@DatabaseName) IS NOT NULL
                BEGIN
                    DECLARE @Sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@DatabaseName)
                        + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@DatabaseName) + N';';
                    EXEC sys.sp_executesql @Sql;
                END;
                """;
            command.Parameters.Add(new SqlParameter("@DatabaseName", SqlDbType.NVarChar, 128) { Value = _databaseName });
            await command.ExecuteNonQueryAsync();
        }

        if (!string.IsNullOrEmpty(_directory) && Directory.Exists(_directory)
            && string.Equals(Path.GetFileName(_directory), _databaseName, StringComparison.Ordinal))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task MigrationsAndBackupUseTheConfiguredDatabase()
    {
        DatabaseInitializationResult repeated = await _initializer.InitializeAsync();
        Assert.AreEqual(0, repeated.ExecutedCount);
        Assert.AreEqual(0, repeated.FailedCount);
        Assert.AreEqual(_initialization.Migrations.Count, repeated.Migrations.Count);
        Assert.AreEqual(_databaseName, await ScalarAsync<string>("SELECT DB_NAME();"));
        Assert.AreEqual(1, await ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ImportRow') AND name = N'AcceptedTransactionId';"));

        DemoDataLoadResult demo = await new DemoDataService(_connections!).LoadDemoDataAsync();
        Assert.IsTrue(demo.TransactionsCreated > 0);
        Assert.IsEmpty((await _reports.GetExpenseReportAsync(new ExpenseReportFilter())).Warnings);

        DatabaseBackupResult backup = await new DatabaseBackupService(_connections!).CreateSqlBackupAsync(_directory);
        Assert.IsTrue(backup.Succeeded, backup.Message);
        Assert.IsTrue(backup.Verified, backup.Message);
        await using SqlConnection master = _connections!.CreateMasterConnection();
        await master.OpenAsync();
        await using SqlCommand command = master.CreateCommand();
        command.CommandText = "RESTORE HEADERONLY FROM DISK = @Path;";
        command.Parameters.Add(new SqlParameter("@Path", SqlDbType.NVarChar, 1000) { Value = backup.FilePath });
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        Assert.AreEqual(_databaseName, reader.GetString(reader.GetOrdinal("DatabaseName")));

        DatabaseBackupResult export = await new DatabaseBackupService(_connections!).ExportDataZipAsync(_directory);
        Assert.IsTrue(export.Succeeded, export.Message);
        using ZipArchive archive = ZipFile.OpenRead(export.FilePath);
        Assert.IsNotNull(archive.GetEntry("transaction_links.csv"));
        Assert.IsNotNull(archive.GetEntry("statement_checks.csv"));
        using StreamReader transactionExport = new(archive.GetEntry("transactions.csv")!.Open());
        string transactionHeader = (await transactionExport.ReadLineAsync())!;
        Assert.IsTrue(transactionHeader.Contains("PostingDate", StringComparison.Ordinal));
        Assert.IsTrue(transactionHeader.Contains("ClassificationIsManual", StringComparison.Ordinal));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RepeatedAllyImportPreservesMultiplicityAndAllowsExactRecovery()
    {
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        string file = await FileAsync("Date,Time,Amount,Type,Description\n2026-04-01,,-12.34,Withdrawal,REPEATED STORE\n2026-04-01,,-12.34,Withdrawal,REPEATED STORE\n");
        ImportSummary first = await _imports.ImportAsync(checking.AccountId, ImportProfileCodes.AllyCheckingCsv, file);
        ImportSummary repeated = await _imports.ImportAsync(checking.AccountId, ImportProfileCodes.AllyCheckingCsv, file);
        Assert.AreEqual(2, first.InsertedRows);
        Assert.AreEqual(0, repeated.InsertedRows);
        Assert.AreEqual(2, repeated.ExactDuplicateRows);
        Assert.HasCount(2, await RowsAsync());
        Assert.AreEqual(24.68m, await ExpensesAsync());

        IReadOnlyList<DuplicateReviewItem> skipped = await _duplicates.GetAsync(new DuplicateReviewFilter { Status = DuplicateReviewStatuses.ExactDuplicateSkipped });
        Assert.HasCount(2, skipped);
        long recovered = await _duplicates.ApproveAsync(skipped[0].ImportRowId);
        long replayed = await _duplicates.ApproveAsync(skipped[0].ImportRowId);
        Assert.AreEqual(recovered, replayed);
        Assert.HasCount(3, await RowsAsync());
        Assert.AreEqual(37.02m, await ExpensesAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConcurrentDuplicateAcceptanceInsertsOnceAndDeletionClearsOrigin()
    {
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        await ImportAsync(checking, [new(1, -25m, "Withdrawal", "ORIGINAL STORE", "EVENT1")]);
        ImportSummary changed = await ImportAsync(checking, [new(1, -25m, "Withdrawal", "CORRECTED STORE", "EVENT1")]);
        Assert.AreEqual(1, changed.DuplicateReviewRows);
        DuplicateReviewItem candidate = (await _duplicates.GetAsync(new DuplicateReviewFilter())).Single();

        long[] accepted = await Task.WhenAll(_duplicates.ApproveAsync(candidate.ImportRowId), _duplicates.ApproveAsync(candidate.ImportRowId));
        Assert.AreEqual(accepted[0], accepted[1]);
        Assert.HasCount(2, await RowsAsync());
        Assert.AreEqual(1, await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ImportRow WHERE [Status] = N'DuplicateAccepted' AND AcceptedTransactionId IS NOT NULL;"));

        await _transactions.DeleteAsync(accepted[0]);
        Assert.HasCount(1, await RowsAsync());
        Assert.AreEqual(1, await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ImportRow WHERE [Status] = N'Deleted' AND AcceptedTransactionId IS NULL;"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _duplicates.ApproveAsync(candidate.ImportRowId));
        await _transactions.DeleteAsync((await RowsAsync()).Single().ImportedTransactionId);
        Assert.AreEqual(0, await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ImportRow WHERE DuplicateMatchTransactionId IS NOT NULL OR AcceptedTransactionId IS NOT NULL;"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConflictingSourceAccountIsRejectedBeforeAnyImportWrite()
    {
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        string file = await FileAsync("Date,Amount,Type,Description,Account,Transaction ID\n2026-04-01,-25.00,Withdrawal,STORE,Different Account,TX1\n");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _imports.PreviewAsync(checking.AccountId, ImportProfileCodes.AccountActivityCsv, file));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _imports.ImportAsync(checking.AccountId, ImportProfileCodes.AccountActivityCsv, file));
        Assert.HasCount(0, await RowsAsync());
        Assert.AreEqual(0, await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ImportBatch;"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CheckingSavingsTransferReconcilesBalancesWithoutHouseholdSpending()
    {
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        Account savings = await AccountAsync("Emergency Savings", "Savings", "2222");
        await ImportAsync(checking, [new(5, -1000m, "Withdrawal", "TRANSFER TO SAVINGS 2222", "TRANSFER-A")]);
        await ImportAsync(savings, [new(5, 1000m, "Deposit", "TRANSFER FROM CHECKING 1111", "TRANSFER-A")]);
        Assert.AreEqual(1, (await LinksAsync()).Count(link => link.LinkType == TransactionLinkTypes.BankTransfer));
        Assert.AreEqual(0m, await ExpensesAsync());
        MoneyInOutSummary household = await _reports.GetMoneyInOutSummaryAsync(new ExpenseReportFilter());
        Assert.AreEqual(0m, household.TotalMoneyIn);
        Assert.AreEqual(0m, household.TotalMoneyOut);
        Assert.AreEqual(1000m, (await _reports.GetMoneyInOutSummaryAsync(new ExpenseReportFilter { AccountId = checking.AccountId })).TotalMoneyOut);
        Assert.AreEqual(1000m, (await _reports.GetMoneyInOutSummaryAsync(new ExpenseReportFilter { AccountId = savings.AccountId })).TotalMoneyIn);

        AccountReconciliationRepository reconciliation = new(_connections!);
        AccountReconciliation checkedStatement = await reconciliation.CheckAsync(new AccountReconciliationRequest
        {
            AccountId = checking.AccountId, FromDate = new(2026, 4, 1), ToDate = new(2026, 4, 30), OpeningBalance = 1500m, ClosingBalance = 500m
        }, record: true);
        AccountReconciliation savedStatement = await reconciliation.CheckAsync(new AccountReconciliationRequest
        {
            AccountId = savings.AccountId, FromDate = new(2026, 4, 1), ToDate = new(2026, 4, 30), OpeningBalance = 200m, ClosingBalance = 1200m
        }, record: true);
        Assert.IsTrue(checkedStatement.IsBalanced);
        Assert.IsTrue(savedStatement.IsBalanced);
        await _imports.ReapplyRulesAsync();
        Assert.AreEqual(0m, await ExpensesAsync());
        Assert.HasCount(1, await LinksAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task PayPalSmartConnectPurchasesAndLaterRepaymentCountEachPurchaseOnce()
    {
        Account wallet = await AccountAsync("PayPal Wallet", "PayPal Wallet", "3333");
        Account card = await AccountAsync("Smart Connect", "PayPal Smart Connect", "4444");
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        await ImportAsync(wallet, [new(1, -40m, "Purchase", "MERCHANT ALPHA", "W1", "Smart Connect 4444"), new(10, -60m, "Purchase", "MERCHANT BETA", "W2", "Smart Connect 4444")]);
        Activity[] cardRows = [new(1, -40m, "Purchase", "MERCHANT ALPHA", "C1"), new(10, -60m, "Purchase", "MERCHANT BETA", "C2"), new(25, 100m, "Payment", "PAYMENT FROM CHECKING 1111", "P1")];
        await ImportAsync(card, cardRows, ImportProfileCodes.PayPalSmartConnectCsv);
        await ImportAsync(checking, [new(25, -100m, "Withdrawal", "SMART CONNECT PAYMENT 4444", "B1")]);
        Assert.HasCount(6, await RowsAsync());
        Assert.AreEqual(2, (await LinksAsync()).Count(link => link.LinkType == TransactionLinkTypes.PurchaseMirror));
        Assert.AreEqual(1, (await LinksAsync()).Count(link => link.LinkType == TransactionLinkTypes.CreditCardPaymentFunding));
        Assert.AreEqual(100m, await ExpensesAsync());
        Assert.AreEqual(100m, (await _reports.GetMoneyInOutSummaryAsync(new ExpenseReportFilter())).TotalMoneyOut);
        ImportSummary repeated = await ImportAsync(card, cardRows, ImportProfileCodes.PayPalSmartConnectCsv);
        Assert.AreEqual(0, repeated.InsertedRows);
        Assert.HasCount(6, await RowsAsync());

        TransactionLinkRecord mirror = (await LinksAsync()).Single(link => link.LinkType == TransactionLinkTypes.PurchaseMirror && link.SourceAmount == -40m);
        await _links.DeleteAsync(mirror.TransactionLinkId);
        Assert.AreEqual(140m, await ExpensesAsync());
        await _imports.RepairDataFlowAsync();
        Assert.AreEqual(140m, await ExpensesAsync());
        await _links.SaveAsync(new TransactionLinkEditModel { SourceTransactionId = mirror.SourceTransactionId, LinkedTransactionId = mirror.LinkedTransactionId, LinkType = TransactionLinkTypes.PurchaseMirror });
        Assert.AreEqual(100m, await ExpensesAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task AggregatePurchaseAllocationsPreserveResidualsAndReverseOnUnlink()
    {
        Account wallet = await AccountAsync("PayPal Wallet", "PayPal Wallet", "3333");
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        await ImportAsync(wallet, [new(1, -40m, "Purchase", "MERCHANT ALPHA", "W1", "Checking 1111"), new(10, -60m, "Purchase", "MERCHANT BETA", "W2", "Checking 1111")]);
        await ImportAsync(checking, [new(30, -110m, "Withdrawal", "PAYPAL COMBINED FUNDING", "B1")]);
        IReadOnlyList<ImportedTransaction> rows = await RowsAsync();
        long alpha = rows.Single(row => row.ExternalTransactionId == "W1").ImportedTransactionId;
        long beta = rows.Single(row => row.ExternalTransactionId == "W2").ImportedTransactionId;
        long debit = rows.Single(row => row.ExternalTransactionId == "B1").ImportedTransactionId;
        long firstLink = await _links.SaveAsync(new TransactionLinkEditModel { SourceTransactionId = alpha, LinkedTransactionId = debit, LinkType = TransactionLinkTypes.PurchaseFundingAllocation, AllocationAmount = 40m });
        await _links.SaveAsync(new TransactionLinkEditModel { SourceTransactionId = beta, LinkedTransactionId = debit, LinkType = TransactionLinkTypes.PurchaseFundingAllocation, AllocationAmount = 60m });
        Assert.AreEqual(110m, await ExpensesAsync());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _links.SaveAsync(new TransactionLinkEditModel { SourceTransactionId = alpha, LinkedTransactionId = debit, LinkType = TransactionLinkTypes.PurchaseFundingAllocation, AllocationAmount = 1m }));
        Assert.HasCount(2, await LinksAsync());
        await _links.DeleteAsync(firstLink);
        Assert.AreEqual(150m, await ExpensesAsync());
        await _transactions.DeleteAsync(debit);
        Assert.HasCount(0, await LinksAsync());
        Assert.AreEqual(0, await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.TransactionLinkRejection;"));
        Assert.AreEqual(100m, await ExpensesAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ReapplyPreservesManualDecisionsAndUpdatesLinkedMerchantClassification()
    {
        Account wallet = await AccountAsync("PayPal Wallet", "PayPal Wallet", "3333");
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        long manualId = await _transactions.SaveAsync(new ImportedTransaction
        {
            AccountId = checking.AccountId, TransactionDate = new(2026, 4, 1), Amount = -25m, Type = "Withdrawal",
            Description = "MANUAL TRANSFER", Category = "Transfer", IsTransfer = true, IsManual = true, ClassificationIsManual = true
        });
        await ImportAsync(wallet, [new(10, -40m, "Purchase", "UNIQUE MERCHANT", "W1", "Checking 1111", "Shopping")]);
        await ImportAsync(checking, [new(10, -40m, "Withdrawal", "PAYPAL UNIQUE MERCHANT", "B1")]);
        Assert.HasCount(1, await LinksAsync());
        await _rules.SaveAsync(new FinancialRule
        {
            RuleName = "Merchant groceries", MatchDescription = "UNIQUE MERCHANT", MatchType = "Withdrawal",
            Category = "Groceries", SpendingClass = "Food", IsActive = true
        });
        await _imports.ReapplyRulesAsync();
        ImportedTransaction manual = (await _transactions.GetByIdAsync(manualId))!;
        Assert.IsTrue(manual.IsTransfer);
        Assert.AreEqual("Transfer", manual.Category);
        ImportedTransaction purchase = (await RowsAsync()).Single(row => row.ExternalTransactionId == "W1");
        Assert.AreEqual("Groceries", purchase.Category);
        Assert.HasCount(1, await LinksAsync());
        Assert.AreEqual(40m, await ExpensesAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task AUtilityPaymentCannotBecomeCardFundingFromAmountAlone()
    {
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        Account card = await AccountAsync("Smart Connect", "PayPal Smart Connect", "4444");
        await ImportAsync(card, [new(15, 500m, "Payment", "CARD PAYMENT", "P1")], ImportProfileCodes.PayPalSmartConnectCsv);
        await ImportAsync(checking, [new(15, -500m, "Withdrawal", "UTILITY PAYMENT", "U1")]);
        Assert.HasCount(0, await LinksAsync());
        Assert.AreEqual(500m, await ExpensesAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task NativePayPalActivityCannotProduceAMisleadingWalletBalanceCheck()
    {
        Account wallet = await AccountAsync("PayPal Wallet", "PayPal Wallet", "3333");
        string statement = await FileAsync("ACCOUNT ACTIVITY\n01/02/2026\nPreApproved Payment:\nSAMPLE STORE\nUSD -100.00 0.00 -100.00\nPAYPAL BALANCE ACCOUNT\n");
        ImportSummary imported = await _imports.ImportAsync(wallet.AccountId, ImportProfileCodes.PayPalStatement, statement);
        Assert.AreEqual(1, imported.InsertedRows);
        AccountReconciliationRepository reconciliation = new(_connections!);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => reconciliation.CheckAsync(new AccountReconciliationRequest
        {
            AccountId = wallet.AccountId, FromDate = new(2026, 1, 1), ToDate = new(2026, 1, 31), OpeningBalance = 0m, ClosingBalance = 0m
        }, record: true));
        Assert.AreEqual(0, await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.AccountReconciliation;"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task UpgradeQuarantinesLegacyGuessesWithoutLosingLinkEvidence()
    {
        Account wallet = await AccountAsync("PayPal Wallet", "PayPal Wallet", "3333");
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        await ImportAsync(wallet, [new(1, -50m, "Purchase", "SAMPLE STORE", "W1")]);
        await ImportAsync(checking, [new(1, -50m, "Withdrawal", "UNRELATED UTILITY", "B1")]);
        long source = (await RowsAsync()).Single(row => row.ExternalTransactionId == "W1").ImportedTransactionId;
        long linked = (await RowsAsync()).Single(row => row.ExternalTransactionId == "B1").ImportedTransactionId;
        await using (SqlConnection connection = _connections!.CreateConnection())
        {
            await connection.OpenAsync();
            await using SqlCommand seed = connection.CreateCommand();
            seed.CommandText = """
                INSERT INTO dbo.TransactionLink(SourceTransactionId, LinkedTransactionId, LinkType, ConfidenceScore, Notes)
                VALUES (@Source, @Linked, N'PayPalFunding', 95, N'Matched PayPal PreApproved Payment: SAMPLE STORE'),
                       (@Source, @Linked, N'ManualReview', 100, N'Existing investigation');
                UPDATE dbo.ImportedTransaction
                SET Category = N'Transfer', SpendingClass = N'PayPal Funding', IsTransfer = 1,
                    Notes = N'Auto-linked as PayPal funding transfer.', ClassificationIsManual = 0
                WHERE ImportedTransactionId = @Linked;
                DELETE FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.22.0';
                """;
            seed.Parameters.Add(new SqlParameter("@Source", SqlDbType.BigInt) { Value = source });
            seed.Parameters.Add(new SqlParameter("@Linked", SqlDbType.BigInt) { Value = linked });
            await seed.ExecuteNonQueryAsync();
            string migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sql", "migrations", "025_financial_integrity.sql"));
            SqlScriptRunner runner = new();
            await runner.ExecuteScriptAsync(connection, migration);
            await runner.ExecuteScriptAsync(connection, migration);
        }

        IReadOnlyList<TransactionLinkRecord> links = await LinksAsync();
        Assert.HasCount(2, links);
        Assert.IsTrue(links.All(link => link.LinkType == TransactionLinkTypes.ManualReview));
        TransactionLinkRecord review = links.Single(link => link.Notes?.StartsWith("Review required after upgrade: ", StringComparison.Ordinal) == true);
        Assert.IsTrue(review.Notes!.Contains("PayPalFunding", StringComparison.Ordinal));
        Assert.IsTrue(review.Notes.Contains("Matched PayPal PreApproved Payment: SAMPLE STORE", StringComparison.Ordinal));
        Assert.AreEqual(1, await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.TransactionLinkRejection WHERE LinkType = N'PurchaseMirror';"));
        Assert.IsTrue((await _transactions.GetByIdAsync(linked))!.LegacyLinkTreatment);
        Assert.AreEqual(100m, await ExpensesAsync());
        await _imports.RepairDataFlowAsync();
        Assert.AreEqual(100m, await ExpensesAsync());
        await _links.SaveAsync(new TransactionLinkEditModel
        {
            TransactionLinkId = review.TransactionLinkId, SourceTransactionId = source, LinkedTransactionId = linked,
            LinkType = TransactionLinkTypes.PurchaseMirror, ConfidenceScore = 100, Notes = "Confirmed against both statements."
        });
        Assert.AreEqual(50m, await ExpensesAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task WalletRepaymentBankDebitAndCreditReceiptReconcileInEitherLinkOrder()
    {
        Account wallet = await AccountAsync("PayPal Wallet", "PayPal Wallet", "3333");
        Account card = await AccountAsync("Smart Connect", "PayPal Smart Connect", "4444");
        Account checking = await AccountAsync("Everyday Checking", "Checking", "1111");
        await ImportAsync(wallet, [new(25, -500m, "Buyer Credit Payment", "CREDIT REPAYMENT", "W-PAY")]);
        await ImportAsync(card, [new(25, 500m, "Payment", "PAYMENT RECEIVED", "C-PAY")], ImportProfileCodes.PayPalSmartConnectCsv);
        await ImportAsync(checking, [new(25, -500m, "Withdrawal", "REPAYMENT", "B-PAY")]);
        Assert.HasCount(0, await LinksAsync());
        IReadOnlyList<ImportedTransaction> rows = await RowsAsync();
        long walletId = rows.Single(row => row.ExternalTransactionId == "W-PAY").ImportedTransactionId;
        long cardId = rows.Single(row => row.ExternalTransactionId == "C-PAY").ImportedTransactionId;
        long bankId = rows.Single(row => row.ExternalTransactionId == "B-PAY").ImportedTransactionId;

        foreach (bool mirrorFirst in new[] { true, false })
        {
            foreach (TransactionLinkRecord previous in await LinksAsync()) await _links.DeleteAsync(previous.TransactionLinkId);
            TransactionLinkEditModel mirror = new() { SourceTransactionId = walletId, LinkedTransactionId = bankId, LinkType = TransactionLinkTypes.PurchaseMirror };
            TransactionLinkEditModel funding = new() { SourceTransactionId = cardId, LinkedTransactionId = bankId, LinkType = TransactionLinkTypes.CreditCardPaymentFunding };
            await _links.SaveAsync(mirrorFirst ? mirror : funding);
            await _links.SaveAsync(mirrorFirst ? funding : mirror);
            await _imports.ReapplyRulesAsync();
            Assert.HasCount(2, await LinksAsync());
            Assert.HasCount(3, await RowsAsync());
            var expenses = await _reports.GetExpenseReportAsync(new ExpenseReportFilter());
            var cash = await _reports.GetCashFlowReportAsync(new ExpenseReportFilter());
            Assert.AreEqual(0m, expenses.Metrics.TotalExpenses);
            Assert.AreEqual(500m, cash.Summary.TotalMoneyOut);
            Assert.AreEqual(0m, cash.Summary.TotalMoneyIn);
            Assert.IsEmpty(expenses.Warnings);
            Assert.IsEmpty(cash.Warnings);
        }
    }

    private async Task<Account> AccountAsync(string name, string type, string lastFour)
    {
        Account account = new() { AccountNickname = name, Institution = "Example Institution", AccountType = type, AccountNumber = "00000000" + lastFour, IsActive = true };
        account.AccountId = await _accounts.SaveAsync(account);
        return account;
    }

    private async Task<ImportSummary> ImportAsync(Account account, IReadOnlyList<Activity> rows, string profile = ImportProfileCodes.AccountActivityCsv)
    {
        string header = "Date,Amount,Type,Description,Account,Transaction ID,Funding Source,Category\n";
        string body = string.Join("\n", rows.Select(row => string.Join(",", new[]
        {
            new DateOnly(2026, 4, row.Day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.Amount.ToString("0.00", CultureInfo.InvariantCulture), row.Type, row.Description, account.AccountNickname,
            row.Id, row.Funding ?? string.Empty, row.Category ?? string.Empty
        }.Select(CsvCell))));
        return await _imports.ImportAsync(account.AccountId, profile, await FileAsync(header + body + "\n"));
    }

    private async Task<string> FileAsync(string content)
    {
        string file = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".csv");
        await File.WriteAllTextAsync(file, content);
        return file;
    }

    private Task<IReadOnlyList<ImportedTransaction>> RowsAsync() => _transactions.GetAsync(new TransactionFilter { MaxRows = 10000 });
    private Task<IReadOnlyList<TransactionLinkRecord>> LinksAsync() => _links.GetLinksAsync(new TransactionLinkFilter { MaxRows = 10000 });
    private async Task<decimal> ExpensesAsync() => (await _reports.GetExpenseMetricSummaryAsync(new ExpenseReportFilter())).TotalExpenses;

    private async Task<T> ScalarAsync<T>(string query)
    {
        await using SqlConnection connection = _connections!.CreateConnection();
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = query;
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static string CsvCell(string text) => "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    private sealed record Activity(int Day, decimal Amount, string Type, string Description, string Id, string? Funding = null, string? Category = null);
}
