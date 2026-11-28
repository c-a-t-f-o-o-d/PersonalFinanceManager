using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Data.Configuration;
using PersonalFinanceManager.Data.Sql;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class SettingsView : UserControl, IPageLayoutControl
    {
        private readonly Func<Task> _settingsSavedAsync;
        private readonly SettingsService _settingsService;
        private readonly TextBox _connectionString = new();
        private readonly TextBox _defaultImportFolder = new();
        private readonly TextBox _backupFolder = new();
        private readonly CheckBox _maskAccountNumbers = new() { Text = "Mask account numbers by default", AutoSize = true };
        private readonly CheckBox _useDarkMode = new() { Name = "UseDarkMode", Text = "Use dark mode", AccessibleName = "Dark mode", AutoSize = true };
        private bool _updatingAppearance;

        public SettingsView(SettingsService settingsService, AppSettings currentSettings, Func<Task> settingsSavedAsync)
        {
            _settingsService = settingsService;
            _settingsSavedAsync = settingsSavedAsync;
            InitializeLayout();
            LoadSettings(currentSettings);
            _useDarkMode.Checked = UiTheme.IsDarkMode;
            _useDarkMode.CheckedChanged += (_, _) => ChangeAppearance();
            UiTheme.ThemeChanged += HandleThemeChanged;
        }

        public int PreferredPageHeight => 900;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UiTheme.ThemeChanged -= HandleThemeChanged;
            }

            base.Dispose(disposing);
        }

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Top,
                Height = 858,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 342));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 282));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            Controls.Add(root);

            TableLayoutPanel appearance = CreateSection(root, 0, "Appearance", "Choose a comfortable color theme. Changes apply immediately and are remembered on this PC.");
            _useDarkMode.Margin = new Padding(0, 7, 0, 0);
            appearance.Controls.Add(_useDarkMode, 0, 1);

            TableLayoutPanel connection = CreateSection(root, 1, "Database connection", "Choose a local database on this PC or connect to a shared SQL Server. Test the connection before saving.");
            TableLayoutPanel connectionFields = CreateFields();
            _connectionString.Name = "ConnectionString";
            _connectionString.AccessibleName = "Database connection string";
            _connectionString.Multiline = true;
            _connectionString.ScrollBars = ScrollBars.Vertical;
            AddRow(connectionFields, 0, "Connection string", _connectionString, 94);

            FlowLayoutPanel templates = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            Button useLocalDbTemplate = new() { Text = "Use local database", Width = 160, Height = 34, Margin = new Padding(0, 3, 8, 3) };
            useLocalDbTemplate.Click += (_, _) => _connectionString.Text = AppSettings.LocalDbConnectionString;
            templates.Controls.Add(useLocalDbTemplate);
            Button useNetworkTemplate = new() { Text = "Use SQL Server template", Width = 198, Height = 34, Margin = new Padding(0, 3, 0, 3) };
            useNetworkTemplate.Click += (_, _) => _connectionString.Text = AppSettings.NetworkSqlServerTemplateConnectionString;
            templates.Controls.Add(useNetworkTemplate);
            AddRow(connectionFields, 1, "Templates", templates, 46);

            Button test = new() { Text = "Test connection", Width = 152, Height = 34, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 3) };
            test.Click += async (_, _) => await TestConnectionAsync().ConfigureAwait(true);
            AddRow(connectionFields, 2, string.Empty, test, 44);
            test.Dock = DockStyle.None;
            connection.Controls.Add(connectionFields, 0, 1);

            TableLayoutPanel files = CreateSection(root, 2, "Files & privacy", "Set convenient folders for statement imports and backups, and choose how account numbers appear.");
            TableLayoutPanel fileFields = CreateFields();
            AddRow(fileFields, 0, "Default import folder", CreateFolderPanel(_defaultImportFolder, "Select the default import folder"), 44);
            AddRow(fileFields, 1, "Backup / export folder", CreateFolderPanel(_backupFolder, "Select the default backup/export folder"), 44);
            AddRow(fileFields, 2, "Account numbers", _maskAccountNumbers, 42);
            files.Controls.Add(fileFields, 0, 1);

            FlowLayoutPanel actions = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = new Padding(0, 8, 0, 0)
            };

            Button save = new() { Text = "Save settings", Width = 144, Height = 38, Margin = new Padding(0, 0, 16, 0) };
            UiTheme.StylePrimaryButton(save);
            save.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
            actions.Controls.Add(save);

            Label note = new()
            {
                Text = "Connection details are protected for your Windows account. Keep backups in a secure folder.",
                Width = 580,
                Height = 46,
                Padding = new Padding(0, 3, 0, 0),
                Font = UiTheme.SmallFont
            };

            UiTheme.SetTextRole(note, "SecondaryText");
            actions.Controls.Add(note);
            root.Controls.Add(actions, 0, 3);
        }

        private static TableLayoutPanel CreateSection(TableLayoutPanel root, int row, string title, string description)
        {
            CardPanel card = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 16, 20, 16),
                Margin = new Padding(0, 0, 0, 16)
            };

            TableLayoutPanel section = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };

            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            section.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Panel heading = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
            Label titleLabel = new() { Text = title, Font = UiTheme.SectionTitleFont, Dock = DockStyle.Top, Height = 24, UseMnemonic = false };
            Label descriptionLabel = new() { Text = description, Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            UiTheme.SetTextRole(descriptionLabel, "SecondaryText");
            heading.Controls.Add(descriptionLabel);
            heading.Controls.Add(titleLabel);
            section.Controls.Add(heading, 0, 0);
            card.Controls.Add(section);
            root.Controls.Add(card, 0, row);
            return section;
        }

        private static TableLayoutPanel CreateFields()
        {
            TableLayoutPanel fields = new() { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 164));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return fields;
        }

        private static void AddRow(TableLayoutPanel root, int row, string labelText, Control control, int height)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Label label = new() { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 4, 0, 4);
            root.Controls.Add(label, 0, row);
            root.Controls.Add(control, 1, row);
        }

        private void ChangeAppearance()
        {
            if (_updatingAppearance)
            {
                return;
            }

            try
            {
                _settingsService.SaveAppearancePreference(_useDarkMode.Checked);
                UiTheme.SetDarkMode(_useDarkMode.Checked);
            }
            catch (Exception ex)
            {
                HandleThemeChanged(this, EventArgs.Empty);
                MessageBox.Show(this, "The appearance preference could not be saved. " + ex.Message, "Appearance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void HandleThemeChanged(object? sender, EventArgs e)
        {
            _updatingAppearance = true;

            try
            {
                _useDarkMode.Checked = UiTheme.IsDarkMode;
            }
            finally
            {
                _updatingAppearance = false;
            }
        }

        private void LoadSettings(AppSettings settings)
        {
            _connectionString.Text = settings.ConnectionString;
            _defaultImportFolder.Text = settings.DefaultImportFolder;
            _backupFolder.Text = settings.BackupFolder;
            _maskAccountNumbers.Checked = settings.MaskAccountNumbers;
        }

        private TableLayoutPanel CreateFolderPanel(TextBox textBox, string description)
        {
            TableLayoutPanel panel = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };

            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(0, 5, 8, 3);
            textBox.AccessibleName = description;
            Button browse = new() { Text = "Browse…", Dock = DockStyle.Fill, Margin = Padding.Empty };
            browse.Click += (_, _) => BrowseFolder(textBox, description);
            panel.Controls.Add(textBox, 0, 0);
            panel.Controls.Add(browse, 1, 0);
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
                MaskAccountNumbers = _maskAccountNumbers.Checked,
                UseDarkMode = UiTheme.IsDarkMode
            };

            _settingsService.Save(settings);
            await _settingsSavedAsync().ConfigureAwait(true);
            MessageBox.Show(this, "Settings saved. Database services were reloaded.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
