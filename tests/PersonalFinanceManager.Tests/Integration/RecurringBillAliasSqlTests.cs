using System.Data;
using System.IO.Compression;
using Microsoft.Data.SqlClient;
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
public sealed class RecurringBillAliasSqlTests
{
    private const string _databasePrefix = "PFM_BillAliases_";
    private string _databaseName = string.Empty;
    private string _directory = string.Empty;
    private SqlConnectionFactory? _connections;
    private RecurringBillRepository _bills = null!;
    private AccountRepository _accounts = null!;

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
        DatabaseInitializationResult initialization = await new DatabaseInitializer(_connections, new FileLogger()).InitializeAsync();
        Assert.IsTrue(initialization.ExecutedCount > 0, "Migration files must be copied to the test output directory.");
        Assert.AreEqual(0, initialization.FailedCount);
        _bills = new RecurringBillRepository(_connections);
        _accounts = new AccountRepository(_connections, new AccountDataProtector());
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
    public async Task AliasesRoundTripThroughBillReportsAndPortableExport()
    {
        Account account = await CreateAccountAsync("Checking", "1111");
        RecurringBill bill = Mortgage(account.AccountId);
        bill.RecurringBillId = await _bills.SaveAsync(bill);
        RecurringBill? restored = await _bills.GetByIdAsync(bill.RecurringBillId);
        Assert.IsNotNull(restored);
        CollectionAssert.AreEqual(bill.PayeeAliases.ToArray(), restored.PayeeAliases.ToArray());
        CollectionAssert.AreEqual(bill.SourceDescriptionAliases.ToArray(), restored.SourceDescriptionAliases.ToArray());
        restored.Notes = "Reviewed payment schedule";
        await _bills.SaveAsync(restored);
        Assert.HasCount(1, await _bills.GetBillsAsync());
        CollectionAssert.AreEqual(bill.SourceDescriptionAliases.ToArray(), (await _bills.GetBillsAsync()).Single().SourceDescriptionAliases.ToArray());

        TransactionRepository transactions = new(_connections!);
        await transactions.SaveAsync(Payment(account.AccountId, 1, "OLD SERVICER HOME 101", 1800M));
        await transactions.SaveAsync(Payment(account.AccountId, 2, "NEW SERVICER HOME 101", 1800M));
        await transactions.SaveAsync(Payment(account.AccountId, 1, "OTHER PROPERTY 202", 100M));
        await transactions.SaveAsync(Payment(account.AccountId, 2, "OTHER PROPERTY 202", 100M));
        ReportRepository reports = new(_connections!);
        SalarySpendingAnalysis salary = await reports.GetSalarySpendingAnalysisAsync(new SalarySpendingFilter
        {
            FromDate = new DateOnly(2026, 1, 1),
            ToDate = new DateOnly(2026, 2, 28),
            SpendingMode = SalarySpendingMode.RequiredBillsPlusSelectedCategories,
            SelectedSpendingBuckets = [new SalarySpendingScopeOption { Category = "Mortgage", SpendingClass = "Housing" }]
        });
        Assert.IsTrue(salary.CanEstimate);
        Assert.AreEqual(1800M, salary.RequiredBillsMonthlySpending);
        Assert.AreEqual(100M, salary.TransactionMonthlySpending);

        DatabaseBackupResult export = await new DatabaseBackupService(_connections!).ExportDataZipAsync(_directory);
        Assert.IsTrue(export.Succeeded, export.Message);
        using (ZipArchive archive = ZipFile.OpenRead(export.FilePath))
        using (StreamReader reader = new(archive.GetEntry("recurring_bills.csv")!.Open()))
        {
            string csv = await reader.ReadToEndAsync();
            StringAssert.Contains(csv, "PayeeAliasesJson");
            StringAssert.Contains(csv, "SourceDescriptionAliasesJson");
            StringAssert.Contains(csv, "OLD SERVICER HOME 101");
            StringAssert.Contains(csv, "Old servicer");
        }

        await _bills.DeleteAsync(bill.RecurringBillId);
        Assert.IsNull(await _bills.GetByIdAsync(bill.RecurringBillId));
        SalarySpendingAnalysis afterDelete = await reports.GetSalarySpendingAnalysisAsync(new SalarySpendingFilter { SpendingMode = SalarySpendingMode.RequiredBillsOnly });
        Assert.AreEqual(0M, afterDelete.RequiredBillsMonthlySpending);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConflictingSavesAreRejectedWithoutLosingExistingAliases()
    {
        Account account = await CreateAccountAsync("Checking", "1111");
        Account otherAccount = await CreateAccountAsync("Other checking", "2222");
        RecurringBill first = Mortgage(account.AccountId);
        first.RecurringBillId = await _bills.SaveAsync(first);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _bills.SaveAsync(Mortgage(account.AccountId)));
        RecurringBill oversized = Mortgage(account.AccountId);
        oversized.SourceDescription = new string('x', 1001);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _bills.SaveAsync(oversized));

