using System.Collections.Concurrent;
using System.Globalization;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.App.UI.Views;
using PersonalFinanceManager.Application.Services.Contracts;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Reporting;
using static PersonalFinanceManager.UiTests.UiTestSupport;
using WinFormsApplication = System.Windows.Forms.Application;

namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class ReportLoadingTests
    {
        [TestMethod]
        [DataRow("expenses")]
        [DataRow("cash-flow")]
        public void ReportsLoadDirectlyWhileAccountOptionsAreStillPending(string reportName)
        {
            RunReportTest(() =>
            {
                ControlledAnalysisService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 680) };
                Control view = CreateReport(reportName, service);
                host.Controls.Add(new PageHost(view));
                host.Show();
                PumpUntil(() => service.ReportCount(reportName) == 1, "The report must start without opening Transactions or waiting for account options.");
                Assert.IsFalse(service.Accounts.Task.IsCompleted);
                ExpenseReportFilter filter = service.ReportFilter(reportName, 0);
                Assert.AreEqual(new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11), filter.FromDate);
                Assert.AreEqual(DateOnly.FromDateTime(DateTime.Today), filter.ToDate);
                Assert.IsNull(filter.AccountId);

                service.CompleteReport(reportName, 0, 173.42M);
                PumpUntil(() => HasMetric(view, 173.42M), "Report totals should become visible while account options are pending.");
                Label status = Descendants(view).OfType<Label>().Single(label => label.Name == "PageLoadStatus");
                Assert.IsTrue(status.Visible);
                StringAssert.Contains(status.Text, "account options");

                service.Accounts.SetResult([CreateAccount()]);
                PumpUntil(() => !status.Visible, "The loading indicator should clear after both independent loads finish.");
                Assert.AreEqual(1, service.ReportCount(reportName), "Binding account options must not restart the report.");
                Assert.IsTrue(HasMetric(view, 173.42M));
                host.Close();
            });
        }

        [TestMethod]
        [DataRow("expenses")]
        [DataRow("cash-flow")]
        public void OlderReportCompletionCannotOverwriteTheNewestDateRange(string reportName)
        {
            RunReportTest(() =>
            {
                ControlledAnalysisService service = new();
                service.Accounts.SetResult([CreateAccount()]);
                using ThemedForm host = new() { ClientSize = new Size(1100, 680) };
                Control view = CreateReport(reportName, service);
                host.Controls.Add(new PageHost(view));
                host.Show();
                PumpUntil(() => service.ReportCount(reportName) == 1, "The initial report should start automatically.");
                Descendants(view).OfType<Button>().Single(button => button.Text == "All").PerformClick();
                PumpUntil(() => service.ReportCount(reportName) == 2, "The new date range should start its own report request.");
                Assert.IsTrue(service.ReportToken(reportName, 0).IsCancellationRequested);
                Assert.IsNull(service.ReportFilter(reportName, 1).FromDate);
                Assert.IsNull(service.ReportFilter(reportName, 1).ToDate);

                service.CompleteReport(reportName, 1, 287.65M);
                PumpUntil(() => HasMetric(view, 287.65M), "The newest report should be displayed first.");
                service.CompleteReport(reportName, 0, 173.42M);
                DrainCallbacks(host);
                Assert.IsTrue(HasMetric(view, 287.65M), "A service that ignores cancellation must still be unable to replace newer totals.");
                Assert.IsFalse(HasMetric(view, 173.42M));
                host.Close();
            });
        }

        [TestMethod]
        [DataRow("expenses")]
        [DataRow("cash-flow")]
        public void ReportAndAccountCompletionsAfterNavigationDoNotTouchDisposedControls(string reportName)
        {
            RunReportTest(() =>
            {
                ControlledAnalysisService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 680) };
                Control view = CreateReport(reportName, service);
                PageHost page = new(view);
                host.Controls.Add(page);
                host.Show();
                PumpUntil(() => service.ReportCount(reportName) == 1, "The initial report should start automatically.");
                Label[] labels = Descendants(view).OfType<Label>().ToArray();
                page.Dispose();
                string[] disposedText = labels.Select(label => label.Text).ToArray();
                Assert.IsTrue(service.ReportToken(reportName, 0).IsCancellationRequested);
                Assert.IsTrue(service.AccountTokens.Single().IsCancellationRequested);

                service.Accounts.SetResult([CreateAccount()]);
                service.CompleteReport(reportName, 0, 173.42M);
                DrainCallbacks(host);
                CollectionAssert.AreEqual(disposedText, labels.Select(label => label.Text).ToArray(), "Detached pages must ignore late data, including services that ignore cancellation.");
                host.Close();
            });
        }

        [TestMethod]
        public void SalaryStartsOneInitialCalculationAndDoesNotWaitForAccountOptions()
        {
            RunReportTest(() =>
            {
                ControlledAnalysisService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 680) };
                SalaryAnalysisView view = new(service);
                host.Controls.Add(new PageHost(view));
                host.Show();
                PumpUntil(() => service.Scopes.Count == 1, "Salary must start its own spending scope request.");
                Assert.IsFalse(service.Accounts.Task.IsCompleted);
                PendingRequest<SalarySpendingFilter, IReadOnlyList<SalarySpendingScopeOption>> scope = service.Scopes.Single();
                Assert.AreEqual(new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11), scope.Filter.FromDate);
                Assert.AreEqual(DateOnly.FromDateTime(DateTime.Today), scope.Filter.ToDate);
                scope.Response.SetResult([CreateScope("Current bucket")]);
                PumpUntil(() => service.Salaries.Count == 1, "Salary analysis must run without waiting for account options.");
                service.Salaries.Single().Response.SetResult(CreateSalaryResult("Current analysis"));
                PumpUntil(() => HasReviewNote(view, "Current analysis"), "The delayed salary response should update its own page.");

                service.Accounts.SetResult([CreateAccount()]);
                DrainCallbacks(host);
                Assert.AreEqual(1, service.Scopes.Count, "Initial account binding must not start a competing all-history scope request.");
                Assert.AreEqual(1, service.Salaries.Count);
                Assert.IsTrue(Descendants(view).OfType<CheckBox>().Any(checkBox => checkBox.Text.StartsWith("Current bucket /", StringComparison.Ordinal)));
                host.Close();
            });
        }

        [TestMethod]
        public void NewSalaryRangeSupersedesBothPendingAnalysisAndOldScopeResults()
        {
            RunReportTest(() =>
            {
                ControlledAnalysisService service = new();
                service.Accounts.SetResult([CreateAccount()]);
                using ThemedForm host = new() { ClientSize = new Size(1100, 680) };
                SalaryAnalysisView view = new(service);
                host.Controls.Add(new PageHost(view));
                host.Show();
                PumpUntil(() => service.Scopes.Count == 1, "The initial scope should load automatically.");
                service.Scopes.Single().Response.SetResult([CreateScope("Old bucket")]);
                PumpUntil(() => service.Salaries.Count == 1, "The initial salary analysis should start after its scope is available.");
                PendingRequest<SalarySpendingFilter, SalarySpendingAnalysis> older = service.Salaries.Single();

                Descendants(view).OfType<Button>().Single(button => button.Text == "All").PerformClick();
                PumpUntil(() => service.Scopes.Count == 2, "Changing the date range should supersede the whole calculation.");
                Assert.IsTrue(older.Token.IsCancellationRequested);
                service.Scopes.ToArray()[1].Response.SetResult([CreateScope("Newest bucket")]);
                PumpUntil(() => service.Salaries.Count == 2, "The new calculation should use the new spending scope.");
                service.Salaries.ToArray()[1].Response.SetResult(CreateSalaryResult("Newest analysis"));
                PumpUntil(() => HasReviewNote(view, "Newest analysis"), "The new salary result should be visible.");

                older.Response.SetResult(CreateSalaryResult("Obsolete analysis"));
                DrainCallbacks(host);
                Assert.IsTrue(HasReviewNote(view, "Newest analysis"));
                Assert.IsFalse(HasReviewNote(view, "Obsolete analysis"));
                Assert.IsTrue(Descendants(view).OfType<CheckBox>().Any(checkBox => checkBox.Text.StartsWith("Newest bucket /", StringComparison.Ordinal)));
                Assert.IsFalse(Descendants(view).OfType<CheckBox>().Any(checkBox => checkBox.Text.StartsWith("Old bucket /", StringComparison.Ordinal)));
                host.Close();
            });
        }

        [TestMethod]
        public void SupersededSalaryScopeCannotStartAnObsoleteCalculation()
        {
            RunReportTest(() =>
            {
                ControlledAnalysisService service = new();
                service.Accounts.SetResult([CreateAccount()]);
                using ThemedForm host = new() { ClientSize = new Size(1100, 680) };
                SalaryAnalysisView view = new(service);
                host.Controls.Add(new PageHost(view));
                host.Show();
                PumpUntil(() => service.Scopes.Count == 1, "The initial spending scope should load automatically.");
                PendingRequest<SalarySpendingFilter, IReadOnlyList<SalarySpendingScopeOption>> older = service.Scopes.Single();
                Descendants(view).OfType<Button>().Single(button => button.Text == "All").PerformClick();
                PumpUntil(() => service.Scopes.Count == 2, "The new range should request a new spending scope.");
                Assert.IsTrue(older.Token.IsCancellationRequested);
                service.Scopes.ToArray()[1].Response.SetResult([CreateScope("Newest bucket")]);
                PumpUntil(() => service.Salaries.Count == 1, "Only the new scope should start a calculation.");
                service.Salaries.Single().Response.SetResult(CreateSalaryResult("Newest analysis"));
                PumpUntil(() => HasReviewNote(view, "Newest analysis"), "The current calculation should complete.");

                older.Response.SetResult([CreateScope("Obsolete bucket")]);
                DrainCallbacks(host);
                Assert.AreEqual(1, service.Salaries.Count, "A superseded scope must not continue into another salary request.");
                Assert.IsTrue(HasReviewNote(view, "Newest analysis"));
                Assert.IsFalse(Descendants(view).OfType<CheckBox>().Any(checkBox => checkBox.Text.StartsWith("Obsolete bucket /", StringComparison.Ordinal)));
                host.Close();
            });
        }

        [TestMethod]
        public void SalaryNavigationCancelsTheRemainingCalculationStages()
        {
            RunReportTest(() =>
            {
                ControlledAnalysisService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 680) };
                SalaryAnalysisView view = new(service);
                PageHost page = new(view);
                host.Controls.Add(page);
                host.Show();
                PumpUntil(() => service.Scopes.Count == 1, "The initial scope should start automatically.");
                PendingRequest<SalarySpendingFilter, IReadOnlyList<SalarySpendingScopeOption>> scope = service.Scopes.Single();
                page.Dispose();
                Assert.IsTrue(scope.Token.IsCancellationRequested);
                Assert.IsTrue(service.AccountTokens.Single().IsCancellationRequested);
                service.Accounts.SetResult([CreateAccount()]);
                scope.Response.SetResult([CreateScope("Late bucket")]);
                DrainCallbacks(host);
                Assert.AreEqual(0, service.Salaries.Count, "A disposed page must not start another calculation stage after its scope finishes.");
                host.Close();
            });
        }

        [TestMethod]
        public void RunwayStartsBothRequiredQueriesAndDisplaysDelayedResults()
        {
            RunReportTest(() =>
            {
                ControlledAnalysisService service = new();
                using ThemedForm host = new() { ClientSize = new Size(1100, 680) };
                RunwayView view = new(service);
                host.Controls.Add(new PageHost(view));
                host.Show();
                PumpUntil(() => service.AccountTokens.Count == 1 && service.BillTokens.Count == 1, "Both runway data queries should start before either completes.");
                Assert.IsFalse(service.Accounts.Task.IsCompleted);
                Assert.IsFalse(service.Bills.Task.IsCompleted);
                service.Bills.SetResult([]);
                service.Accounts.SetResult([CreateAccount()]);
                PumpUntil(() => HasMetric(view, 10000M), "Runway should display balances without visiting Transactions.");
                Assert.IsFalse(Descendants(view).OfType<Label>().Single(label => label.Name == "PageLoadStatus").Visible);
                host.Close();
            });
        }

        private static void RunReportTest(Action action)
        {
            RunOnUiThread(() =>
            {
                List<Exception> errors = [];
                ThreadExceptionEventHandler handler = (_, args) => errors.Add(args.Exception);
                WinFormsApplication.ThreadException += handler;

                try
                {
                    action();
                    Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
                }
                finally
                {
                    WinFormsApplication.ThreadException -= handler;
                }
            });
        }

        private static void DrainCallbacks(Control host)
        {
            bool drained = false;
            host.BeginInvoke(new Action(() => drained = true));
            PumpUntil(() => drained, "Queued UI callbacks should complete.");
            WinFormsApplication.DoEvents();
        }

        private static Control CreateReport(string reportName, IAnalysisService service)
        {
            return reportName == "expenses" ? new ReportsView(service) : new MoneyInOutView(service);
        }

        private static bool HasMetric(Control view, decimal value)
        {
            string text = value.ToString("C2", CultureInfo.CurrentCulture);
            return Descendants(view).OfType<Label>().Any(label => label.Text == text);
        }

        private static bool HasReviewNote(Control view, string note)
        {
            return Descendants(view).OfType<TextBox>().Any(textBox => textBox.Lines.Contains(note));
        }

        private static Account CreateAccount()
        {
            return new Account
            {
                AccountId = 1,
                AccountNickname = "Example checking",
                AccountType = "Checking",
                Institution = "Example Financial",
                AvailableBalance = 10000M,
                BalanceAsOfDate = DateOnly.FromDateTime(DateTime.Today)
            };
        }

        private static SalarySpendingScopeOption CreateScope(string category)
        {
            return new SalarySpendingScopeOption { Category = category, SpendingClass = "Household", MonthlyAverage = 100M };
        }

        private static SalarySpendingAnalysis CreateSalaryResult(string note)
        {
            return new SalarySpendingAnalysis { CanEstimate = false, Warnings = [note] };
        }

        private sealed class PendingRequest<TFilter, TResult>(TFilter filter, CancellationToken token)
        {
            public TFilter Filter { get; } = filter;
            public CancellationToken Token { get; } = token;
            public TaskCompletionSource<TResult> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private sealed class ControlledAnalysisService : IAnalysisService, IRunwayWorkflowService
        {
            private readonly PreviewAnalysisService _preview = new();
            private readonly ConcurrentQueue<PendingRequest<ExpenseReportFilter, ExpenseReportResult>> _expenses = new();
            private readonly ConcurrentQueue<PendingRequest<ExpenseReportFilter, CashFlowReportResult>> _cashFlow = new();
            public TaskCompletionSource<IReadOnlyList<Account>> Accounts { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<IReadOnlyList<RecurringBill>> Bills { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public ConcurrentQueue<CancellationToken> AccountTokens { get; } = new();
            public ConcurrentQueue<CancellationToken> BillTokens { get; } = new();
            public ConcurrentQueue<PendingRequest<SalarySpendingFilter, IReadOnlyList<SalarySpendingScopeOption>>> Scopes { get; } = new();
            public ConcurrentQueue<PendingRequest<SalarySpendingFilter, SalarySpendingAnalysis>> Salaries { get; } = new();

            public int ReportCount(string reportName)
            {
                return reportName == "expenses" ? _expenses.Count : _cashFlow.Count;
            }
            public ExpenseReportFilter ReportFilter(string reportName, int index)
            {
                return reportName == "expenses" ? _expenses.ToArray()[index].Filter : _cashFlow.ToArray()[index].Filter;
            }
            public CancellationToken ReportToken(string reportName, int index)
            {
                return reportName == "expenses" ? _expenses.ToArray()[index].Token : _cashFlow.ToArray()[index].Token;
            }

            public void CompleteReport(string reportName, int index, decimal amount)
            {
                FinancialReportCalculator calculator = new(new ReportData
                {
                    Transactions = [new ReportTransaction
                    {
                        Id = 1,
                        AccountId = 1,
                        AccountNickname = "Example checking",
                        AccountType = "Checking",
                        Date = DateOnly.FromDateTime(DateTime.Today),
                        Amount = -amount,
                        Description = "Example purchase",
                        Category = "Groceries",
                        SpendingClass = "Food"
                    }]
                });

                if (reportName == "expenses")
                {
                    PendingRequest<ExpenseReportFilter, ExpenseReportResult> request = _expenses.ToArray()[index];
                    request.Response.SetResult(calculator.Expenses(request.Filter));
                }
                else
                {
                    PendingRequest<ExpenseReportFilter, CashFlowReportResult> request = _cashFlow.ToArray()[index];
                    request.Response.SetResult(calculator.CashFlow(request.Filter));
                }
            }

            public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
            {
                AccountTokens.Enqueue(cancellationToken);
                return Accounts.Task;
            }

            public Task<IReadOnlyList<RecurringBill>> GetBillsAsync(CancellationToken cancellationToken = default)
            {
                BillTokens.Enqueue(cancellationToken);
                return Bills.Task;
            }

            public Task<ExpenseReportResult> GetExpenseReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            {
                PendingRequest<ExpenseReportFilter, ExpenseReportResult> request = new(filter, cancellationToken);
                _expenses.Enqueue(request);
                return request.Response.Task;
            }

            public Task<CashFlowReportResult> GetCashFlowReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            {
                PendingRequest<ExpenseReportFilter, CashFlowReportResult> request = new(filter, cancellationToken);
                _cashFlow.Enqueue(request);
                return request.Response.Task;
            }

            public Task<IReadOnlyList<SalarySpendingScopeOption>> GetSalarySpendingScopeOptionsAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
            {
                PendingRequest<SalarySpendingFilter, IReadOnlyList<SalarySpendingScopeOption>> request = new(filter, cancellationToken);
                Scopes.Enqueue(request);
                return request.Response.Task;
            }

            public Task<SalarySpendingAnalysis> GetSalarySpendingAnalysisAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
            {
                PendingRequest<SalarySpendingFilter, SalarySpendingAnalysis> request = new(filter, cancellationToken);
                Salaries.Enqueue(request);
                return request.Response.Task;
            }

            public Task<ExpenseMetricSummary> GetExpenseMetricSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            {
                return _preview.GetExpenseMetricSummaryAsync(filter, cancellationToken);
            }
            public Task<IReadOnlyList<ExpenseSummaryItem>> GetExpenseSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            {
                return _preview.GetExpenseSummaryAsync(filter, cancellationToken);
            }
            public Task<IReadOnlyList<MonthlyExpenseTotalItem>> GetMonthlyExpenseTotalsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            {
                return _preview.GetMonthlyExpenseTotalsAsync(filter, cancellationToken);
            }
            public Task<IReadOnlyList<ExpenseDetailItem>> GetExpenseDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            {
                return _preview.GetExpenseDetailsAsync(filter, cancellationToken);
            }
            public Task<MoneyInOutSummary> GetMoneyInOutSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            {
                return _preview.GetMoneyInOutSummaryAsync(filter, cancellationToken);
            }
            public Task<IReadOnlyList<MoneyInOutMonthlyItem>> GetMoneyInOutMonthlyAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            {
                return _preview.GetMoneyInOutMonthlyAsync(filter, cancellationToken);
            }
            public Task<IReadOnlyList<MoneyInOutDetailItem>> GetMoneyInOutDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            {
                return _preview.GetMoneyInOutDetailsAsync(filter, cancellationToken);
            }
            public Task<IReadOnlyList<IncomeOwnerSummary>> GetIncomeOwnerSummariesAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
            {
                return _preview.GetIncomeOwnerSummariesAsync(filter, cancellationToken);
            }
        }
    }
}
