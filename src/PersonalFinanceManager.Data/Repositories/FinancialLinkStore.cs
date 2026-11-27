using Microsoft.Data.SqlClient;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Sql;

namespace PersonalFinanceManager.Data.Repositories;

internal sealed class FinancialLinkState
{
    public List<FinancialLinkTransaction> Transactions { get; } = new();
    public List<FinancialLink> Links { get; } = new();
    public List<FinancialLinkRejection> Rejections { get; } = new();
}

internal static class FinancialLinkStore
{
    public static async Task<FinancialLinkState> LoadAsync(SqlConnection connection,
        SqlTransaction transaction,
        SqlResourceReader resources,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = resources.Read("TransactionLinks/GetFinancialState.sql");
        FinancialLinkState state = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            state.Transactions.Add(new FinancialLinkTransaction(reader.GetInt64Value("ImportedTransactionId"),
                reader.GetInt32Value("AccountId"), reader.GetStringOrEmpty("AccountType"), reader.GetStringOrEmpty("AccountNickname"),
                reader.GetStringOrEmpty("Institution"), reader.GetStringOrNull("AccountNumberLastFour"),
                reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue, reader.GetDecimalValue("Amount"),
                reader.GetStringOrEmpty("Type"), reader.GetStringOrEmpty("Description"), reader.GetStringOrNull("ExternalTransactionId"),
                reader.GetStringOrNull("FundingSource"), reader.GetStringOrNull("Category"), reader.GetBooleanValue("IsTransfer")));
        }

        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            state.Links.Add(new FinancialLink(reader.GetInt64Value("TransactionLinkId"),
                reader.GetInt64Value("SourceTransactionId"),
                reader.GetInt64Value("LinkedTransactionId"),
                reader.GetStringOrEmpty("LinkType"),
                reader.GetDecimalOrNull("AllocationAmount")));
        }

        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            state.Rejections.Add(new FinancialLinkRejection(reader.GetInt64Value("SourceTransactionId"), reader.GetInt64Value("LinkedTransactionId"), reader.GetStringOrEmpty("LinkType")));
        }

        return state;
    }

    public static void Validate(FinancialLinkState state, TransactionLinkEditModel model)
    {
        FinancialLinkTransaction? source = state.Transactions.FirstOrDefault(t => t.Id == model.SourceTransactionId);
        FinancialLinkTransaction? linked = state.Transactions.FirstOrDefault(t => t.Id == model.LinkedTransactionId);

        if (source is null || linked is null)
        {
            throw new InvalidOperationException("One of the selected transactions no longer exists.");
        }

        if (model.TransactionLinkId != 0 && !state.Links.Any(l => l.Id == model.TransactionLinkId))
        {
            throw new InvalidOperationException("The selected link no longer exists. Refresh and try again.");
        }

        FinancialLinkRules.Validate(model, source, linked, state.Links, state.Transactions.ToDictionary(t => t.Id));
    }
}
