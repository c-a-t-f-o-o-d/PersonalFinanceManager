using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IRunwayWorkflowService
    {
        Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RecurringBill>> GetBillsAsync(CancellationToken cancellationToken = default);
    }
}
