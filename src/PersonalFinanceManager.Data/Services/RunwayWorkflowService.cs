using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class RunwayWorkflowService : IRunwayWorkflowService
    {
        private readonly AccountRepository _accountRepository;
        private readonly RecurringBillRepository _billRepository;

        public RunwayWorkflowService(AccountRepository accountRepository, RecurringBillRepository billRepository)
        {
            _accountRepository = accountRepository;
            _billRepository = billRepository;
        }

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return _accountRepository.GetAllAsync(cancellationToken);
        }

        public Task<IReadOnlyList<RecurringBill>> GetBillsAsync(CancellationToken cancellationToken = default)
        {
            return _billRepository.GetBillsAsync(cancellationToken);
        }
    }
}
