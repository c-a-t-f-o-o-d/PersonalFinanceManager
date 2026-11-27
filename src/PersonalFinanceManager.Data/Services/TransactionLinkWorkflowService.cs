using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class TransactionLinkWorkflowService : ITransactionLinkWorkflowService
    {
        private readonly AccountRepository _accountRepository;
        private readonly ImportService _importService;
        private readonly TransactionLinkRepository _transactionLinkRepository;

        public TransactionLinkWorkflowService(AccountRepository accountRepository, TransactionLinkRepository transactionLinkRepository, ImportService importService)
        {
            _accountRepository = accountRepository;
            _transactionLinkRepository = transactionLinkRepository;
            _importService = importService;
        }

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return _accountRepository.GetAllAsync(cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetLinkTypesAsync(CancellationToken cancellationToken = default)
        {
            return _transactionLinkRepository.GetLinkTypesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<TransactionLinkRecord>> GetLinksAsync(TransactionLinkFilter filter, CancellationToken cancellationToken = default)
        {
            return _transactionLinkRepository.GetLinksAsync(filter, cancellationToken);
        }

        public Task<IReadOnlyList<TransactionLinkTransactionOption>> GetTransactionOptionsAsync(int? accountId, string? searchText, int maxRows, CancellationToken cancellationToken = default)
        {
            return _transactionLinkRepository.GetTransactionOptionsAsync(accountId, searchText, maxRows, cancellationToken);
        }

        public Task SaveAsync(TransactionLinkEditModel model, CancellationToken cancellationToken = default)
        {
            return _transactionLinkRepository.SaveAsync(model, cancellationToken);
        }

        public Task DeleteAsync(long transactionLinkId, CancellationToken cancellationToken = default)
        {
            return _transactionLinkRepository.DeleteAsync(transactionLinkId, cancellationToken);
        }

        public Task<DataFlowRepairSummary> RepairDataFlowAsync(CancellationToken cancellationToken = default)
        {
            return _importService.RepairDataFlowAsync(cancellationToken);
        }
    }
}
