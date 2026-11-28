using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Reporting;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IAnalysisService
    {
        Task<ExpenseReportResult> GetExpenseReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default);

        Task<CashFlowReportResult> GetCashFlowReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        Task<ExpenseMetricSummary> GetExpenseMetricSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ExpenseSummaryItem>> GetExpenseSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MonthlyExpenseTotalItem>> GetMonthlyExpenseTotalsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ExpenseDetailItem>> GetExpenseDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default);

        Task<MoneyInOutSummary> GetMoneyInOutSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MoneyInOutMonthlyItem>> GetMoneyInOutMonthlyAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MoneyInOutDetailItem>> GetMoneyInOutDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SalarySpendingScopeOption>> GetSalarySpendingScopeOptionsAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default);

        Task<SalarySpendingAnalysis> GetSalarySpendingAnalysisAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IncomeOwnerSummary>> GetIncomeOwnerSummariesAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default);
    }
}
