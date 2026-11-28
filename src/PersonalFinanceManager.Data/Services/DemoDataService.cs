using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Application.Services.Contracts;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class DemoDataService : IDemoDataService
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources = new();

        public DemoDataService(SqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<DemoDataSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Demo/GetDemoDataSummary.sql");
            command.CommandTimeout = 60;
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new DemoDataSummary();
            }

            return new DemoDataSummary
            {
                AccountCount = Convert.ToInt32(reader["AccountCount"]),
                TransactionCount = Convert.ToInt32(reader["TransactionCount"]),
                RuleCount = Convert.ToInt32(reader["RuleCount"]),
                BillCount = Convert.ToInt32(reader["BillCount"])
            };
        }

        public async Task<DemoDataLoadResult> LoadDemoDataAsync(CancellationToken cancellationToken = default)
        {
            DemoDataSummary before = await GetSummaryAsync(cancellationToken).ConfigureAwait(false);

            if (before.HasUserData)
            {
                throw new InvalidOperationException("Demo data can only be loaded into an empty database. Create a new LocalDB database or clear the current database before loading demo data.");
            }

            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Demo/LoadDemoData.sql");
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            DemoDataSummary after = await GetSummaryAsync(cancellationToken).ConfigureAwait(false);

            return new DemoDataLoadResult
            {
                AccountsCreated = Math.Max(0, after.AccountCount - before.AccountCount),
                TransactionsCreated = Math.Max(0, after.TransactionCount - before.TransactionCount),
                RulesCreated = Math.Max(0, after.RuleCount - before.RuleCount),
                BillsCreated = Math.Max(0, after.BillCount - before.BillCount)
            };
        }
    }
}
