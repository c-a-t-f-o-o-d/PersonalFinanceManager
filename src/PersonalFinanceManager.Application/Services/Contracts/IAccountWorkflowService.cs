using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IAccountWorkflowService
    {
        Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        Task<Account?> GetAccountForEditAsync(int accountId, CancellationToken cancellationToken = default);

        Task<int> SaveAccountAsync(Account account, CancellationToken cancellationToken = default);

        Task DeleteAccountAsync(int accountId, CancellationToken cancellationToken = default);
    }
}
