using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Repositories
{
    public sealed class ReportRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources;

        public ReportRepository(SqlConnectionFactory connectionFactory, SqlResourceReader? sqlResources = null)
        {
            _connectionFactory = connectionFactory;
            _sqlResources = sqlResources ?? new SqlResourceReader();
        }


        public async Task<ExpenseMetricSummary> GetExpenseMetricSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("Reports/GetExpenseMetricSummary.sql");

            AddCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new ExpenseMetricSummary();
            }

            return new ExpenseMetricSummary
            {
                TotalExpenses = reader.GetDecimalValue("TotalExpenses"),
                TransactionCount = reader.GetInt32Value("TransactionCount"),
                FirstTransactionDate = reader.GetDateOnlyOrNull("FirstTransactionDate"),
                LastTransactionDate = reader.GetDateOnlyOrNull("LastTransactionDate")
            };
        }

        public async Task<IReadOnlyList<ExpenseSummaryItem>> GetExpenseSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            string groupExpression = GetGroupExpression(filter.BreakdownMode);
            List<ExpenseSummaryItem> items = new();
            decimal grandTotal = 0M;
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadReportSql("Reports/GetExpenseSummary.sql", groupExpression: groupExpression);

            AddCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ExpenseSummaryItem item = new()
                {
                    GroupName = reader.GetStringOrEmpty("GroupName"),
                    TotalAmount = reader.GetDecimalValue("TotalAmount"),
                    TransactionCount = reader.GetInt32Value("TransactionCount")
                };

                grandTotal += item.TotalAmount;
                items.Add(item);
            }

            if (grandTotal > 0M)
            {
                foreach (ExpenseSummaryItem item in items)
                {
                    item.PercentOfTotal = decimal.Round(item.TotalAmount / grandTotal * 100M, 2, MidpointRounding.AwayFromZero);
                }
            }

            return items;
        }

        public async Task<IReadOnlyList<MonthlyExpenseTotalItem>> GetMonthlyExpenseTotalsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<MonthlyExpenseTotalItem> items = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("Reports/GetMonthlyExpenseTotals.sql");

            AddCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new MonthlyExpenseTotalItem
                {
                    MonthStart = reader.GetDateOnlyOrNull("MonthStart") ?? DateOnly.MinValue,
                    TotalAmount = reader.GetDecimalValue("TotalAmount"),
                    TransactionCount = reader.GetInt32Value("TransactionCount")
                });
            }

            return items;
        }

        public async Task<IReadOnlyList<ExpenseDetailItem>> GetExpenseDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            int maxRows = Math.Clamp(filter.MaxDetailRows <= 0 ? 1000 : filter.MaxDetailRows, 1, 10000);
            List<ExpenseDetailItem> items = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadReportSql("Reports/GetExpenseDetails.sql", maxRows: maxRows);

            AddCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new ExpenseDetailItem
                {
                    ImportedTransactionId = reader.GetInt64Value("ImportedTransactionId"),
                    AccountId = reader.GetInt32Value("AccountId"),
                    AccountNickname = reader.GetStringOrEmpty("AccountNickname"),
                    TransactionDate = reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                    Amount = -reader.GetDecimalValue("Amount"),
                    Type = reader.GetStringOrEmpty("Type"),
                    Description = reader.GetStringOrEmpty("Description"),
                    Category = reader.GetStringOrNull("Category"),
                    SpendingClass = reader.GetStringOrNull("SpendingClass"),
                    Merchant = reader.GetStringOrNull("Merchant")
                });
            }

            return items;
        }


        public async Task<MoneyInOutSummary> GetMoneyInOutSummaryAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("Reports/GetMoneyInOutSummary.sql");

            AddCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new MoneyInOutSummary();
            }

            return new MoneyInOutSummary
            {
                TotalMoneyIn = reader.GetDecimalValue("TotalMoneyIn"),
                TotalMoneyOut = reader.GetDecimalValue("TotalMoneyOut"),
                TransactionCount = reader.GetInt32Value("TransactionCount"),
                FirstTransactionDate = reader.GetDateOnlyOrNull("FirstTransactionDate"),
                LastTransactionDate = reader.GetDateOnlyOrNull("LastTransactionDate")
            };
        }

        public async Task<IReadOnlyList<MoneyInOutMonthlyItem>> GetMoneyInOutMonthlyAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<MoneyInOutMonthlyItem> items = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("Reports/GetMoneyInOutMonthly.sql");

            AddCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new MoneyInOutMonthlyItem
                {
                    MonthStart = reader.GetDateOnlyOrNull("MonthStart") ?? DateOnly.MinValue,
                    MoneyIn = reader.GetDecimalValue("MoneyIn"),
                    MoneyOut = reader.GetDecimalValue("MoneyOut"),
                    TransactionCount = reader.GetInt32Value("TransactionCount")
                });
            }

            return items;
        }

        public async Task<IReadOnlyList<MoneyInOutDetailItem>> GetMoneyInOutDetailsAsync(ExpenseReportFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            int maxRows = Math.Clamp(filter.MaxDetailRows <= 0 ? 1000 : filter.MaxDetailRows, 1, 10000);
            List<MoneyInOutDetailItem> items = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadReportSql("Reports/GetMoneyInOutDetails.sql", maxRows: maxRows);

            AddCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new MoneyInOutDetailItem
                {
                    ImportedTransactionId = reader.GetInt64Value("ImportedTransactionId"),
                    AccountNickname = reader.GetStringOrEmpty("AccountNickname"),
                    TransactionDate = reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                    Amount = reader.GetDecimalValue("Amount"),
                    Type = reader.GetStringOrEmpty("Type"),
                    Description = reader.GetStringOrEmpty("Description"),
                    Category = reader.GetStringOrNull("Category"),
                    SpendingClass = reader.GetStringOrNull("SpendingClass"),
                    Merchant = reader.GetStringOrNull("Merchant")
                });
            }

            return items;
        }


        public async Task<IReadOnlyList<string>> GetAvailableReportCategoriesAsync(CancellationToken cancellationToken = default)
        {
            return await GetDistinctReportValueAsync("Category", cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<string>> GetAvailableReportCategoryTypesAsync(CancellationToken cancellationToken = default)
        {
            return await GetDistinctReportValueAsync("SpendingClass", cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<SalarySpendingScopeOption>> GetSalarySpendingScopeOptionsAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<SalarySpendingScopeOption> options = new();
            int periodMonths = await GetSalaryPeriodMonthCountAsync(filter, cancellationToken).ConfigureAwait(false);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadReportSql(
                "Reports/Salary/GetSalarySpendingScopeOptions.sql",
                expenseWhereClause: GetExpenseWhereClause("t"),
                salaryScopeWhereClause: GetSalaryScopeOptionWhereClause("t"));

            AddSalaryCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                decimal totalAmount = reader.GetDecimalValue("TotalAmount");
                int activeMonths = Math.Max(1, reader.GetInt32Value("ActiveMonths"));

                options.Add(new SalarySpendingScopeOption
                {
                    Category = reader.GetStringOrEmpty("Category"),
                    SpendingClass = reader.GetStringOrEmpty("SpendingClass"),
                    TotalAmount = totalAmount,
                    TransactionCount = reader.GetInt32Value("TransactionCount"),
                    ActiveMonthCount = activeMonths,
                    PeriodMonthCount = periodMonths,
                    MonthlyAverage = decimal.Round(totalAmount / Math.Max(1, periodMonths), 2, MidpointRounding.AwayFromZero)
                });
            }

            return options;
        }

        public async Task<SalarySpendingAnalysis> GetSalarySpendingAnalysisAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);

            SalarySpendingAnalysis analysis = new()
            {
                SpendingMode = filter.SpendingMode
            };

            if (filter.SpendingMode is SalarySpendingMode.AllExpenseTrend or SalarySpendingMode.SelectedCategoriesOnly or SalarySpendingMode.RequiredBillsPlusSelectedCategories)
            {
                IReadOnlyList<MonthlyExpenseTotalItem> monthlyTrend = await GetSalaryTransactionMonthlyTrendAsync(filter, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<SalarySpendingInputLine> transactionLines = await GetTransactionSalaryLinesAsync(filter, cancellationToken).ConfigureAwait(false);
                decimal transactionMonthlyAmount = transactionLines.Sum(line => line.MonthlyAmount);
                analysis.MonthlyTrend = monthlyTrend;
                analysis.TransactionMonthlySpending = transactionMonthlyAmount;
                analysis.ActiveMonthCount = monthlyTrend.Count;
                analysis.FirstTransactionDate = monthlyTrend.Count > 0 ? monthlyTrend.Min(item => item.MonthStart) : null;
                analysis.LastTransactionDate = monthlyTrend.Count > 0 ? monthlyTrend.Max(item => item.MonthStart) : null;
                analysis.InputLines = analysis.InputLines.Concat(transactionLines).ToList();
            }

            if (filter.SpendingMode is SalarySpendingMode.RequiredBillsOnly or SalarySpendingMode.RequiredBillsPlusSelectedCategories)
            {
                IReadOnlyList<SalarySpendingInputLine> billLines = await GetRequiredBillSalaryLinesAsync(filter, cancellationToken).ConfigureAwait(false);
                analysis.RequiredBillsMonthlySpending = billLines.Sum(line => line.MonthlyAmount);
                analysis.InputLines = analysis.InputLines.Concat(billLines).ToList();
            }

            analysis.TargetMonthlySpending = analysis.TransactionMonthlySpending + analysis.RequiredBillsMonthlySpending;
            return analysis;
        }

        public async Task<IReadOnlyList<IncomeOwnerSummary>> GetIncomeOwnerSummariesAsync(SalarySpendingFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            int periodMonths = await GetSalaryPeriodMonthCountAsync(filter, cancellationToken).ConfigureAwait(false);
            List<IncomeOwnerSummary> summaries = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("Reports/Salary/GetIncomeOwnerSummaries.sql");

            AddSalaryCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                decimal totalNetIncome = reader.GetDecimalValue("TotalNetIncome");
                decimal monthlyAverage = decimal.Round(totalNetIncome / Math.Max(1, periodMonths), 2, MidpointRounding.AwayFromZero);

                summaries.Add(new IncomeOwnerSummary
                {
                    IncomeOwner = reader.GetStringOrEmpty("IncomeOwner"),
                    TotalNetIncome = totalNetIncome,
                    MonthlyAverageNetIncome = monthlyAverage,
                    AnnualizedNetIncome = monthlyAverage * 12M,
                    TransactionCount = reader.GetInt32Value("TransactionCount"),
                    PeriodMonthCount = periodMonths
                });
            }

            return summaries;
        }

        private string ReadReportSql(
            string resourcePath,
            string? groupExpression = null,
            int? maxRows = null,
            string? columnName = null,
            string? expenseWhereClause = null,
            string? salaryScopeWhereClause = null,
            string? salaryCategoryPredicate = null)
        {
            string sql = _sqlResources.Read(resourcePath);

            if (groupExpression is not null)
            {
                sql = sql.Replace("/*GROUP_EXPRESSION*/", groupExpression, StringComparison.Ordinal);
            }

            if (maxRows.HasValue)
            {
                sql = sql.Replace("/*MAX_ROWS*/", maxRows.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }

            if (columnName is not null)
            {
                sql = sql.Replace("/*COLUMN_NAME*/", columnName, StringComparison.Ordinal);
            }

            if (expenseWhereClause is not null)
            {
                sql = sql.Replace("/*EXPENSE_WHERE*/", expenseWhereClause, StringComparison.Ordinal);
            }

            if (salaryScopeWhereClause is not null)
            {
                sql = sql.Replace("/*SALARY_SCOPE_WHERE*/", salaryScopeWhereClause, StringComparison.Ordinal);
            }

            if (salaryCategoryPredicate is not null)
            {
                sql = sql.Replace("/*SALARY_CATEGORY_PREDICATE*/", salaryCategoryPredicate, StringComparison.Ordinal);
            }

            return sql;
        }

        private async Task<IReadOnlyList<string>> GetDistinctReportValueAsync(string columnName, CancellationToken cancellationToken)
        {
            if (!string.Equals(columnName, "Category", StringComparison.Ordinal) && !string.Equals(columnName, "SpendingClass", StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException(nameof(columnName));
            }

            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadReportSql("Reports/GetDistinctReportValues.sql", columnName: GetReportValueColumnExpression(columnName));

            List<string> values = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        private async Task<int> GetSalaryPeriodMonthCountAsync(SalarySpendingFilter filter, CancellationToken cancellationToken)
        {
            if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.ToDate.Value >= filter.FromDate.Value)
            {
                return CountInclusiveMonths(filter.FromDate.Value, filter.ToDate.Value);
            }

            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadReportSql(
                "Reports/Salary/GetSalaryPeriodBounds.sql",
                expenseWhereClause: GetExpenseWhereClause("t"),
                salaryScopeWhereClause: GetSalaryScopeOptionWhereClause("t"));

            AddSalaryCommonParameters(command, filter);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return 1;
            }

            DateOnly? firstDate = filter.FromDate ?? reader.GetDateOnlyOrNull("FirstDate");
            DateOnly? lastDate = filter.ToDate ?? reader.GetDateOnlyOrNull("LastDate");

            if (!firstDate.HasValue || !lastDate.HasValue || lastDate.Value < firstDate.Value)
            {
                return 1;
            }

            return CountInclusiveMonths(firstDate.Value, lastDate.Value);
        }

        private static int CountInclusiveMonths(DateOnly startDate, DateOnly endDate)
        {
            int months = (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month + 1;
            return Math.Max(1, months);
        }

        private async Task<IReadOnlyList<MonthlyExpenseTotalItem>> GetSalaryTransactionMonthlyTrendAsync(SalarySpendingFilter filter, CancellationToken cancellationToken)
        {
            List<MonthlyExpenseTotalItem> items = new();
            string categoryPredicate = BuildSalaryCategoryPredicate("t", filter, out List<SqlParameter> categoryParameters);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadReportSql(
                "Reports/Salary/GetSalaryTransactionMonthlyTrend.sql",
                expenseWhereClause: GetExpenseWhereClause("t"),
                salaryCategoryPredicate: categoryPredicate);

            AddSalaryCommonParameters(command, filter);

            foreach (SqlParameter parameter in categoryParameters)
            {
                command.Parameters.Add(parameter);
            }

            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new MonthlyExpenseTotalItem
                {
                    MonthStart = reader.GetDateOnlyOrNull("MonthStart") ?? DateOnly.MinValue,
                    TotalAmount = reader.GetDecimalValue("TotalAmount"),
                    TransactionCount = reader.GetInt32Value("TransactionCount")
                });
            }

            return items;
        }

        private async Task<IReadOnlyList<SalarySpendingInputLine>> GetTransactionSalaryLinesAsync(SalarySpendingFilter filter, CancellationToken cancellationToken)
        {
            List<SalarySpendingInputLine> lines = new();
            int periodMonths = await GetSalaryPeriodMonthCountAsync(filter, cancellationToken).ConfigureAwait(false);
            string categoryPredicate = BuildSalaryCategoryPredicate("t", filter, out List<SqlParameter> categoryParameters);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadReportSql(
                "Reports/Salary/GetTransactionSalaryLines.sql",
                expenseWhereClause: GetExpenseWhereClause("t"),
                salaryCategoryPredicate: categoryPredicate);

            AddSalaryCommonParameters(command, filter);

            foreach (SqlParameter parameter in categoryParameters)
            {
                command.Parameters.Add(parameter);
            }

            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                decimal totalAmount = reader.GetDecimalValue("TotalAmount");
                int activeMonths = Math.Max(1, reader.GetInt32Value("ActiveMonths"));
                decimal monthlyAmount = decimal.Round(totalAmount / Math.Max(1, periodMonths), 2, MidpointRounding.AwayFromZero);
                string category = reader.GetStringOrEmpty("Category");
                string categoryType = reader.GetStringOrEmpty("SpendingClass");

                lines.Add(new SalarySpendingInputLine
                {
                    Source = "Transactions",
                    Name = category + " / " + categoryType,
                    Category = category,
                    SpendingClass = categoryType,
                    MonthlyAmount = monthlyAmount,
                    AnnualizedAmount = monthlyAmount * 12M,
                    Count = reader.GetInt32Value("TransactionCount"),
                    Notes = $"Average over {periodMonths} selected month(s); active in {activeMonths} month(s)"
                });
            }

            return lines;
        }

        private async Task<IReadOnlyList<SalarySpendingInputLine>> GetRequiredBillSalaryLinesAsync(SalarySpendingFilter filter, CancellationToken cancellationToken)
        {
            List<SalarySpendingInputLine> lines = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("Reports/Salary/GetRequiredBillSalaryLines.sql");

            command.Parameters.Add(SqlParameters.Int("@AccountId", filter.AccountId));
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                decimal monthlyAmount = NormalizeBillAmount(reader.GetDecimalValue("TypicalAmount"), reader.GetStringOrEmpty("Frequency"));

                lines.Add(new SalarySpendingInputLine
                {
                    Source = "Required Bill",
                    Name = reader.GetStringOrEmpty("BillName"),
                    Category = reader.GetStringOrNull("Category"),
                    SpendingClass = reader.GetStringOrNull("SpendingClass"),
                    MonthlyAmount = monthlyAmount,
                    AnnualizedAmount = monthlyAmount * 12M,
                    Count = 1,
                    Notes = $"{reader.GetStringOrEmpty("RequirementType")} bill; {reader.GetStringOrEmpty("Frequency")} converted to monthly equivalent"
                });
            }

            return lines;
        }

        private static decimal NormalizeBillAmount(decimal amount, string frequency)
        {
            string normalized = string.IsNullOrWhiteSpace(frequency) ? "Monthly" : frequency.Trim();
            decimal monthly;

            if (string.Equals(normalized, "Weekly", StringComparison.OrdinalIgnoreCase))
            {
                monthly = amount * 52M / 12M;
            }
            else if (string.Equals(normalized, "Biweekly", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "Bi-Weekly", StringComparison.OrdinalIgnoreCase))
            {
                monthly = amount * 26M / 12M;
            }
            else if (string.Equals(normalized, "Quarterly", StringComparison.OrdinalIgnoreCase))
            {
                monthly = amount / 3M;
            }
            else if (string.Equals(normalized, "Semiannual", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "Semi-Annual", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "Twice Yearly", StringComparison.OrdinalIgnoreCase))
            {
                monthly = amount / 6M;
            }
            else if (string.Equals(normalized, "Annual", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "Annually", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "Yearly", StringComparison.OrdinalIgnoreCase))
            {
                monthly = amount / 12M;
            }
            else
            {
                monthly = amount;
            }

            return decimal.Round(monthly, 2, MidpointRounding.AwayFromZero);
        }

        private static string BuildSalaryCategoryPredicate(string alias, SalarySpendingFilter filter, out List<SqlParameter> parameters)
        {
            parameters = new List<SqlParameter>();

            if (filter.SpendingMode is not (SalarySpendingMode.SelectedCategoriesOnly or SalarySpendingMode.RequiredBillsPlusSelectedCategories))
            {
                return string.Empty;
            }

            List<string> predicates = new();

            for (int index = 0; index < filter.SelectedSpendingBuckets.Count; index++)
            {
                SalarySpendingScopeOption bucket = filter.SelectedSpendingBuckets[index];
                string categoryParameterName = "@SalaryBucketCategory" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string typeParameterName = "@SalaryBucketType" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                predicates.Add($"(ISNULL(NULLIF(LTRIM(RTRIM({alias}.Category)), N''), N'(Uncategorized)') = {categoryParameterName} AND ISNULL(NULLIF(LTRIM(RTRIM({alias}.SpendingClass)), N''), N'(Uncategorized)') = {typeParameterName})");
                parameters.Add(SqlParameters.NVarChar(categoryParameterName, bucket.Category, 200));
                parameters.Add(SqlParameters.NVarChar(typeParameterName, bucket.SpendingClass, 200));
            }

            if (filter.SelectedSpendingBuckets.Count == 0)
            {
                for (int index = 0; index < filter.SelectedCategories.Count; index++)
                {
                    string value = filter.SelectedCategories[index];

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    string parameterName = "@SalaryCategory" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    predicates.Add($"LTRIM(RTRIM({alias}.Category)) = {parameterName}");
                    parameters.Add(SqlParameters.NVarChar(parameterName, value.Trim(), 200));
                }

                for (int index = 0; index < filter.SelectedCategoryTypes.Count; index++)
                {
                    string value = filter.SelectedCategoryTypes[index];

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    string parameterName = "@SalaryCategoryType" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    predicates.Add($"LTRIM(RTRIM({alias}.SpendingClass)) = {parameterName}");
                    parameters.Add(SqlParameters.NVarChar(parameterName, value.Trim(), 200));
                }
            }

            if (predicates.Count == 0)
            {
                return " AND 1 = 0";
            }

            return " AND (" + string.Join(" OR ", predicates) + ")";
        }

        private string GetSalaryScopeOptionWhereClause(string alias)
        {
            return ReadSqlFragment("Reports/Fragments/SalaryScopeWhere.sql", alias);
        }

        private string GetExpenseWhereClause(string alias)
        {
            return ReadSqlFragment("Reports/Fragments/ExpenseWhere.sql", alias);
        }

        private string ReadSqlFragment(string resourcePath, string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("A SQL alias is required.", nameof(alias));
            }

            return _sqlResources.Read(resourcePath).Replace("/*ALIAS*/", alias.Trim(), StringComparison.Ordinal);
        }

        private static void AddSalaryCommonParameters(SqlCommand command, SalarySpendingFilter filter)
        {
            command.Parameters.Add(SqlParameters.Int("@AccountId", filter.AccountId));
            command.Parameters.Add(SqlParameters.Date("@FromDate", filter.FromDate));
            command.Parameters.Add(SqlParameters.Date("@ToDate", filter.ToDate));
        }

        private static void AddCommonParameters(SqlCommand command, ExpenseReportFilter filter)
        {
            command.Parameters.Add(SqlParameters.Int("@AccountId", filter.AccountId));
            command.Parameters.Add(SqlParameters.Date("@FromDate", filter.FromDate));
            command.Parameters.Add(SqlParameters.Date("@ToDate", filter.ToDate));
        }

        private static string GetReportValueColumnExpression(string columnName)
        {
            if (string.Equals(columnName, "Category", StringComparison.Ordinal))
            {
                return "Category";
            }

            if (string.Equals(columnName, "SpendingClass", StringComparison.Ordinal))
            {
                return "SpendingClass";
            }

            throw new ArgumentOutOfRangeException(nameof(columnName));
        }

        private static string GetGroupExpression(string? breakdownMode)
        {
            if (string.Equals(breakdownMode, ExpenseBreakdownModes.CategoryType, StringComparison.OrdinalIgnoreCase))
            {
                return "ISNULL(NULLIF(LTRIM(RTRIM(t.SpendingClass)), ''), '(Uncategorized)')";
            }

            return "ISNULL(NULLIF(LTRIM(RTRIM(t.Category)), ''), '(Uncategorized)')";
        }
    }
}
