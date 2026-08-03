using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IImportWorkflowService
    {
        Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        IReadOnlyList<ImportProfileOption> GetProfiles();

        Task<ImportSummary> PreviewAsync(int accountId, string profileCode, string path, CancellationToken cancellationToken = default);

        Task<ImportSummary> ImportAsync(int accountId, string profileCode, string path, CancellationToken cancellationToken = default);

        Task<DataFlowRepairSummary> RepairDataFlowAsync(CancellationToken cancellationToken = default);
    }
}
