using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface ITransactionWorkflowService
    {
        Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetCategoryValuesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetCategoryTypeValuesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ImportedTransaction>> GetTransactionsAsync(TransactionFilter filter, CancellationToken cancellationToken = default);

        Task<ImportedTransaction?> GetTransactionAsync(long importedTransactionId, CancellationToken cancellationToken = default);

        Task<long> SaveTransactionAsync(ImportedTransaction transaction, CancellationToken cancellationToken = default);

        Task DeleteTransactionAsync(long importedTransactionId, CancellationToken cancellationToken = default);

        Task<int> UpdateCategorizationAsync(IEnumerable<long> importedTransactionIds, string? category, string? categoryType, CancellationToken cancellationToken = default);

        Task<int> CreateRuleAndApplyAsync(FinancialRule rule, CancellationToken cancellationToken = default);
    }
}
