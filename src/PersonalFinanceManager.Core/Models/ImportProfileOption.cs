namespace PersonalFinanceManager.Core.Models
{
    public sealed class ImportProfileOption
    {
        public ImportProfileOption(string profileCode, string displayName, string fileDialogFilter)
        {
            ProfileCode = profileCode;
            DisplayName = displayName;
            FileDialogFilter = fileDialogFilter;
        }

        public string ProfileCode { get; }
        public string DisplayName { get; }
        public string FileDialogFilter { get; }
    }
}
