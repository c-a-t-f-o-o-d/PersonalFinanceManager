using PersonalFinanceManager.Application.Services.Contracts;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Reporting;

namespace PersonalFinanceManager.UiTests
{
    internal sealed class PreviewAnalysisService : IAnalysisService
    {
        private readonly FinancialReportCalculator _calculator;
        private readonly Account _account = new()
        {
            AccountId = 1,
            AccountNickname = "Household checking",
            AccountType = "Checking",
            Institution = "Example Financial"
        };

        public PreviewAnalysisService()
        {
            List<ReportTransaction> transactions = [];
            DateOnly month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

            for (int offset = 0; offset < 6; offset++)
            {
                foreach ((string category, string group, decimal amount) in new[]
                {
                    ("Mortgage", "Housing", 1800M),
                    ("Groceries", "Food", 520M + 12M * offset),
                    ("Utilities", "Housing", 210M + 7M * offset),
                    ("Transportation", "Transport", 145M + 9M * offset)
                })
                {
                    transactions.Add(new ReportTransaction
                    {
                        Id = transactions.Count + 1,
                        AccountId = _account.AccountId,
                        AccountNickname = _account.AccountNickname,
                        AccountType = _account.AccountType,
                        Date = month.AddMonths(-offset),
                        Amount = -amount,
                        Description = "Example " + category,
                        Category = category,
                        SpendingClass = group
                    });
                }
            }

            _calculator = new FinancialReportCalculator(new ReportData { Transactions = transactions });
        }

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Account>>([_account]);
        }

        public Task<ExpenseReportResult> GetExpenseReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.Expenses(filter));
        }

        public Task<CashFlowReportResult> GetCashFlowReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.CashFlow(filter));
        }

        public Task<ExpenseMetricSummary> GetExpenseMetricSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.Expenses(filter).Metrics);
        }

        public Task<IReadOnlyList<ExpenseSummaryItem>> GetExpenseSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.Expenses(filter).Summary);
        }

        public Task<IReadOnlyList<MonthlyExpenseTotalItem>> GetMonthlyExpenseTotalsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.Expenses(filter).Monthly);
        }

        public Task<IReadOnlyList<ExpenseDetailItem>> GetExpenseDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.Expenses(filter).Details);
        }

        public Task<MoneyInOutSummary> GetMoneyInOutSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.CashFlow(filter).Summary);
        }

        public Task<IReadOnlyList<MoneyInOutMonthlyItem>> GetMoneyInOutMonthlyAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.CashFlow(filter).Monthly);
        }

        public Task<IReadOnlyList<MoneyInOutDetailItem>> GetMoneyInOutDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.CashFlow(filter).Details);
        }

        public Task<IReadOnlyList<SalarySpendingScopeOption>> GetSalarySpendingScopeOptionsAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.SpendingOptions(filter));
        }

        public Task<SalarySpendingAnalysis> GetSalarySpendingAnalysisAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.SalarySpending(filter));
        }

        public Task<IReadOnlyList<IncomeOwnerSummary>> GetIncomeOwnerSummariesAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_calculator.Income(filter));
        }
    }
}
