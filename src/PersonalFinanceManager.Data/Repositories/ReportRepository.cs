using System.Data;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Reporting;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Repositories;

public sealed class ReportRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlResourceReader _sqlResources;

    public ReportRepository(SqlConnectionFactory connectionFactory, SqlResourceReader? sqlResources = null)
    {
        _connectionFactory = connectionFactory;
        _sqlResources = sqlResources ?? new SqlResourceReader();
    }

    public async Task<ExpenseReportResult> GetExpenseReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetCalculatorAsync(cancellationToken).ConfigureAwait(false)).Expenses(filter);
    }

    public async Task<CashFlowReportResult> GetCashFlowReportAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetCalculatorAsync(cancellationToken).ConfigureAwait(false)).CashFlow(filter);
    }

    public async Task<ExpenseMetricSummary> GetExpenseMetricSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetExpenseReportAsync(filter, cancellationToken).ConfigureAwait(false)).Metrics;
    }

    public async Task<IReadOnlyList<ExpenseSummaryItem>> GetExpenseSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetExpenseReportAsync(filter, cancellationToken).ConfigureAwait(false)).Summary;
    }

    public async Task<IReadOnlyList<MonthlyExpenseTotalItem>> GetMonthlyExpenseTotalsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetExpenseReportAsync(filter, cancellationToken).ConfigureAwait(false)).Monthly;
    }

    public async Task<IReadOnlyList<ExpenseDetailItem>> GetExpenseDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetExpenseReportAsync(filter, cancellationToken).ConfigureAwait(false)).Details;
    }

    public async Task<MoneyInOutSummary> GetMoneyInOutSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetCashFlowReportAsync(filter, cancellationToken).ConfigureAwait(false)).Summary;
    }

    public async Task<IReadOnlyList<MoneyInOutMonthlyItem>> GetMoneyInOutMonthlyAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetCashFlowReportAsync(filter, cancellationToken).ConfigureAwait(false)).Monthly;
    }

    public async Task<IReadOnlyList<MoneyInOutDetailItem>> GetMoneyInOutDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetCashFlowReportAsync(filter, cancellationToken).ConfigureAwait(false)).Details;
    }

    public async Task<IReadOnlyList<SalarySpendingScopeOption>> GetSalarySpendingScopeOptionsAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetCalculatorAsync(cancellationToken).ConfigureAwait(false)).SpendingOptions(filter);
    }

    public async Task<SalarySpendingAnalysis> GetSalarySpendingAnalysisAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetCalculatorAsync(cancellationToken).ConfigureAwait(false)).SalarySpending(filter);
    }

    public async Task<IReadOnlyList<IncomeOwnerSummary>> GetIncomeOwnerSummariesAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
    {
        return (await GetCalculatorAsync(cancellationToken).ConfigureAwait(false)).Income(filter);
    }

    private async Task<FinancialReportCalculator> GetCalculatorAsync(CancellationToken cancellationToken)
    {
        return new(await GetDataAsync(cancellationToken).ConfigureAwait(false));
    }

    internal async Task<ReportData> GetDataAsync(CancellationToken cancellationToken)
    {
        List<ReportTransaction> transactions = new();
        List<TransactionLinkRecord> links = new();
        List<RecurringBill> bills = new();
        await using SqlConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await using SqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = _sqlResources.Read("Reports/GetReportData.sql");

        await using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                transactions.Add(new ReportTransaction
                {
                    Id = reader.GetInt64Value("ImportedTransactionId"),
                    AccountId = reader.GetInt32Value("AccountId"),
                    AccountNickname = reader.GetStringOrEmpty("AccountNickname"),
                    AccountType = reader.GetStringOrEmpty("AccountType"),
                    AccountOwner = reader.GetStringOrEmpty("AccountOwner"),
                    Date = reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                    Amount = reader.GetDecimalValue("Amount"),
                    Type = reader.GetStringOrEmpty("Type"),
                    Description = reader.GetStringOrEmpty("Description"),
                    Category = reader.GetStringOrNull("Category"),
                    SpendingClass = reader.GetStringOrNull("SpendingClass"),
                    Merchant = reader.GetStringOrNull("Merchant"),
                    IncomeOwner = reader.GetStringOrNull("IncomeOwner"),
                    IsIncome = reader.GetBooleanValue("IsIncome"),
                    IsTransfer = reader.GetBooleanValue("IsTransfer"),
                    ClassificationIsManual = reader.GetBooleanValue("ClassificationIsManual"),
                    LegacyLinkTreatment = reader.GetBooleanValue("LegacyLinkTreatment")
                });
            }

            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                links.Add(new TransactionLinkRecord
                {
                    TransactionLinkId = reader.GetInt64Value("TransactionLinkId"),
                    SourceTransactionId = reader.GetInt64Value("SourceTransactionId"),
                    LinkedTransactionId = reader.GetInt64Value("LinkedTransactionId"),
                    LinkType = reader.GetStringOrEmpty("LinkType"),
                    AllocationAmount = reader.GetDecimalOrNull("AllocationAmount"),
                    ConfidenceScore = reader.GetInt32Value("ConfidenceScore"),
                    Notes = reader.GetStringOrNull("Notes")
                });
            }

            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                bills.Add(new RecurringBill
                {
                    RecurringBillId = reader.GetInt32Value("RecurringBillId"),
                    AccountId = reader.GetInt32OrNull("AccountId"),
                    BillName = reader.GetStringOrEmpty("BillName"),
                    Payee = reader.GetStringOrEmpty("Payee"),
                    Category = reader.GetStringOrNull("Category"),
                    SpendingClass = reader.GetStringOrNull("SpendingClass"),
                    RequirementType = reader.GetStringOrEmpty("RequirementType"),
                    TypicalAmount = reader.GetDecimalValue("TypicalAmount"),
                    Frequency = reader.GetStringOrEmpty("Frequency"),
                    IsActive = reader.GetBooleanValue("IsActive"),
                    IsRequired = reader.GetBooleanValue("IsRequired"),
                    SourceDescription = reader.GetStringOrNull("SourceDescription")
                });
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ReportData { Transactions = transactions, Links = links, Bills = bills };
    }
}
