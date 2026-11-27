using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Reporting;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class AnalysisService : IAnalysisService
    {
        private readonly AccountRepository _accountRepository;
        private readonly ReportRepository _reportRepository;

        public AnalysisService(AccountRepository accountRepository, ReportRepository reportRepository)
        {
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
        }

        public Task<ExpenseReportResult> GetExpenseReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            => _reportRepository.GetExpenseReportAsync(filter, cancellationToken);

        public Task<CashFlowReportResult> GetCashFlowReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
            => _reportRepository.GetCashFlowReportAsync(filter, cancellationToken);

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return _accountRepository.GetAllAsync(cancellationToken);
        }

        public Task<ExpenseMetricSummary> GetExpenseMetricSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetExpenseMetricSummaryAsync(filter, cancellationToken);
        }

        public Task<IReadOnlyList<ExpenseSummaryItem>> GetExpenseSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetExpenseSummaryAsync(filter, cancellationToken);
        }

        public Task<IReadOnlyList<MonthlyExpenseTotalItem>> GetMonthlyExpenseTotalsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetMonthlyExpenseTotalsAsync(filter, cancellationToken);
        }

        public Task<IReadOnlyList<ExpenseDetailItem>> GetExpenseDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetExpenseDetailsAsync(filter, cancellationToken);
        }

        public Task<MoneyInOutSummary> GetMoneyInOutSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetMoneyInOutSummaryAsync(filter, cancellationToken);
        }

        public Task<IReadOnlyList<MoneyInOutMonthlyItem>> GetMoneyInOutMonthlyAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetMoneyInOutMonthlyAsync(filter, cancellationToken);
        }

        public Task<IReadOnlyList<MoneyInOutDetailItem>> GetMoneyInOutDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetMoneyInOutDetailsAsync(filter, cancellationToken);
        }

        public Task<IReadOnlyList<SalarySpendingScopeOption>> GetSalarySpendingScopeOptionsAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetSalarySpendingScopeOptionsAsync(filter, cancellationToken);
        }

        public Task<SalarySpendingAnalysis> GetSalarySpendingAnalysisAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetSalarySpendingAnalysisAsync(filter, cancellationToken);
        }

        public Task<IReadOnlyList<IncomeOwnerSummary>> GetIncomeOwnerSummariesAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
        {
            return _reportRepository.GetIncomeOwnerSummariesAsync(filter, cancellationToken);
        }
    }
}
