namespace PersonalFinanceManager.Core.Models
{
    public sealed class DatabaseBackupResult
    {
        public bool Succeeded { get; set; }
        public bool Verified { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
