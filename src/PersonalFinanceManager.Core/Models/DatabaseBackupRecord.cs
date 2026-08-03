namespace PersonalFinanceManager.Core.Models
{
    public sealed class DatabaseBackupRecord
    {
        public int DatabaseBackupHistoryId { get; set; }
        public string BackupType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public bool Succeeded { get; set; }
        public bool Verified { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
