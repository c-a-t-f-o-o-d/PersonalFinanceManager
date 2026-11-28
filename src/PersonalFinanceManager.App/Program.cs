using PersonalFinanceManager.App.UI;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManagerIdentity = PersonalFinanceManager.Core.Application.ApplicationIdentity;
using PersonalFinanceManager.Data.Configuration;
using PersonalFinanceManager.Data.Logging;

namespace PersonalFinanceManager.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            FileLogger logger = new();
            System.Windows.Forms.Application.ThreadException += (_, args) => ShowUnhandledError(logger, args.Exception);

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    logger.Error("UnhandledException", "Unhandled non-UI exception.", exception);
                }
            };

            SettingsService settingsService = new();
            UiTheme.SetDarkMode(settingsService.Load().UseDarkMode);
            System.Windows.Forms.Application.Run(new MainForm(settingsService, logger));
        }

        private static void ShowUnhandledError(FileLogger logger, Exception exception)
        {
            logger.Error("ThreadException", "Unhandled UI exception.", exception);
            MessageBox.Show(exception.Message, PersonalFinanceManagerIdentity.ProductName + " Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
