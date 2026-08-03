using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Sql
{
    public sealed class SqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString?.Trim() ?? string.Empty;
        }

        public bool HasConnectionString => !string.IsNullOrWhiteSpace(_connectionString);

        public SqlConnection CreateConnection()
        {
            if (!HasConnectionString)
            {
                throw new InvalidOperationException("Configure a database connection in Settings before using database features.");
            }

            return new SqlConnection(_connectionString);
        }

        public SqlConnection CreateMasterConnection()
        {
            if (!HasConnectionString)
            {
                throw new InvalidOperationException("Configure a database connection in Settings before using database features.");
            }

            SqlConnectionStringBuilder builder = new(_connectionString)
            {
                InitialCatalog = "master"
            };

            return new SqlConnection(builder.ConnectionString);
        }
    }
}
