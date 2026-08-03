namespace PersonalFinanceManager.Core.Application
{
    public static class ApplicationIdentity
    {
        public const string ProductName = "PersonalFinanceManager";
        public const string ApplicationName = "PersonalFinanceManager";
        public const string DatabaseName = "PersonalFinanceManager";
        public const string LocalApplicationDataFolderName = "PersonalFinanceManager";
        public const string BackupFilePrefix = "PersonalFinanceManager";
        public const string LocalDbProfileName = "LocalDb";
        public const string NetworkSqlServerTemplateProfileName = "NetworkSqlServerTemplate";
        public const string SetupRequiredProfileName = "SetupRequired";
        public const string CustomProfileName = "Custom";
        public const string LocalDbConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=PersonalFinanceManager;Integrated Security=True;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name=PersonalFinanceManager";
        public const string NetworkSqlServerTemplateConnectionString = "Data Source=tcp:YOUR_SQL_SERVER_OR_IP,14333;Initial Catalog=PersonalFinanceManager;User ID=YOUR_SQL_LOGIN;Password=YOUR_SQL_PASSWORD;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name=PersonalFinanceManager";
        public const string EmptyConnectionString = "";
    }
}
