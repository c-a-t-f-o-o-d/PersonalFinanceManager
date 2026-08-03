using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class ImportWorkflowService : IImportWorkflowService
    {
        private readonly AccountRepository _accountRepository;
        private readonly ImportService _importService;

        public ImportWorkflowService(AccountRepository accountRepository, ImportService importService)
        {
            _accountRepository = accountRepository;
            _importService = importService;
        }

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return _accountRepository.GetAllAsync(cancellationToken);
        }

        public IReadOnlyList<ImportProfileOption> GetProfiles()
        {
            return _importService.GetProfiles();
        }

        public Task<ImportSummary> PreviewAsync(int accountId, string profileCode, string path, CancellationToken cancellationToken = default)
        {
            return _importService.PreviewAsync(accountId, profileCode, path, cancellationToken);
        }

        public Task<ImportSummary> ImportAsync(int accountId, string profileCode, string path, CancellationToken cancellationToken = default)
        {
            return _importService.ImportAsync(accountId, profileCode, path, cancellationToken);
        }

        public Task<DataFlowRepairSummary> RepairDataFlowAsync(CancellationToken cancellationToken = default)
        {
            return _importService.RepairDataFlowAsync(cancellationToken);
        }
    }
}
