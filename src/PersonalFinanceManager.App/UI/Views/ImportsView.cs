using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class ImportsView : UserControl, IPageLayoutControl
    {
        private readonly IImportWorkflowService _importWorkflowService;
        private readonly ComboBox _accounts = new();
        private readonly ComboBox _profiles = new();
        private readonly TextBox _filePath = new();
        private readonly BindingSource _bindingSource = new();
        private readonly DataGridView _grid = new();
        private readonly TextBox _summary = new();

        public ImportsView(IImportWorkflowService importWorkflowService)
        {
            _importWorkflowService = importWorkflowService ?? throw new ArgumentNullException(nameof(importWorkflowService));
            InitializeLayout();
        }

        public int PreferredPageHeight => 1050;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadAccountsAsync().ConfigureAwait(true);
            LoadProfiles();
        }

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 4
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            FlowLayoutPanel accountRow = new() { Dock = DockStyle.Fill };
            root.Controls.Add(accountRow, 0, 0);
            accountRow.Controls.Add(new Label { Text = "Account", Width = 80, TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
            _accounts.Width = 360;
            _accounts.DropDownStyle = ComboBoxStyle.DropDownList;
            accountRow.Controls.Add(_accounts);
            accountRow.Controls.Add(new Label { Text = "Profile", Width = 60, TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
            _profiles.Width = 260;
            _profiles.DropDownStyle = ComboBoxStyle.DropDownList;
            accountRow.Controls.Add(_profiles);
            FlowLayoutPanel fileRow = new() { Dock = DockStyle.Fill };
            root.Controls.Add(fileRow, 0, 1);
            _filePath.Width = 600;
            fileRow.Controls.Add(_filePath);
            Button browse = new() { Text = "Browse", Width = 100 };
            browse.Click += (_, _) => Browse();
            fileRow.Controls.Add(browse);
            Button preview = new() { Text = "Preview", Width = 100 };
            preview.Click += async (_, _) => await PreviewAsync().ConfigureAwait(true);
            fileRow.Controls.Add(preview);
            Button import = new() { Text = "Import", Width = 100 };
            import.Click += async (_, _) => await ImportAsync().ConfigureAwait(true);
            fileRow.Controls.Add(import);
            Button repairDataFlow = new() { Text = "Repair Data Links", Width = 135 };
            repairDataFlow.Click += async (_, _) => await RepairDataFlowAsync().ConfigureAwait(true);
            fileRow.Controls.Add(repairDataFlow);
            _summary.Dock = DockStyle.Fill;
            _summary.Multiline = true;
            _summary.ReadOnly = true;
            root.Controls.Add(_summary, 0, 2);
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.ReadOnly = true;
            _grid.DataSource = _bindingSource;
            AddColumn("TransactionDate", "Date", 95);
            AddColumn("TransactionTime", "Time", 80);
            AddColumn("PostingDate", "Post Date", 95);
            AddColumn("Amount", "Amount", 100, "C2");
            AddColumn("Type", "Type", 130);
            AddColumn("Description", "Description", 320);
            AddColumn("FundingSource", "Funding Source", 190);
            AddColumn("ExternalTransactionId", "External ID", 140);
            AddColumn("SourceCategory", "Source Category", 150);
            AddColumn("Category", "Category", 160);
            AddColumn("SpendingClass", "Category Type", 140);
            AddColumn("Merchant", "Merchant", 160);
            AddColumn("IsIncome", "Income", 70);
            AddColumn("IsTransfer", "Transfer", 70);
            root.Controls.Add(_grid, 0, 3);
        }

        private void AddColumn(string property, string header, int width, string? format = null)
        {
            DataGridViewTextBoxColumn column = new()
            {
                DataPropertyName = property,
                HeaderText = header,
                Width = width
            };

            if (!string.IsNullOrWhiteSpace(format))
            {
                column.DefaultCellStyle.Format = format;
            }

            _grid.Columns.Add(column);
        }

        private async Task LoadAccountsAsync()
        {
            IReadOnlyList<Account> accounts = await _importWorkflowService.GetAccountsAsync().ConfigureAwait(true);
            _accounts.DataSource = accounts;
            _accounts.DisplayMember = nameof(Account.DisplayName);
            _accounts.ValueMember = nameof(Account.AccountId);
        }

        private void LoadProfiles()
        {
            IReadOnlyList<ImportProfileOption> profiles = _importWorkflowService.GetProfiles();
            _profiles.DataSource = profiles.ToList();
            _profiles.DisplayMember = nameof(ImportProfileOption.DisplayName);
            _profiles.ValueMember = nameof(ImportProfileOption.ProfileCode);
        }

        private void Browse()
        {
            ImportProfileOption? profile = _profiles.SelectedItem as ImportProfileOption;

            using OpenFileDialog dialog = new()
            {
                Filter = profile?.FileDialogFilter ?? "All files (*.*)|*.*",
                Title = "Select import file"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _filePath.Text = dialog.FileName;
            }
        }

        private async Task PreviewAsync()
        {
            if (!ValidateInputs(out int accountId, out string profileCode, out string path))
            {
                return;
            }

            ImportSummary summary = await _importWorkflowService.PreviewAsync(accountId, profileCode, path).ConfigureAwait(true);
            _bindingSource.DataSource = summary.PreviewRows;
            _summary.Text = $"Profile: {profileCode}. Preview rows: {summary.ParsedRows}. Parse errors: {summary.ErrorRows}.";

            if (summary.ParseErrors.Count > 0)
            {
                _summary.AppendText(Environment.NewLine + string.Join(Environment.NewLine, summary.ParseErrors.Select(error => $"Row {error.RowNumber}: {error.Message}")));
            }
        }

        private async Task ImportAsync()
        {
            if (!ValidateInputs(out int accountId, out string profileCode, out string path))
            {
                return;
            }

            ImportSummary summary = await _importWorkflowService.ImportAsync(accountId, profileCode, path).ConfigureAwait(true);

            _summary.Text = $"Profile: {profileCode}{Environment.NewLine}" +
                $"Batch: {summary.ImportBatchId}{Environment.NewLine}" +
                $"Parsed: {summary.ParsedRows}{Environment.NewLine}" +
                $"Inserted: {summary.InsertedRows}{Environment.NewLine}" +
                $"Duplicates total: {summary.DuplicateRows}{Environment.NewLine}" +
                $"Exact duplicates skipped: {summary.ExactDuplicateRows}{Environment.NewLine}" +
                $"Duplicates needing review: {summary.DuplicateReviewRows}{Environment.NewLine}" +
                $"Errors: {summary.ErrorRows}{Environment.NewLine}" +
                $"New PayPal funding links: {summary.LinkedFundingRows}{Environment.NewLine}" +
                $"Linked PayPal rows normalized: {summary.NormalizedLinkedFundingRows}{Environment.NewLine}" +
                $"Likely PayPal funding rows normalized: {summary.NormalizedLikelyPayPalFundingRows}{Environment.NewLine}" +
                $"Credit card funding links: {summary.CreditCardFundingLinks}{Environment.NewLine}" +
                $"Credit card payment allocations: {summary.CreditCardPaymentAllocations}{Environment.NewLine}" +
                $"Linked credit card payment rows normalized: {summary.NormalizedCreditCardPaymentRows}{Environment.NewLine}" +
                $"Likely credit card payment rows normalized: {summary.NormalizedLikelyCreditCardPaymentRows}{Environment.NewLine}" +
                $"Bank transfer links: {summary.BankTransferLinks}{Environment.NewLine}" +
                $"Linked bank transfer rows normalized: {summary.NormalizedBankTransferRows}{Environment.NewLine}" +
                $"Same file hash seen before: {summary.FileHashPreviouslyImported}";

            _bindingSource.DataSource = Array.Empty<ImportedTransaction>();
        }

        private async Task RepairDataFlowAsync()
        {
            try
            {
                DataFlowRepairSummary summary = await _importWorkflowService.RepairDataFlowAsync().ConfigureAwait(true);

                _summary.Text = $"Data-flow repair completed.{Environment.NewLine}" +
                    $"New PayPal funding links: {summary.LinkedFundingRows}{Environment.NewLine}" +
                    $"Linked PayPal rows normalized: {summary.NormalizedLinkedFundingRows}{Environment.NewLine}" +
                    $"Likely PayPal funding rows normalized: {summary.NormalizedLikelyPayPalFundingRows}{Environment.NewLine}" +
                    $"Credit card funding links: {summary.CreditCardFundingLinks}{Environment.NewLine}" +
                    $"Credit card payment allocations: {summary.CreditCardPaymentAllocations}{Environment.NewLine}" +
                    $"Linked credit card payment rows normalized: {summary.NormalizedCreditCardPaymentRows}{Environment.NewLine}" +
                    $"Likely credit card payment rows normalized: {summary.NormalizedLikelyCreditCardPaymentRows}{Environment.NewLine}" +
                    $"Bank transfer links: {summary.BankTransferLinks}{Environment.NewLine}" +
                    $"Linked bank transfer rows normalized: {summary.NormalizedBankTransferRows}{Environment.NewLine}" +
                    $"Potential exact duplicate groups already in the database: {summary.PotentialDuplicateGroups}{Environment.NewLine}" +
                    "Refresh Expense Analysis after this completes.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Data-flow repair failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ValidateInputs(out int accountId, out string profileCode, out string path)
        {
            accountId = 0;
            profileCode = string.Empty;
            path = _filePath.Text.Trim();

            if (_accounts.SelectedValue is not int selectedAccountId)
            {
                MessageBox.Show(this, "Select an account first.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (_profiles.SelectedValue is not string selectedProfileCode || string.IsNullOrWhiteSpace(selectedProfileCode))
            {
                MessageBox.Show(this, "Select an import profile first.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!File.Exists(path))
            {
                MessageBox.Show(this, "Select an import file first.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            accountId = selectedAccountId;
            profileCode = selectedProfileCode;
            return true;
        }
    }
}
