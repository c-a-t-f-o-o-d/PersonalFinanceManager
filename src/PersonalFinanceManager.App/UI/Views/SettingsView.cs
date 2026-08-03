using PersonalFinanceManager.Data.Configuration;
using PersonalFinanceManager.Data.Sql;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class SettingsView : UserControl, IPageLayoutControl
    {
        private readonly Func<Task> _settingsSavedAsync;
        private readonly SettingsService _settingsService;
        private readonly Label _connectionDefault = new();
        private readonly TextBox _connectionString = new();
        private readonly TextBox _defaultImportFolder = new();
        private readonly TextBox _backupFolder = new();
        private readonly CheckBox _maskAccountNumbers = new() { Text = "Mask account numbers by default", AutoSize = true };

        public SettingsView(SettingsService settingsService, AppSettings currentSettings, Func<Task> settingsSavedAsync)
        {
            _settingsService = settingsService;
            _settingsSavedAsync = settingsSavedAsync;
            InitializeLayout();
            LoadSettings(currentSettings);
        }

        public int PreferredPageHeight => 1000;

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(12)
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);
            _connectionDefault.Text = "No repository database credentials are stored. Choose a template or enter a custom connection string.";
            _connectionDefault.TextAlign = ContentAlignment.MiddleLeft;
            _connectionDefault.Dock = DockStyle.Fill;
            _connectionString.Multiline = true;
            _connectionString.Height = 90;
            _connectionString.ScrollBars = ScrollBars.Vertical;
            FlowLayoutPanel importFolderPanel = CreateFolderPanel(_defaultImportFolder, "Select the default import folder");
            FlowLayoutPanel backupFolderPanel = CreateFolderPanel(_backupFolder, "Select the default backup/export folder");
            int row = 0;
            AddRow(root, ref row, "Default connection", _connectionDefault, 34);
            AddRow(root, ref row, "Connection string", _connectionString, 100);
            AddRow(root, ref row, "Default import folder", importFolderPanel, 40);
            AddRow(root, ref row, "Backup/export folder", backupFolderPanel, 40);
            AddRow(root, ref row, "Privacy", _maskAccountNumbers, 34);

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            Button test = new() { Text = "Test Connection", Width = 130 };
            test.Click += async (_, _) => await TestConnectionAsync().ConfigureAwait(true);
            buttons.Controls.Add(test);
            Button save = new() { Text = "Save", Width = 100 };
            save.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
            buttons.Controls.Add(save);
            Button useLocalDbTemplate = new() { Text = "Use LocalDB Template", Width = 165 };
            useLocalDbTemplate.Click += (_, _) => _connectionString.Text = AppSettings.LocalDbConnectionString;
            buttons.Controls.Add(useLocalDbTemplate);
            Button useNetworkTemplate = new() { Text = "Use Network SQL Template", Width = 190 };
            useNetworkTemplate.Click += (_, _) => _connectionString.Text = AppSettings.NetworkSqlServerTemplateConnectionString;
            buttons.Controls.Add(useNetworkTemplate);
            AddRow(root, ref row, string.Empty, buttons, 44);

            TextBox help = new()
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                Text = "PersonalFinanceManager does not store database credentials in source control. Select a template or enter a custom connection string, then save. The local connection string is protected with Windows DPAPI and stored under %LocalAppData%\\PersonalFinanceManager\\appsettings.Local.json. Use LocalDB for development or a network SQL Server for shared multi-machine access."
            };

            AddRow(root, ref row, "Notes", help, 110);
        }

        private static void AddRow(TableLayoutPanel root, ref int row, string labelText, Control control, int height)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

            Label label = new()
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(3, 4, 3, 4);
            root.Controls.Add(label, 0, row);
            root.Controls.Add(control, 1, row);
            row++;
        }

        private void LoadSettings(AppSettings settings)
        {
            _connectionString.Text = settings.ConnectionString;
            _defaultImportFolder.Text = settings.DefaultImportFolder;
            _backupFolder.Text = settings.BackupFolder;
            _maskAccountNumbers.Checked = settings.MaskAccountNumbers;
        }

        private FlowLayoutPanel CreateFolderPanel(TextBox textBox, string description)
        {
            FlowLayoutPanel panel = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            textBox.Width = 520;
            Button browse = new() { Text = "Browse", Width = 90 };
            browse.Click += (_, _) => BrowseFolder(textBox, description);
            panel.Controls.Add(textBox);
            panel.Controls.Add(browse);
            return panel;
        }

        private void BrowseFolder(TextBox textBox, string description)
        {
            using FolderBrowserDialog dialog = new()
            {
                Description = description
            };

            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                dialog.SelectedPath = textBox.Text;
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textBox.Text = dialog.SelectedPath;
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                string connectionString = _connectionString.Text.Trim();

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    MessageBox.Show(this, "Enter a connection string first.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (AppSettings.ContainsPlaceholderSecret(connectionString))
                {
                    MessageBox.Show(this, "Replace the placeholder server, login, and password values before testing.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                await using var connection = new SqlConnectionFactory(connectionString).CreateConnection();
                await connection.OpenAsync().ConfigureAwait(true);
                MessageBox.Show(this, $"Connected to {connection.DataSource} / {connection.Database}.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Connection failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task SaveAsync()
        {
            string connectionString = _connectionString.Text.Trim();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show(this, "Enter a connection string.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (AppSettings.ContainsPlaceholderSecret(connectionString))
            {
                DialogResult result = MessageBox.Show(this, "The connection string still contains setup placeholder values. Save it anyway?", "Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            AppSettings settings = new()
            {
                ConnectionString = connectionString,
                ConnectionProfileName = AppSettings.ResolveProfileName(connectionString),
                DefaultImportFolder = _defaultImportFolder.Text.Trim(),
                BackupFolder = _backupFolder.Text.Trim(),
                MaskAccountNumbers = _maskAccountNumbers.Checked
            };

            _settingsService.Save(settings);
            await _settingsSavedAsync().ConfigureAwait(true);
            MessageBox.Show(this, "Settings saved. Database services were reloaded.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
