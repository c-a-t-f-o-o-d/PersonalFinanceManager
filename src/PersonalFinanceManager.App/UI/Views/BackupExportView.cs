using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManagerIdentity = PersonalFinanceManager.Core.Application.ApplicationIdentity;
using PersonalFinanceManager.Data.Configuration;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class BackupExportView : UserControl, IPageLayoutControl
    {
        private readonly AppSettings _settings;
        private readonly IDatabaseBackupService _backupService;
        private readonly SettingsService _settingsService;
        private readonly BindingSource _historySource = new();
        private readonly TextBox _folder = new();
        private readonly Label _status = new();
        private readonly DataGridView _historyGrid = new();

        public BackupExportView(IDatabaseBackupService backupService, SettingsService settingsService, AppSettings settings)
        {
            _backupService = backupService;
            _settingsService = settingsService;
            _settings = settings;
            InitializeLayout();
        }

        public int PreferredPageHeight => 1000;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _folder.Text = string.IsNullOrWhiteSpace(_settings.BackupFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), PersonalFinanceManagerIdentity.ProductName + " Backups")
                : _settings.BackupFolder;

            await RefreshHistoryAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            FlowLayoutPanel commandRow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            root.Controls.Add(commandRow, 0, 0);
            commandRow.Controls.Add(MakeLabel("Folder", 50));
            _folder.Width = 520;
            commandRow.Controls.Add(_folder);
            Button browse = new() { Text = "Browse", Width = 90 };
            browse.Click += (_, _) => BrowseFolder();
            commandRow.Controls.Add(browse);
            Button saveFolder = new() { Text = "Save Folder", Width = 105 };
            saveFolder.Click += (_, _) => SaveFolder();
            commandRow.Controls.Add(saveFolder);
            Button backup = new() { Text = "SQL Backup (.bak)", Width = 135 };
            backup.Click += async (_, _) => await CreateSqlBackupAsync().ConfigureAwait(true);
            commandRow.Controls.Add(backup);
            Button export = new() { Text = "Export CSV Zip", Width = 125 };
            export.Click += async (_, _) => await ExportDataZipAsync().ConfigureAwait(true);
            commandRow.Controls.Add(export);
            _status.Text = "Choose a folder, then create a SQL backup or portable CSV export.";
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(_status, 0, 1);
            ConfigureHistoryGrid();
            root.Controls.Add(_historyGrid, 0, 2);
            PageLayout.AutoSizeHeaders(root, 0);
        }

        private static Label MakeLabel(string text, int width)
        {
            return new Label
            {
                Text = text,
                Width = width,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(3, 8, 3, 3)
            };
        }

        private void ConfigureHistoryGrid()
        {
            _historyGrid.Dock = DockStyle.Fill;
            _historyGrid.AutoGenerateColumns = false;
            _historyGrid.AllowUserToAddRows = false;
            _historyGrid.ReadOnly = true;
            _historyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _historyGrid.DataSource = _historySource;
            AddColumn("CreatedAtUtc", "Created UTC", 145);
            AddColumn("BackupType", "Type", 100);
            AddColumn("Succeeded", "Succeeded", 85);
            AddColumn("Verified", "Verified", 80);
            AddColumn("FilePath", "File", 520);
            AddColumn("ErrorMessage", "Error", 420);
        }

        private void AddColumn(string property, string header, int width)
        {
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = property,
                HeaderText = header,
                Width = width
            });
        }

        private void BrowseFolder()
        {
            using FolderBrowserDialog dialog = new()
            {
                Description = "Select backup/export folder"
            };

            if (!string.IsNullOrWhiteSpace(_folder.Text))
            {
                dialog.SelectedPath = _folder.Text;
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _folder.Text = dialog.SelectedPath;
            }
        }

        private void SaveFolder()
        {
            _settings.BackupFolder = _folder.Text.Trim();
            _settingsService.Save(_settings);
            _status.Text = "Backup/export folder saved.";
        }

        private async Task CreateSqlBackupAsync()
        {
            try
            {
                _status.Text = "Creating SQL Server backup...";
                UseWaitCursor = true;
                SaveFolder();
                var result = await _backupService.CreateSqlBackupAsync(_folder.Text.Trim()).ConfigureAwait(true);
                _status.Text = result.Message + " " + result.FilePath;
                await RefreshHistoryAsync().ConfigureAwait(true);
                MessageBox.Show(this, result.Message + Environment.NewLine + result.FilePath, "Backup", MessageBoxButtons.OK, result.Succeeded ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private async Task ExportDataZipAsync()
        {
            try
            {
                _status.Text = "Creating portable CSV export...";
                UseWaitCursor = true;
                SaveFolder();
                var result = await _backupService.ExportDataZipAsync(_folder.Text.Trim()).ConfigureAwait(true);
                _status.Text = result.Message + " " + result.FilePath;
                await RefreshHistoryAsync().ConfigureAwait(true);
                MessageBox.Show(this, result.Message + Environment.NewLine + result.FilePath, "Export", MessageBoxButtons.OK, result.Succeeded ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private async Task RefreshHistoryAsync()
        {
            _historySource.DataSource = await _backupService.GetHistoryAsync().ConfigureAwait(true);
        }
    }
}
