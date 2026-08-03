using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class DataHealthWorkflowService : IDataHealthWorkflowService
    {
        private readonly DataHealthRepository _dataHealthRepository;

        public DataHealthWorkflowService(DataHealthRepository dataHealthRepository)
        {
            _dataHealthRepository = dataHealthRepository;
        }

        public Task<IReadOnlyList<DataHealthCheckResult>> GetChecksAsync(CancellationToken cancellationToken = default)
        {
            return _dataHealthRepository.GetChecksAsync(cancellationToken);
        }

        public Task<IReadOnlyList<DataHealthDetailRow>> GetDetailsAsync(string issueCode, CancellationToken cancellationToken = default)
        {
            return _dataHealthRepository.GetDetailsAsync(issueCode, cancellationToken);
        }

        public Task<int> SyncTransactionOwnersFromAccountsAsync(CancellationToken cancellationToken = default)
        {
            return _dataHealthRepository.SyncTransactionOwnersFromAccountsAsync(cancellationToken);
        }
    }
}
