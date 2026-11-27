using System.Text;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Database
{
    public sealed class SqlScriptRunner
    {
        public async Task ExecuteScriptAsync(SqlConnection connection, string script, CancellationToken cancellationToken = default)
        {
            foreach (string batch in SplitBatches(script))
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await using SqlCommand command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public IReadOnlyList<string> SplitBatches(string script)
        {
            ArgumentNullException.ThrowIfNull(script);
            string[] lines = script.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            List<string> batches = new();
            StringBuilder current = new();

            foreach (string line in lines)
            {
                if (string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase))
                {
                    batches.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.AppendLine(line);
            }

            batches.Add(current.ToString());
            return batches;
        }
    }
}
