using System.Reflection;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.App.UI.Views;
using PersonalFinanceManager.Application.Services.Contracts;
using PersonalFinanceManager.Core.Models;
using static PersonalFinanceManager.UiTests.UiTestSupport;

namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class TransactionsLoadingTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void InitialRowsLoadWithoutApplyWhileLookupsAreStillPending(bool dark)
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(dark);
                DelayedTransactionService service = new();
                TransactionsView view = new(service);
                using ThemedForm host = CreateHost(view);
                host.Show();
                PumpUntil(() => service.Rows.Count == 1, "The initial transaction query must start without pressing Apply or waiting for lookup queries.");
                Assert.AreEqual(1, service.Accounts.Count);
                Assert.AreEqual(1, service.Categories.Count);
                Assert.AreEqual(1, service.CategoryTypes.Count);
                Assert.IsFalse(service.Accounts[0].Task.IsCompleted);
                Assert.IsFalse(service.Categories[0].Task.IsCompleted);
                Assert.IsFalse(service.CategoryTypes[0].Task.IsCompleted);

                TransactionFilter filter = service.Rows[0].Filter;
                Assert.IsNull(filter.AccountId);
                Assert.IsNull(filter.FromDate);
                Assert.IsNull(filter.ToDate);
                Assert.IsNull(filter.Category);
                Assert.IsNull(filter.SpendingClass);
                Assert.AreEqual("All Accounts", GetField<ComboBox>(view, "_accountFilter").Text);

                DataGridView grid = GetField<DataGridView>(view, "_grid");
                service.Rows[0].Read.Complete([Transaction(1, "Initial purchase")]);
                PumpUntil(() => CurrentTransactionId(grid) == 1, "Rows must bind while account and category lookup tasks remain incomplete.");
                Label status = LoadStatus(view);
                Assert.IsTrue(status.Visible, "Pending lookup work must remain visible after transaction rows arrive.");
                Rectangle gridBounds = grid.RectangleToScreen(grid.ClientRectangle);
                Rectangle statusBounds = status.RectangleToScreen(status.ClientRectangle);
                Assert.IsFalse(gridBounds.IntersectsWith(statusBounds), "Loading feedback must not cover transaction rows or their scrollbar.");
                Assert.IsFalse(view.VerticalScroll.Visible);
                Assert.IsFalse(((PageHost)view.Parent!).VerticalScroll.Visible);
                Capture(host, "transactions-loading-" + (dark ? "dark" : "light"), TestContext);

                ComboBox category = GetField<ComboBox>(view, "_category");
                category.Text = "User-entered category";
                CompleteLookups(service);
                PumpUntil(() => !LoadStatus(view).Visible, "The page must leave its loading state after all initial requests finish.");
                Assert.AreEqual("User-entered category", category.Text, "Late lookup results must preserve text entered while the grid was loading.");
                Assert.AreEqual(1, service.Rows.Count, "Completing lookups must not issue an unrequested second transaction query.");
                Assert.AreEqual(1L, CurrentTransactionId(grid));
                host.Close();
            });
        }

        [TestMethod]
        public void NewerApplyResultSurvivesAnOlderDelayedQuery()
        {
            RunOnUiThread(() =>
            {
                DelayedTransactionService service = new();
                TransactionsView view = new(service);
                using ThemedForm host = CreateHost(view);
                host.Show();
                FinishInitialLoad(view, service);
                TextBox search = GetField<TextBox>(view, "_search");
                DataGridView grid = GetField<DataGridView>(view, "_grid");
                search.Text = "Older request";
                Task older = InvokeRead(view, "LoadTransactionsAsync");
                Assert.AreEqual(2, service.Rows.Count);
                search.Text = "Newest request";
                Descendants(view).OfType<Button>().Single(button => button.Text == "Apply").PerformClick();
                PumpUntil(() => service.Rows.Count == 3, "Apply must start a new query for the current filter.");
                Assert.IsTrue(service.Rows[1].Read.CancellationToken.IsCancellationRequested, "A superseded read should be cancelled.");
                Assert.AreEqual("Newest request", service.Rows[2].Filter.SearchText);

                service.Rows[2].Read.Complete([Transaction(3, "Newest result")]);
                PumpUntil(() => CurrentTransactionId(grid) == 3, "The most recent Apply result must populate the grid.");
                service.Rows[1].Read.Complete([Transaction(2, "Older result")]);
                PumpUntil(() => older.IsCompleted, "The earlier request must finish even when its provider ignores cancellation.");
                older.GetAwaiter().GetResult();
                Assert.AreEqual(3L, CurrentTransactionId(grid), "A late result must not replace rows for the user's newer filter.");
                Assert.IsFalse(LoadStatus(view).Visible);
                host.Close();
            });
        }

        [TestMethod]
        public void DisposingThePageCancelsPendingReadsAndIgnoresLateResults()
        {
            RunOnUiThread(() =>
            {
                DelayedTransactionService service = new();
                TransactionsView view = new(service);
                using ThemedForm host = CreateHost(view);
                host.Show();
                FinishInitialLoad(view, service);
                BindingSource source = GetField<BindingSource>(view, "_bindingSource");
                Task pending = InvokeRead(view, "LoadTransactionsAsync");
                Assert.AreEqual(2, service.Rows.Count);
                view.Dispose();
                object? boundAfterDisposal = source.DataSource;
                Assert.IsTrue(service.Rows[1].Read.CancellationToken.IsCancellationRequested, "Navigating away must cancel the page's pending database reads.");

                service.Rows[1].Read.Complete([Transaction(2, "Late result")]);
                PumpUntil(() => pending.IsCompleted, "A late provider response must finish without updating disposed controls.");
                pending.GetAwaiter().GetResult();
                Assert.AreSame(boundAfterDisposal, source.DataSource, "A disposed page must not receive late data binding updates.");
                host.Close();
            });
        }

        [TestMethod]
        public void DuplicateAddAttemptsDoNotQueueEditorsWhileAccountsAreLoading()
        {
            RunOnUiThread(() =>
            {
                DelayedTransactionService service = new();
                TransactionsView view = new(service);
                using ThemedForm host = CreateHost(view);
                host.Show();
                PumpUntil(() => service.Rows.Count == 1, "Initial reads must start before the Add action is tested.");
                Task first = InvokeRead(view, "AddAsync");
                Assert.IsFalse(first.IsCompleted, "The first editor request should wait for the existing account lookup.");
                Task duplicate = InvokeRead(view, "AddAsync");
                Assert.IsTrue(duplicate.IsCompletedSuccessfully, "A duplicate Add action must return immediately instead of queuing a second editor.");
                Assert.AreEqual(1, service.Accounts.Count, "Opening an editor must reuse the pending initial account request.");
                Assert.IsTrue(GetField<List<Button>>(view, "_mutationButtons").All(button => !button.Enabled));
                Assert.IsTrue(Descendants(view).OfType<Button>().Single(button => button.Text == "Refresh").Enabled);
                Assert.IsTrue(Descendants(view).OfType<Button>().Single(button => button.Text == "Apply").Enabled);

                view.Dispose();
                CompleteLookups(service);
                service.Rows[0].Read.Complete([]);
                PumpUntil(() => first.IsCompleted, "Navigating away while waiting for account options must abandon the pending editor.");
                first.GetAwaiter().GetResult();
                Assert.IsFalse(System.Windows.Forms.Application.OpenForms.OfType<TransactionEditForm>().Any(), "A late account response must not open an editor for a disposed page.");
                host.Close();
            });
        }

        private static ThemedForm CreateHost(TransactionsView view)
        {
            ThemedForm host = new() { ClientSize = new Size(1200, 700) };
            host.Controls.Add(new PageHost(view));
            return host;
        }

        private static void FinishInitialLoad(TransactionsView view, DelayedTransactionService service)
        {
            PumpUntil(() => service.Rows.Count == 1, "The initial transaction request must start automatically.");
            CompleteLookups(service);
            service.Rows[0].Read.Complete([Transaction(1, "Initial purchase")]);
            DataGridView grid = GetField<DataGridView>(view, "_grid");
            PumpUntil(() => CurrentTransactionId(grid) == 1 && !LoadStatus(view).Visible, "The initial delayed requests must populate the page.");
        }

        private static void CompleteLookups(DelayedTransactionService service)
        {
            service.Accounts[0].Complete([new Account
            {
                AccountId = 1,
                AccountNickname = "Household checking",
                Institution = "Example Financial",
                AccountType = "Checking"
            }]);
            service.Categories[0].Complete(["Groceries", "Utilities"]);
            service.CategoryTypes[0].Complete(["Food & Drink", "Utilities"]);
        }

        private static ImportedTransaction Transaction(long id, string description)
        {
            return new ImportedTransaction
            {
                ImportedTransactionId = id,
                AccountId = 1,
                AccountNickname = "Household checking",
                TransactionDate = new DateOnly(2026, 11, 1),
                Amount = -25M,
                Type = "Withdrawal",
                Description = description,
                Category = "Groceries",
                SpendingClass = "Food & Drink"
            };
        }

        private static long? CurrentTransactionId(DataGridView grid)
        {
            return grid.Rows.Count == 1 && grid.Rows[0].DataBoundItem is ImportedTransaction transaction
                ? transaction.ImportedTransactionId
                : null;
        }

        private static Label LoadStatus(Control view)
        {
            return Descendants(view).OfType<Label>().Single(label => label.Name == "PageLoadStatus");
        }

        private static T GetField<T>(TransactionsView view, string name)
        {
            return (T)typeof(TransactionsView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        }

        private static Task InvokeRead(TransactionsView view, string method)
        {
            return (Task)typeof(TransactionsView).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null)!;
        }

        private sealed class DelayedRead<T>
        {
            private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public DelayedRead(CancellationToken cancellationToken)
            {
                CancellationToken = cancellationToken;
            }

            public CancellationToken CancellationToken { get; }
            public Task<T> Task => _completion.Task;

            public void Complete(T value)
            {
                _completion.SetResult(value);
            }
        }

        private sealed record TransactionRead(TransactionFilter Filter, DelayedRead<IReadOnlyList<ImportedTransaction>> Read);

        private sealed class DelayedTransactionService : ITransactionWorkflowService
        {
            public List<DelayedRead<IReadOnlyList<Account>>> Accounts { get; } = new();
            public List<DelayedRead<IReadOnlyList<string>>> Categories { get; } = new();
            public List<DelayedRead<IReadOnlyList<string>>> CategoryTypes { get; } = new();
            public List<TransactionRead> Rows { get; } = new();

            public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
            {
                return AddRead(Accounts, cancellationToken);
            }

            public Task<IReadOnlyList<string>> GetCategoryValuesAsync(CancellationToken cancellationToken = default)
            {
                return AddRead(Categories, cancellationToken);
            }

            public Task<IReadOnlyList<string>> GetCategoryTypeValuesAsync(CancellationToken cancellationToken = default)
            {
                return AddRead(CategoryTypes, cancellationToken);
            }

            public Task<IReadOnlyList<ImportedTransaction>> GetTransactionsAsync(TransactionFilter filter, CancellationToken cancellationToken = default)
            {
                DelayedRead<IReadOnlyList<ImportedTransaction>> read = new(cancellationToken);
                Rows.Add(new TransactionRead(filter, read));
                return read.Task;
            }

            public Task<ImportedTransaction?> GetTransactionAsync(long importedTransactionId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<long> SaveTransactionAsync(ImportedTransaction transaction, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task DeleteTransactionAsync(long importedTransactionId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> UpdateCategorizationAsync(IEnumerable<long> importedTransactionIds, string? category, string? categoryType, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> CreateRuleAndApplyAsync(FinancialRule rule, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            private static Task<T> AddRead<T>(List<DelayedRead<T>> reads, CancellationToken cancellationToken)
            {
                DelayedRead<T> read = new(cancellationToken);
                reads.Add(read);
                return read.Task;
            }
        }
    }
}
