using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IDataHealthWorkflowService
    {
        Task<IReadOnlyList<DataHealthCheckResult>> GetChecksAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DataHealthDetailRow>> GetDetailsAsync(string issueCode, CancellationToken cancellationToken = default);

        Task<int> SyncTransactionOwnersFromAccountsAsync(CancellationToken cancellationToken = default);
    }
}
