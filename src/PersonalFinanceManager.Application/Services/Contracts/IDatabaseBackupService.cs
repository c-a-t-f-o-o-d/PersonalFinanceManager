using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IDatabaseBackupService
    {
        Task<IReadOnlyList<DatabaseBackupRecord>> GetHistoryAsync(CancellationToken cancellationToken = default);

        Task<DatabaseBackupResult> CreateSqlBackupAsync(string folderPath, CancellationToken cancellationToken = default);

        Task<DatabaseBackupResult> ExportDataZipAsync(string folderPath, CancellationToken cancellationToken = default);
    }
}
