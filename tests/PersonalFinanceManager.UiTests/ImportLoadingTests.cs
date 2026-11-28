using System.Reflection;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.App.UI.Views;
using PersonalFinanceManager.Application.Services.Contracts;
using PersonalFinanceManager.Core.Models;
using static PersonalFinanceManager.UiTests.UiTestSupport;

namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class ImportLoadingTests
    {
        [TestMethod]
        public void PendingPreviewBlocksImportAndRepairWithoutDisablingNavigation()
        {
            RunOnUiThread(() =>
            {
                string path = CreateStatementFile();

                try
                {
                    DelayedImportService service = new();
                    using ThemedForm host = new() { ClientSize = new Size(1100, 720) };
                    ImportsView view = ShowImports(host, service, path);
                    Task preview = InvokeImportAction(view, "PreviewAsync");
                    Assert.AreEqual(1, service.PreviewCalls);
                    Assert.IsTrue(service.PreviewToken.CanBeCanceled);
                    Assert.IsTrue(host.Enabled);
                    Assert.IsTrue(view.Enabled, "The page should disable operation inputs, not its navigation container.");
                    Assert.IsTrue(Descendants(view).OfType<TabControl>().Single().Enabled);
                    Assert.IsTrue(Descendants(view).OfType<ComboBox>().All(input => !input.Enabled));
                    Assert.IsTrue(Descendants(view).OfType<Button>().All(button => !button.Enabled));
                    Assert.IsFalse(Descendants(view).OfType<TextBox>().Single(input => !input.Multiline).Enabled);
                    InvokeImportAction(view, "ImportAsync").GetAwaiter().GetResult();
                    InvokeImportAction(view, "RepairDataFlowAsync").GetAwaiter().GetResult();
                    Assert.AreEqual(0, service.ImportCalls, "Import must wait for the active preview.");
                    Assert.AreEqual(0, service.RepairCalls, "Reconciliation must not race the preview summary.");
                    service.PreviewCompletion.SetResult(new ImportSummary
                    {
                        ParsedRows = 1,
                        PreviewRows = [new ImportedTransaction { Description = "EXAMPLE GROCERY", Amount = -12M }]
                    });
                    PumpUntil(() => preview.IsCompleted, "The preview did not complete.");
                    preview.GetAwaiter().GetResult();
                    Assert.AreEqual(1, Descendants(view).OfType<DataGridView>().Single().RowCount);
                    Assert.IsTrue(Descendants(view).OfType<ComboBox>().All(input => input.Enabled));
                    Assert.IsTrue(Descendants(view).OfType<Button>().All(button => button.Enabled));
                    Assert.IsTrue(Descendants(view).OfType<TextBox>().Single(input => !input.Multiline).Enabled);
                    host.Close();
                }
                finally
                {
                    File.Delete(path);
                }
            });
        }

        [TestMethod]
        public void ConfirmedImportCompletesAfterPageDisposalWithoutUpdatingControls()
        {
            RunOnUiThread(() =>
            {
                string path = CreateStatementFile();

                try
                {
                    DelayedImportService service = new();
                    using ThemedForm host = new() { ClientSize = new Size(1100, 720) };
                    ImportsView view = ShowImports(host, service, path);
                    DataGridView grid = Descendants(view).OfType<DataGridView>().Single();
                    BindingSource binding = (BindingSource)grid.DataSource!;
                    object? initialData = binding.DataSource;
                    Task import = InvokeImportAction(view, "ImportAsync");
                    Assert.AreEqual(1, service.ImportCalls);
                    Assert.IsFalse(service.ImportToken.CanBeCanceled, "Navigation must not cancel the confirmed import.");
                    InvokeImportAction(view, "PreviewAsync").GetAwaiter().GetResult();
                    InvokeImportAction(view, "RepairDataFlowAsync").GetAwaiter().GetResult();
                    Assert.AreEqual(0, service.PreviewCalls);
                    Assert.AreEqual(0, service.RepairCalls);
                    view.Dispose();
                    service.ImportCompletion.SetResult(new ImportSummary { ImportBatchId = 7, ParsedRows = 1, InsertedRows = 1 });
                    PumpUntil(() => import.IsCompleted, "The committed import did not complete after navigation.");
                    import.GetAwaiter().GetResult();
                    Assert.IsTrue(service.ImportCommitted);
                    Assert.IsFalse(service.ImportToken.IsCancellationRequested);
                    Assert.AreSame(initialData, binding.DataSource, "A completed import must not update a disposed page.");
                    Assert.IsFalse(view.IsHandleCreated);
                    host.Close();
                }
                finally
                {
                    File.Delete(path);
                }
            });
        }

        [TestMethod]
        public void ImportFailureRemainsObservableAfterNavigation()
        {
            RunOnUiThread(() =>
            {
                string path = CreateStatementFile();

                try
                {
                    DelayedImportService service = new();
                    using ThemedForm host = new() { ClientSize = new Size(1100, 720) };
                    ImportsView view = ShowImports(host, service, path);
                    Task import = InvokeImportAction(view, "ImportAsync");
                    view.Dispose();
                    InvalidOperationException expected = new("The database rejected the import.");
                    service.ImportCompletion.SetException(expected);
                    PumpUntil(() => import.IsCompleted, "The failed import did not complete.");
                    InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => import.GetAwaiter().GetResult());
                    Assert.AreSame(expected, actual);
                    Assert.IsFalse(service.ImportCommitted);
                    Assert.IsFalse(view.IsHandleCreated);
                    host.Close();
                }
                finally
                {
                    File.Delete(path);
                }
            });
        }

        private static ImportsView ShowImports(Form host, DelayedImportService service, string path)
        {
            ImportsView view = new(service) { Dock = DockStyle.Fill };
            host.Controls.Add(view);
            host.Show();
            PumpUntil(() => Descendants(view).OfType<ComboBox>().All(input => input.SelectedIndex >= 0), "Import accounts and profiles did not load.");
            Descendants(view).OfType<TextBox>().Single(input => !input.Multiline).Text = path;
            return view;
        }

        private static string CreateStatementFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "PFM_UiImport_" + Guid.NewGuid().ToString("N") + ".csv");
            File.WriteAllText(path, "Date,Amount,Description\n2026-01-02,-12.00,EXAMPLE GROCERY\n");
            return path;
        }

        private static Task InvokeImportAction(ImportsView view, string name)
        {
            MethodInfo actionMethod = typeof(ImportsView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Import action '{name}' was not found.");
            Func<Task> action = (Func<Task>)actionMethod.CreateDelegate(typeof(Func<Task>), view);
            MethodInfo runner = typeof(ImportsView).GetMethod("RunImportActionAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The import action runner was not found.");
            return (Task)(runner.Invoke(view, [action]) ?? throw new InvalidOperationException("The import action returned no task."));
        }

        private sealed class DelayedImportService : IImportWorkflowService
        {
            public TaskCompletionSource<ImportSummary> PreviewCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<ImportSummary> ImportCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public CancellationToken PreviewToken { get; private set; }
            public CancellationToken ImportToken { get; private set; }
            public int PreviewCalls { get; private set; }
            public int ImportCalls { get; private set; }
            public int RepairCalls { get; private set; }
            public bool ImportCommitted { get; private set; }

            public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Account>>([new Account
                {
                    AccountId = 1,
                    AccountNickname = "Example checking",
                    Institution = "Example Financial",
                    AccountType = "Checking"
                }]);
            }

            public IReadOnlyList<ImportProfileOption> GetProfiles()
            {
                return [new ImportProfileOption("ExampleCsv", "Example CSV", "CSV files (*.csv)|*.csv")];
            }

            public Task<ImportSummary> PreviewAsync(int accountId, string profileCode, string path, CancellationToken cancellationToken = default)
            {
                PreviewCalls++;
                PreviewToken = cancellationToken;
                return PreviewCompletion.Task;
            }

            public async Task<ImportSummary> ImportAsync(int accountId, string profileCode, string path, CancellationToken cancellationToken = default)
            {
                ImportCalls++;
                ImportToken = cancellationToken;
                ImportSummary result = await ImportCompletion.Task.ConfigureAwait(false);
                ImportCommitted = true;
                return result;
            }

            public Task<DataFlowRepairSummary> RepairDataFlowAsync(CancellationToken cancellationToken = default)
            {
                RepairCalls++;
                return Task.FromResult(new DataFlowRepairSummary());
            }
        }
    }
}
