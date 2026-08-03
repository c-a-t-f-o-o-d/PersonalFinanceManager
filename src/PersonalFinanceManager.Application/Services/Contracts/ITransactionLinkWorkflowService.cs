using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface ITransactionLinkWorkflowService
    {
        Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetLinkTypesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TransactionLinkRecord>> GetLinksAsync(TransactionLinkFilter filter, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TransactionLinkTransactionOption>> GetTransactionOptionsAsync(int? accountId, string? searchText, int maxRows, CancellationToken cancellationToken = default);

        Task SaveAsync(TransactionLinkEditModel model, CancellationToken cancellationToken = default);

        Task DeleteAsync(long transactionLinkId, CancellationToken cancellationToken = default);

        Task<int> ApplyPayPalFundingTreatmentAsync(IEnumerable<long> transactionLinkIds, CancellationToken cancellationToken = default);

        Task<int> ApplyCreditCardPaymentTreatmentAsync(IEnumerable<long> transactionLinkIds, CancellationToken cancellationToken = default);

        Task<int> UndoPayPalFundingTreatmentAsync(IEnumerable<long> transactionLinkIds, CancellationToken cancellationToken = default);

        Task<DataFlowRepairSummary> RepairDataFlowAsync(CancellationToken cancellationToken = default);
    }
}
