using System.Data;
using Microsoft.Data.SqlClient;
using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;
using PersonalFinanceManager.Data.Database;
using PersonalFinanceManager.Data.Logging;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Data.Security;
using PersonalFinanceManager.Data.Services;
using PersonalFinanceManager.Data.Sql;

namespace PersonalFinanceManager.Tests.Integration;

[TestClass]
[DoNotParallelize]
public sealed class StarterCategorizationSqlTests
{
    private const string _databasePrefix = "PFM_Starters_";
    private string _databaseName = string.Empty;
    private string _directory = string.Empty;
    private SqlConnectionFactory? _connections;
    private DatabaseInitializer _initializer = null!;
    private RuleRepository _rules = null!;
    private TransactionRepository _transactions = null!;

    [TestInitialize]
    public async Task CreateIsolatedDatabase()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("PFM_RUN_SQL_TESTS") != "1")
        {
            Assert.Inconclusive("SQL integration tests require Windows LocalDB and PFM_RUN_SQL_TESTS=1.");
        }

        _databaseName = _databasePrefix + Guid.NewGuid().ToString("N");
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
        DatabaseInitializationResult initialization = await _initializer.InitializeAsync();
        Assert.IsTrue(initialization.ExecutedCount > 0, "Migration files must be copied to the test output directory.");
        _rules = new RuleRepository(_connections);
        _transactions = new TransactionRepository(_connections);
    }

    [TestCleanup]
    public async Task DropIsolatedDatabase()
    {
        if (_connections is not null && _databaseName.StartsWith(_databasePrefix, StringComparison.Ordinal) && Guid.TryParseExact(_databaseName[_databasePrefix.Length..], "N", out _))
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

        if (!string.IsNullOrEmpty(_directory) && Directory.Exists(_directory) && string.Equals(Path.GetFileName(_directory), _databaseName, StringComparison.Ordinal))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task FreshDatabaseProvidesConservativeStartersAndStillSupportsDemoData()
    {
        IReadOnlyList<string> categories = await _transactions.GetCategoryValuesAsync();
        IReadOnlyList<string> groups = await _transactions.GetCategoryTypeValuesAsync();
        Assert.HasCount(28, categories);
        Assert.HasCount(12, groups);
        CollectionAssert.Contains(categories.ToArray(), "Groceries");
        CollectionAssert.Contains(groups.ToArray(), "Food & Drink");
        Assert.IsEmpty(await _transactions.GetAsync(new TransactionFilter()));
        IReadOnlyList<FinancialRule> rules = await _rules.GetAllAsync();
        Assert.HasCount(14, rules);
        Assert.HasCount(4, rules.Where(rule => rule.IsActive).ToArray());
        Assert.IsTrue(rules.All(rule => rule.StarterRuleCode is not null && rule.Priority == 1000000));
        Assert.IsTrue(rules.All(rule => rule.IsIncome is null && rule.IsTransfer is null && rule.IncomeOwner is null && rule.Merchant is null));
        RuleMatcher matcher = new();
        ParsedTransaction fee = new() { Amount = -10m, Type = "Withdrawal", Description = "MONTHLY MAINTENANCE FEE" };
        Assert.AreEqual("Bank Fees", matcher.Match(rules, fee)?.Category);
        Assert.IsNull(matcher.Match(rules, new ParsedTransaction { Amount = 10m, Type = "Refund", Description = fee.Description }));
        Assert.IsNull(matcher.Match(rules, new ParsedTransaction { Amount = -2000m, Type = "Withdrawal", Description = "MORTGAGE PAYMENT" }));
        Assert.IsNull(matcher.Match(rules, new ParsedTransaction { Amount = -1000m, Type = "Withdrawal", Description = "WIRE TRANSFER FEE PRINCIPAL" }));
        FinancialRule custom = new() { RuleName = "Custom fee", Priority = 100, MatchDescription = fee.Description, Category = "Account Charges" };
        Assert.AreSame(custom, matcher.Match(rules.Append(custom), fee));
        DemoDataService demo = new(_connections!);
        Assert.IsFalse((await demo.GetSummaryAsync()).HasUserData);
        DemoDataLoadResult loaded = await demo.LoadDemoDataAsync();
        Assert.AreEqual(6, loaded.RulesCreated);
        Assert.IsTrue(loaded.TransactionsCreated > 0);
        Assert.HasCount(20, await _rules.GetAllAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ReinitializationPreservesEditedAndDeletedStartersAndCustomChoices()
    {
        IReadOnlyList<FinancialRule> rules = await _rules.GetAllAsync();
        FinancialRule edited = rules.Single(rule => rule.StarterRuleCode == "atm-fee");
        edited.Category = "Cash Access Fees";
        edited.SpendingClass = "My Reporting Group";
        edited.Priority = 25;
        await _rules.SaveAsync(edited);
        int deletedId = rules.Single(rule => rule.StarterRuleCode == "rent").RuleId;
        await _rules.DeleteAsync(deletedId);
        await ExecuteAsync("""
            UPDATE dbo.AppLookup SET DisplayName = N'Vehicle Fuel' WHERE LookupGroup = N'Category' AND LookupCode = N'fuel';
            UPDATE dbo.AppLookup SET IsActive = 0 WHERE LookupGroup = N'Category' AND LookupCode = N'clothing';
            """);
        await RunStarterMigrationAsync();
        DatabaseInitializationResult repeated = await _initializer.InitializeAsync();
        Assert.AreEqual(0, repeated.ExecutedCount);
        IReadOnlyList<FinancialRule> after = await _rules.GetAllAsync();
        Assert.HasCount(13, after);
        Assert.IsFalse(after.Any(rule => rule.RuleId == deletedId || rule.StarterRuleCode == "rent"));
        FinancialRule saved = after.Single(rule => rule.RuleId == edited.RuleId);
        Assert.AreEqual("Cash Access Fees", saved.Category);
        Assert.AreEqual(25, saved.Priority);
        Assert.IsNull(saved.StarterRuleCode);
        Assert.IsNull(saved.IsIncome);
        Assert.IsNull(saved.IsTransfer);
        IReadOnlyList<string> categories = await _transactions.GetCategoryValuesAsync();
        CollectionAssert.Contains(categories.ToArray(), "Cash Access Fees");
        CollectionAssert.Contains(categories.ToArray(), "Vehicle Fuel");
        CollectionAssert.DoesNotContain(categories.ToArray(), "Clothing");
        CollectionAssert.Contains((await _transactions.GetCategoryTypeValuesAsync()).ToArray(), "My Reporting Group");
        DemoDataService demo = new(_connections!);
        Assert.IsTrue((await demo.GetSummaryAsync()).HasUserData);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => demo.LoadDemoDataAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task StarterMigrationPreservesExistingRulesAndLookupChoices()
    {
        await ExecuteAsync("""
            DELETE FROM dbo.FinancialRule WHERE StarterRuleCode IS NOT NULL;
            DELETE FROM dbo.AppLookup WHERE LookupGroup IN (N'Category', N'SpendingClass');
            DELETE FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.23.0';
            INSERT INTO dbo.AppLookup (LookupGroup, LookupCode, DisplayName, SortOrder, IsActive)
            VALUES (N'Category', N'groceries', N'Household Groceries', 911, 0);
            """);
        int customId = await _rules.SaveAsync(new FinancialRule
        {
            RuleName = "Existing account fees",
            Priority = 15,
            MatchDescription = "MONTHLY MAINTENANCE FEE",
            MatchType = "Withdrawal",
            Category = "Account Charges",
            SpendingClass = "Custom Fees",
            IsIncome = false,
            IsTransfer = false,
            IsActive = true
        });
        await RunStarterMigrationAsync();
        IReadOnlyList<FinancialRule> after = await _rules.GetAllAsync();
        FinancialRule existing = after.Single(rule => rule.RuleId == customId);
        Assert.AreEqual("Account Charges", existing.Category);
        Assert.AreEqual("Custom Fees", existing.SpendingClass);
        Assert.AreEqual(15, existing.Priority);
        Assert.IsTrue(existing.IsActive);
        Assert.IsFalse(existing.IsIncome!.Value);
        Assert.IsFalse(existing.IsTransfer!.Value);
        Assert.HasCount(13, after.Where(rule => rule.StarterRuleCode is not null).ToArray());
        Assert.IsFalse(after.Any(rule => rule.StarterRuleCode is not null && rule.IsActive));
        Assert.AreEqual(1, await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.AppLookup WHERE LookupGroup = N'Category' AND LookupCode = N'groceries' AND DisplayName = N'Household Groceries' AND SortOrder = 911 AND IsActive = 0;"));
        Assert.AreEqual(1, await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.23.0';"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CategoryOnlyRulesPreserveTransferAndUnclassifiedCreditTreatment()
    {
        IReadOnlyList<FinancialRule> rules = await _rules.GetAllAsync();
        FinancialRule transfer = rules.Single(rule => rule.StarterRuleCode == "internal-transfer");
        transfer.IsActive = true;
        await _rules.SaveAsync(transfer);
        FinancialRule payroll = rules.Single(rule => rule.StarterRuleCode == "salary-wages");
        payroll.IsActive = true;
        await _rules.SaveAsync(payroll);
        AccountRepository accounts = new(_connections!, new AccountDataProtector());
        int accountId = await accounts.SaveAsync(new Account
        {
            AccountNickname = "Example Checking",
            Institution = "Example Bank",
            AccountType = "Checking"
        });
        string path = Path.Combine(_directory, "activity.csv");
        await File.WriteAllTextAsync(path, """
            Date,Amount,Type,Description
            2026-04-01,-10.00,Withdrawal,MONTHLY MAINTENANCE FEE
            2026-04-02,10.00,Refund,MONTHLY MAINTENANCE FEE
            2026-04-03,-500.00,Transfer,INTERNAL TRANSFER
            2026-04-04,900.00,Unclassified Credit,PAYROLL DEPOSIT
            """);
        ImportService imports = new(_connections!, _rules);
        ImportSummary summary = await imports.ImportAsync(accountId, ImportProfileCodes.AccountActivityCsv, path);
        Assert.AreEqual(0, summary.ErrorRows);
        Assert.AreEqual(4, summary.InsertedRows);
        IReadOnlyList<ImportedTransaction> rows = await _transactions.GetAsync(new TransactionFilter());
        ImportedTransaction fee = rows.Single(row => row.Amount == -10m);
        Assert.AreEqual("Bank Fees", fee.Category);
        Assert.IsFalse(fee.IsIncome);
        Assert.IsFalse(fee.IsTransfer);
        ImportedTransaction refund = rows.Single(row => row.Amount == 10m);
        Assert.IsNull(refund.AppliedRuleId);
        Assert.IsFalse(refund.IsIncome);
        ImportedTransaction moved = rows.Single(row => row.Amount == -500m);
        Assert.AreEqual("Internal Transfer", moved.Category);
        Assert.IsTrue(moved.IsTransfer);
        Assert.IsFalse(moved.IsIncome);
        ImportedTransaction credit = rows.Single(row => row.Amount == 900m);
        Assert.AreEqual("Salary & Wages", credit.Category);
        Assert.IsFalse(credit.IsIncome, "A category label must not turn an unreviewed credit into income.");
        Assert.IsFalse(credit.IsTransfer);
        TransactionWorkflowService workflow = new(accounts, _rules, _transactions);
        int affected = await workflow.CreateRuleAndApplyAsync(new FinancialRule
        {
            RuleName = "Inactive fee template",
            MatchDescription = fee.Description,
            Priority = 1,
            Category = "Inactive Override",
            IsIncome = true,
            IsTransfer = true,
            IsActive = false
        });
        Assert.AreEqual(0, affected, "Saving an inactive rule must not apply it to existing transactions.");
        ImportedTransaction unchangedFee = (await _transactions.GetAsync(new TransactionFilter())).Single(row => row.Amount == -10m);
        Assert.AreEqual("Bank Fees", unchangedFee.Category);
        Assert.IsFalse(unchangedFee.IsIncome);
        Assert.IsFalse(unchangedFee.IsTransfer);
        Assert.IsTrue((await _rules.GetAllAsync()).Any(rule => rule.RuleName == "Inactive fee template" && !rule.IsActive));
        await imports.ReapplyRulesAsync();
        rows = await _transactions.GetAsync(new TransactionFilter());
        Assert.IsTrue(rows.Single(row => row.Amount == -500m).IsTransfer);
        Assert.IsFalse(rows.Single(row => row.Amount == 900m).IsIncome);
        var report = await new ReportRepository(_connections!).GetExpenseReportAsync(new ExpenseReportFilter());
        Assert.IsTrue(report.Warnings.Any(warning => warning.Contains("require classification", StringComparison.Ordinal)));
    }

    private async Task RunStarterMigrationAsync()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sql", "migrations", "026_starter_categories_and_rules.sql");
        string script = await File.ReadAllTextAsync(path);
        await using SqlConnection connection = _connections!.CreateConnection();
        await connection.OpenAsync();
        await new SqlScriptRunner().ExecuteScriptAsync(connection, script);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using SqlConnection connection = _connections!.CreateConnection();
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using SqlConnection connection = _connections!.CreateConnection();
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