        RecurringBill separate = Mortgage(account.AccountId);
        separate.BillName = "Other mortgage";
        separate.SourceDescription = "OTHER PROPERTY 202";
        separate.SourceDescriptionAliases = [];
        separate.RecurringBillId = await _bills.SaveAsync(separate);
        separate.SourceDescriptionAliases = ["old servicer home 101"];
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _bills.SaveAsync(separate));
        Assert.IsEmpty((await _bills.GetByIdAsync(separate.RecurringBillId))!.SourceDescriptionAliases);
        CollectionAssert.AreEqual(first.SourceDescriptionAliases.ToArray(), (await _bills.GetByIdAsync(first.RecurringBillId))!.SourceDescriptionAliases.ToArray());

        RecurringBill otherAccountBill = Mortgage(otherAccount.AccountId);
        otherAccountBill.RecurringBillId = await _bills.SaveAsync(otherAccountBill);
        Assert.HasCount(3, await _bills.GetBillsAsync());

        first.IsActive = false;
        await _bills.SaveAsync(first);
        await _bills.SaveAsync(separate);
        CollectionAssert.AreEqual(separate.SourceDescriptionAliases.ToArray(), (await _bills.GetByIdAsync(separate.RecurringBillId))!.SourceDescriptionAliases.ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConcurrentConflictingCreatesKeepOneActiveObligation()
    {
        Account account = await CreateAccountAsync("Checking", "1111");

        async Task<bool> TrySaveAsync()
        {
            try
            {
                await _bills.SaveAsync(Mortgage(account.AccountId));
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        bool[] saved = await Task.WhenAll(TrySaveAsync(), TrySaveAsync());
        Assert.AreEqual(1, saved.Count(value => value));
        Assert.HasCount(1, await _bills.GetBillsAsync());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task StaleEditsCannotRemoveNewerAliasesAndSuccessfulSavesRefreshTheirVersion()
    {
        Account account = await CreateAccountAsync("Checking", "1111");
        RecurringBill bill = Mortgage(account.AccountId);
        int id = await _bills.SaveAsync(bill);
        Assert.AreEqual(id, bill.RecurringBillId);
        Assert.HasCount(8, bill.RowVersion);
        RecurringBill first = (await _bills.GetByIdAsync(id))!;
        RecurringBill stale = (await _bills.GetByIdAsync(id))!;
        byte[] originalVersion = first.RowVersion.ToArray();
        first.SourceDescriptionAliases = ["OLD SERVICER HOME 101", "INTERIM SERVICER HOME 101"];
        await _bills.SaveAsync(first);
        Assert.IsFalse(originalVersion.SequenceEqual(first.RowVersion));
        stale.SourceDescriptionAliases = ["OLD SERVICER HOME 101", "UNREVIEWED SERVICER HOME 101"];
        InvalidOperationException conflict = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _bills.SaveAsync(stale));
        StringAssert.Contains(conflict.Message, "changed or deleted");
        RecurringBill current = (await _bills.GetByIdAsync(id))!;
        CollectionAssert.AreEqual(first.SourceDescriptionAliases.ToArray(), current.SourceDescriptionAliases.ToArray());

        first.SourceDescriptionAliases = ["INTERIM SERVICER HOME 101"];
        byte[] previousVersion = first.RowVersion.ToArray();
        await _bills.SaveAsync(first);
        Assert.IsFalse(previousVersion.SequenceEqual(first.RowVersion));
        current = (await _bills.GetByIdAsync(id))!;
        CollectionAssert.AreEqual(new[] { "INTERIM SERVICER HOME 101" }, current.SourceDescriptionAliases.ToArray());
        CollectionAssert.AreEqual(first.RowVersion, current.RowVersion);
        Assert.AreEqual(1800M, current.TypicalAmount);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task UpgradeRetainsExistingBillsAndRepeatedMigrationPreservesAliases()
    {
        Account account = await CreateAccountAsync("Checking", "1111");
        RecurringBill bill = Mortgage(account.AccountId);
        bill.SourceDescriptionAliases = [];
        bill.PayeeAliases = [];
        bill.Notes = "Custom obligation";
        bill.RecurringBillId = await _bills.SaveAsync(bill);
        await using SqlConnection connection = _connections!.CreateConnection();
        await connection.OpenAsync();
        SqlScriptRunner runner = new();
        await runner.ExecuteScriptAsync(connection, """
            ALTER TABLE dbo.RecurringBill DROP CONSTRAINT CK_RecurringBill_PayeeAliasesJson, CK_RecurringBill_SourceDescriptionAliasesJson,
                DF_RecurringBill_PayeeAliasesJson, DF_RecurringBill_SourceDescriptionAliasesJson;
            ALTER TABLE dbo.RecurringBill DROP COLUMN PayeeAliasesJson, SourceDescriptionAliasesJson, [RowVersion];
            DELETE dbo.SchemaMetadata WHERE SchemaVersion = N'2.24.0';
            """);
        string script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sql", "migrations", "027_recurring_bill_payee_aliases.sql"));
        await runner.ExecuteScriptAsync(connection, script);
        RecurringBill? upgraded = await _bills.GetByIdAsync(bill.RecurringBillId);
        Assert.IsNotNull(upgraded);
        Assert.AreEqual(bill.BillName, upgraded.BillName);
        Assert.AreEqual(bill.Payee, upgraded.Payee);
        Assert.AreEqual(bill.SourceDescription, upgraded.SourceDescription);
        Assert.AreEqual(bill.Notes, upgraded.Notes);
        Assert.IsEmpty(upgraded.SourceDescriptionAliases);
        Assert.IsEmpty(upgraded.PayeeAliases);
        upgraded.SourceDescriptionAliases = ["OLD SERVICER HOME 101"];
        upgraded.PayeeAliases = ["Old servicer"];
        await _bills.SaveAsync(upgraded);
        await runner.ExecuteScriptAsync(connection, script);
        RecurringBill? repeated = await _bills.GetByIdAsync(bill.RecurringBillId);
        Assert.IsNotNull(repeated);
        CollectionAssert.AreEqual(upgraded.PayeeAliases.ToArray(), repeated.PayeeAliases.ToArray());
        CollectionAssert.AreEqual(upgraded.SourceDescriptionAliases.ToArray(), repeated.SourceDescriptionAliases.ToArray());
    }

    private async Task<Account> CreateAccountAsync(string name, string lastFour)
    {
        Account account = new() { AccountNickname = name, Institution = "Example Institution", AccountType = "Checking", AccountNumber = "00000000" + lastFour };
        account.AccountId = await _accounts.SaveAsync(account);
        return account;
    }

    private static RecurringBill Mortgage(int accountId)
    {
        return new RecurringBill
        {
            AccountId = accountId,
            BillName = "Home mortgage",
            Payee = "New servicer",
            PayeeAliases = ["Old servicer"],
            SourceDescription = "NEW SERVICER HOME 101",
            SourceDescriptionAliases = ["OLD SERVICER HOME 101"],
            Category = "Mortgage",
            SpendingClass = "Housing",
            RequirementType = BillRequirementTypes.Debt,
            TypicalAmount = 1800M
        };
    }

    private static ImportedTransaction Payment(int accountId, int month, string description, decimal amount)
    {
        return new ImportedTransaction
        {
            AccountId = accountId,
            TransactionDate = new DateOnly(2026, month, 15),
            Amount = -amount,
            Type = "Withdrawal",
            Description = description,
            Merchant = "New servicer",
            Category = "Mortgage",
            SpendingClass = "Housing",
            IsManual = true,
            ClassificationIsManual = true
        };
    }
}
