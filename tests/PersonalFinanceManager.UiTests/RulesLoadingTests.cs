using System.Reflection;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.App.UI.Views;
using PersonalFinanceManager.Application.Services.Contracts;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;
using static PersonalFinanceManager.UiTests.UiTestSupport;

namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class RulesLoadingTests
    {
        [TestMethod]
        public void DisposingRulesPageCancelsReadsAndIgnoresLateResults()
        {
            RunOnUiThread(() =>
            {
                DelayedRuleService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 720) };
                RulesView view = new(service) { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                DataGridView grid = Descendants(view).OfType<DataGridView>().Single();
                PumpUntil(() => grid.RowCount == 1, "Initial rules did not load.");
                BindingSource binding = (BindingSource)grid.DataSource!;
                object? initialData = binding.DataSource;
                TaskCompletionSource<IReadOnlyList<FinancialRule>> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
                service.RulesResponses.Enqueue(response.Task);
                Task load = InvokePageOperation(view, "LoadRulesAsync");
                CancellationToken token = service.LastReadToken;
                Assert.IsTrue(token.CanBeCanceled);
                view.Dispose();
                Assert.IsTrue(token.IsCancellationRequested);
                response.SetResult([Rule("Late result")]);
                PumpUntil(() => load.IsCompleted, "The abandoned rules load did not complete.");
                load.GetAwaiter().GetResult();
                Assert.AreSame(initialData, binding.DataSource, "A disposed page must not bind a late result.");
                Assert.IsTrue(view.IsDisposed);
                Assert.IsFalse(view.IsHandleCreated, "A late result must not recreate the page handle.");
                host.Close();
            });
        }

        [TestMethod]
        public void NewerRulesLoadWinsWhenAnOlderRequestFinishesLast()
        {
            RunOnUiThread(() =>
            {
                DelayedRuleService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 720) };
                RulesView view = new(service) { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                DataGridView grid = Descendants(view).OfType<DataGridView>().Single();
                PumpUntil(() => grid.RowCount == 1, "Initial rules did not load.");
                TaskCompletionSource<IReadOnlyList<FinancialRule>> older = new(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource<IReadOnlyList<FinancialRule>> newer = new(TaskCreationOptions.RunContinuationsAsynchronously);
                service.RulesResponses.Enqueue(older.Task);
                service.RulesResponses.Enqueue(newer.Task);
                Task olderLoad = InvokePageOperation(view, "LoadRulesAsync");
                CancellationToken olderToken = service.LastReadToken;
                Task newerLoad = InvokePageOperation(view, "LoadRulesAsync");
                Assert.IsTrue(olderToken.IsCancellationRequested);
                newer.SetResult([Rule("Current result")]);
                PumpUntil(() => newerLoad.IsCompleted, "The current rules load did not complete.");
                newerLoad.GetAwaiter().GetResult();
                Assert.AreEqual("Current result", ((FinancialRule)grid.Rows[0].DataBoundItem!).RuleName);
                older.SetResult([Rule("Outdated result")]);
                PumpUntil(() => olderLoad.IsCompleted, "The superseded rules load did not complete.");
                olderLoad.GetAwaiter().GetResult();
                Assert.AreEqual("Current result", ((FinancialRule)grid.Rows[0].DataBoundItem!).RuleName);
                host.Close();
            });
        }

        [TestMethod]
        public void ConfirmedReapplicationFinishesAfterNavigationWithoutCancellingTheWrite()
        {
            RunOnUiThread(() =>
            {
                DelayedRuleService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 720) };
                RulesView view = new(service) { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                PumpUntil(() => service.ReadCalls == 1, "Initial rules did not load.");
                Task operation = InvokePageOperation(view, "ReapplyConfirmedRulesAsync");
                Assert.AreEqual(1, service.ReapplyCalls);
                Assert.IsFalse(service.ReapplyToken.CanBeCanceled, "Navigation must not cancel confirmed financial updates.");
                Assert.IsFalse(Descendants(view).OfType<Button>().Single(button => button.Text == "Reapply Rules to Imports").Enabled);
                InvokePageOperation(view, "ReapplyConfirmedRulesAsync").GetAwaiter().GetResult();
                Assert.AreEqual(1, service.ReapplyCalls, "Repeated activation must not start another reapplication.");
                view.Dispose();
                service.ReapplyCompletion.SetResult(42);
                PumpUntil(() => operation.IsCompleted, "The committed reapplication did not complete after navigation.");
                operation.GetAwaiter().GetResult();
                Assert.IsTrue(service.ReapplicationCommitted);
                Assert.IsFalse(service.ReapplyToken.IsCancellationRequested);
                Assert.AreEqual(1, service.ReadCalls);
                Assert.IsFalse(view.IsHandleCreated, "Completion must not recreate a disposed dialog owner.");
                host.Close();
            });
        }

        [TestMethod]
        public void ConfirmedWorkbookReplacementDoesNotReloadADisposedPage()
        {
            RunOnUiThread(() =>
            {
                DelayedRuleService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 720) };
                RulesView view = new(service) { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                PumpUntil(() => service.ReadCalls == 1, "Initial rules did not load.");
                IReadOnlyList<RuleImportRow> rows = [new RuleImportRow { RuleName = "Example grocery", Description = "EXAMPLE GROCERY" }];
                Task operation = InvokePageOperation(view, "ImportConfirmedRulesAsync", rows);
                Assert.AreEqual(1, service.ReplaceCalls);
                Assert.IsFalse(service.ReplaceToken.CanBeCanceled);
                InvokePageOperation(view, "ImportConfirmedRulesAsync", rows).GetAwaiter().GetResult();
                Assert.AreEqual(1, service.ReplaceCalls, "Repeated activation must not replace the workbook twice.");
                view.Dispose();
                service.ReplaceCompletion.SetResult(true);
                PumpUntil(() => operation.IsCompleted, "The committed replacement did not complete after navigation.");
                operation.GetAwaiter().GetResult();
                Assert.IsTrue(service.ReplacementCommitted);
                Assert.IsFalse(service.ReplaceToken.IsCancellationRequested);
                Assert.AreEqual(1, service.ReadCalls, "A completed write must not start a reload on a disposed page.");
                Assert.IsFalse(view.IsHandleCreated);
                host.Close();
            });
        }

        [TestMethod]
        public void RealWriteFailureRemainsObservableAfterNavigation()
        {
            RunOnUiThread(() =>
            {
                DelayedRuleService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 720) };
                RulesView view = new(service) { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                Task operation = InvokePageOperation(view, "ReapplyConfirmedRulesAsync");
                view.Dispose();
                InvalidOperationException expected = new("The database rejected the update.");
                service.ReapplyCompletion.SetException(expected);
                PumpUntil(() => operation.IsCompleted, "The failed update did not finish.");
                InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => operation.GetAwaiter().GetResult());
                Assert.AreSame(expected, actual);
                Assert.IsFalse(service.ReapplicationCommitted);
                Assert.IsFalse(view.IsHandleCreated);
                host.Close();
            });
        }

        [TestMethod]
        public void ReadFailurePreservesLoadedRulesAndRemainsObservable()
        {
            RunOnUiThread(() =>
            {
                DelayedRuleService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 720) };
                RulesView view = new(service) { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                DataGridView grid = Descendants(view).OfType<DataGridView>().Single();
                PumpUntil(() => grid.RowCount == 1, "Initial rules did not load.");
                TaskCompletionSource<IReadOnlyList<FinancialRule>> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
                service.RulesResponses.Enqueue(response.Task);
                Task load = InvokePageOperation(view, "LoadRulesAsync");
                InvalidOperationException expected = new("The database could not read the rules.");
                response.SetException(expected);
                PumpUntil(() => load.IsCompleted, "The failed read did not finish.");
                InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => load.GetAwaiter().GetResult());
                Assert.AreSame(expected, actual);
                Assert.AreEqual("Initial rule", ((FinancialRule)grid.Rows[0].DataBoundItem!).RuleName);
                Label status = Descendants(view).OfType<Label>().Single(label => label.Name == "PageLoadStatus");
                Assert.IsTrue(status.Visible);
                StringAssert.Contains(status.Text, "could not be loaded");
                host.Close();
            });
        }

        private static Task InvokePageOperation(RulesView view, string name, params object?[] arguments)
        {
            MethodInfo method = typeof(RulesView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Rules operation '{name}' was not found.");
            return (Task)(method.Invoke(view, arguments) ?? throw new InvalidOperationException($"Rules operation '{name}' returned no task."));
        }

        private static FinancialRule Rule(string name)
        {
            return new FinancialRule
            {
                RuleId = 1,
                RuleName = name,
                Priority = 100,
                MatchDescription = "EXAMPLE GROCERY",
                Category = "Groceries",
                SpendingClass = "Food & Drink",
                IsActive = true
            };
        }

        private sealed class DelayedRuleService : IRuleWorkflowService
        {
            public Queue<Task<IReadOnlyList<FinancialRule>>> RulesResponses { get; } = new();
            public TaskCompletionSource<int> ReapplyCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> ReplaceCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public CancellationToken LastReadToken { get; private set; }
            public CancellationToken ReapplyToken { get; private set; }
            public CancellationToken ReplaceToken { get; private set; }
            public int ReadCalls { get; private set; }
            public int ReapplyCalls { get; private set; }
            public int ReplaceCalls { get; private set; }
            public bool ReapplicationCommitted { get; private set; }
            public bool ReplacementCommitted { get; private set; }

            public Task<IReadOnlyList<FinancialRule>> GetRulesAsync(CancellationToken cancellationToken = default)
            {
                ReadCalls++;
                LastReadToken = cancellationToken;
                return RulesResponses.TryDequeue(out Task<IReadOnlyList<FinancialRule>>? response)
                    ? response
                    : Task.FromResult<IReadOnlyList<FinancialRule>>([Rule("Initial rule")]);
            }

            public Task<IReadOnlyList<string>> GetCategoryValuesAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<string>>(["Groceries"]);
            }

            public Task<IReadOnlyList<string>> GetCategoryTypeValuesAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<string>>(["Food & Drink"]);
            }

            public Task<int> SaveRuleAsync(FinancialRule rule, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(rule.RuleId);
            }

            public Task DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public RuleImportResult ParseWorkbook(string workbookPath)
            {
                return new RuleImportResult();
            }

            public async Task ReplaceRulesWithWorkbookRowsAsync(IEnumerable<RuleImportRow> rows, CancellationToken cancellationToken = default)
            {
                ReplaceCalls++;
                ReplaceToken = cancellationToken;
                await ReplaceCompletion.Task.ConfigureAwait(false);
                ReplacementCommitted = true;
            }

            public async Task<int> ReapplyRulesAsync(CancellationToken cancellationToken = default)
            {
                ReapplyCalls++;
                ReapplyToken = cancellationToken;
                int count = await ReapplyCompletion.Task.ConfigureAwait(false);
                ReapplicationCommitted = true;
                return count;
            }
        }
    }
}
