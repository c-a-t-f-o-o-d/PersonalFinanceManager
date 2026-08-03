using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IRecurringBillWorkflowService
    {
        Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetCategoryValuesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetCategoryTypeValuesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetRequirementTypesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RecurringBill>> GetBillsAsync(CancellationToken cancellationToken = default);

        Task<RecurringBill?> GetBillAsync(int recurringBillId, CancellationToken cancellationToken = default);

        Task<int> SaveBillAsync(RecurringBill bill, CancellationToken cancellationToken = default);

        Task DeleteBillAsync(int recurringBillId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RecurringBillCandidate>> GetMonthlyCandidatesAsync(int minimumTransactions, int minimumMonths, CancellationToken cancellationToken = default);
    }
}
