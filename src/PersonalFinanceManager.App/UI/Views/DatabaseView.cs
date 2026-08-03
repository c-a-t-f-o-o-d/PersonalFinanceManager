using PersonalFinanceManager.Data.Database;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class DatabaseView : UserControl, IPageLayoutControl
    {
        private readonly Func<Task> _initializeAsync;
        private readonly SchemaStatusService _schemaStatusService;
        private readonly TextBox _statusText = new();

        public DatabaseView(SchemaStatusService schemaStatusService, Func<Task> initializeAsync)
        {
            _schemaStatusService = schemaStatusService;
            _initializeAsync = initializeAsync;
            InitializeLayout();
        }

        public int PreferredPageHeight => 1000;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await RefreshStatusAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            FlowLayoutPanel commands = new()
            {
                Dock = DockStyle.Fill
            };

            root.Controls.Add(commands, 0, 0);
            Button refresh = new() { Text = "Refresh", Width = 120 };
            refresh.Click += async (_, _) => await RefreshStatusAsync().ConfigureAwait(true);
            commands.Controls.Add(refresh);
            Button initialize = new() { Text = "Initialize Schema", Width = 150 };

            initialize.Click += async (_, _) =>
            {
                await _initializeAsync().ConfigureAwait(true);
                await RefreshStatusAsync().ConfigureAwait(true);
            };

            commands.Controls.Add(initialize);
            _statusText.Dock = DockStyle.Fill;
            _statusText.Multiline = true;
            _statusText.ScrollBars = ScrollBars.Vertical;
            _statusText.ReadOnly = true;
            root.Controls.Add(_statusText, 0, 1);
        }

        private async Task RefreshStatusAsync()
        {
            SchemaStatus status = await _schemaStatusService.GetStatusAsync().ConfigureAwait(true);

            _statusText.Text = $"Connection: {(status.Connected ? "Connected" : "Disconnected")}{Environment.NewLine}" +
                $"Server version: {status.ServerVersion}{Environment.NewLine}" +
                $"Database: {status.DatabaseName}{Environment.NewLine}" +
                $"Schema version: {status.SchemaVersion}{Environment.NewLine}" +
                $"Schema state: {status.SchemaState}{Environment.NewLine}" +
                $"Table count: {status.TableCount}{Environment.NewLine}" +
                $"Applied migrations: {status.AppliedMigrationCount}{Environment.NewLine}" +
                $"Pending migrations: {status.PendingMigrationCount}{Environment.NewLine}" +
                $"Failed migrations: {status.FailedMigrationCount}{Environment.NewLine}" +
                $"Latest migration: {status.LatestMigration}{Environment.NewLine}" +
                $"Message: {status.Message}";
        }
    }
}
