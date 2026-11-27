using System.Text;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class DuplicateReviewView : UserControl, IPageLayoutControl
    {
        private readonly IDuplicateReviewWorkflowService _duplicateReviewWorkflowService;
        private readonly BindingSource _bindingSource = new();
        private readonly ComboBox _accountFilter = new();
        private readonly ComboBox _statusFilter = new();
        private readonly TextBox _search = new();
        private readonly NumericUpDown _maxRows = new();
        private readonly DataGridView _grid = new();
        private readonly TextBox _candidateDetails = new();
        private readonly TextBox _existingDetails = new();
        private readonly TextBox _reasonDetails = new();
        private IReadOnlyList<Account> _accounts = Array.Empty<Account>();
        private bool _resolvingCandidate;

        public DuplicateReviewView(IDuplicateReviewWorkflowService duplicateReviewService)
        {
            _duplicateReviewWorkflowService = duplicateReviewService ?? throw new ArgumentNullException(nameof(duplicateReviewService));
            InitializeLayout();
        }

        public int PreferredPageHeight => 1120;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadAccountsAsync().ConfigureAwait(true);
            LoadStatuses();
            await LoadDuplicatesAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                BackColor = UiTheme.AppBackground
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 164));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 380));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 310));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            Panel filterCard = CreateCard();
            filterCard.Dock = DockStyle.Fill;
            filterCard.Padding = new Padding(16);
            root.Controls.Add(filterCard, 0, 0);

            TableLayoutPanel filters = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 3
            };

            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            filterCard.Controls.Add(filters);
            filters.Controls.Add(MakeLabel("Account"), 0, 0);
            _accountFilter.Dock = DockStyle.Fill;
            _accountFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            filters.Controls.Add(_accountFilter, 1, 0);
            filters.Controls.Add(MakeLabel("Status"), 2, 0);
            _statusFilter.Dock = DockStyle.Fill;
            _statusFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            filters.Controls.Add(_statusFilter, 3, 0);
            filters.Controls.Add(MakeLabel("Search"), 4, 0);
            _search.Dock = DockStyle.Fill;
            filters.Controls.Add(_search, 5, 0);
            filters.Controls.Add(MakeLabel("Max"), 6, 0);
            _maxRows.Dock = DockStyle.Fill;
            _maxRows.Minimum = 1;
            _maxRows.Maximum = 5000;
            _maxRows.Value = 500;
            filters.Controls.Add(_maxRows, 7, 0);

            Label help = new()
            {
                Text = "Review overlapping imports. Exact-skipped rows can be recovered. Rows from different providers may represent a purchase and its funding; review Transaction Links before deleting either.",
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            filters.SetColumnSpan(help, 8);
            filters.Controls.Add(help, 0, 1);

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            filters.SetColumnSpan(buttons, 8);
            filters.Controls.Add(buttons, 0, 2);
            Button refresh = MakeButton("Refresh", 96);
            refresh.Click += async (_, _) => await LoadDuplicatesAsync().ConfigureAwait(true);
            buttons.Controls.Add(refresh);
            Button scan = MakeButton("Scan Cross-Account", 160);
            scan.Click += async (_, _) => await ScanCrossAccountAsync().ConfigureAwait(true);
            buttons.Controls.Add(scan);
            Button approve = MakeButton("Accept / Add", 120);
            approve.Click += async (_, _) => await ApproveSelectedAsync().ConfigureAwait(true);
            buttons.Controls.Add(approve);
            Button ignore = MakeButton("Ignore / Delete", 128);
            ignore.Click += async (_, _) => await IgnoreSelectedAsync().ConfigureAwait(true);
            buttons.Controls.Add(ignore);
            Button keepBoth = MakeButton("Keep Both", 104);
            keepBoth.Click += async (_, _) => await KeepBothSelectedAsync().ConfigureAwait(true);
            buttons.Controls.Add(keepBoth);
            Button deleteCandidate = MakeButton("Delete Left", 104);
            deleteCandidate.Click += async (_, _) => await DeleteCandidateSelectedAsync().ConfigureAwait(true);
            buttons.Controls.Add(deleteCandidate);
            Button deleteExisting = MakeButton("Delete Right", 108);
            deleteExisting.Click += async (_, _) => await DeleteExistingSelectedAsync().ConfigureAwait(true);
            buttons.Controls.Add(deleteExisting);
            Panel gridCard = CreateCard();
            gridCard.Dock = DockStyle.Fill;
            gridCard.Padding = new Padding(12);
            root.Controls.Add(gridCard, 0, 1);
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.ReadOnly = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.DataSource = _bindingSource;
            _grid.SelectionChanged += (_, _) => ShowSelectedDetails();
            AddColumn("ReviewKind", "Kind", 115);
            AddColumn("Status", "Status", 150);
            AddColumn("CandidateDisplayAccount", "Left / Candidate Account", 180);
            AddColumn("ExistingDisplayAccount", "Right / Existing Account", 180);
            AddColumn("CandidateDate", "Date", 95);
            AddColumn("CandidateAmount", "Amount", 95, "C2");
            AddColumn("CandidateType", "Type", 110);
            AddColumn("CandidateDescription", "Candidate Description", 280);
            AddColumn("ExistingDescription", "Existing Description", 280);
            AddColumn("DuplicateReason", "Reason", 360);
            gridCard.Controls.Add(_grid);

            TableLayoutPanel detailCards = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };

            detailCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            detailCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            detailCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            root.Controls.Add(detailCards, 0, 2);
            detailCards.Controls.Add(BuildDetailsCard("Left / candidate", _candidateDetails), 0, 0);
            detailCards.Controls.Add(BuildDetailsCard("Right / existing match", _existingDetails), 1, 0);
            detailCards.Controls.Add(BuildDetailsCard("Review guidance", _reasonDetails), 2, 0);
        }

        private static CardPanel CreateCard()
        {
            return new CardPanel
            {
                BackColor = UiTheme.CardBackground,
                Margin = new Padding(0, 0, 0, 12)
            };
        }

        private static Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary
            };
        }

        private static Button MakeButton(string text, int width)
        {
            Button button = new()
            {
                Text = text,
                Width = width,
                Height = 34,
                Margin = new Padding(0, 4, 8, 4)
            };

            UiTheme.StyleSecondaryButton(button);
            return button;
        }

        private static Control BuildDetailsCard(string title, TextBox textBox)
        {
            Panel card = CreateCard();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(12);

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);

            Label label = new()
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = UiTheme.SectionTitleFont,
                ForeColor = UiTheme.TextPrimary
            };

            layout.Controls.Add(label, 0, 0);
            textBox.Dock = DockStyle.Fill;
            textBox.Multiline = true;
            textBox.ReadOnly = true;
            textBox.ScrollBars = ScrollBars.Vertical;
            textBox.BorderStyle = BorderStyle.None;
            textBox.BackColor = Color.White;
            layout.Controls.Add(textBox, 0, 1);
            return card;
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
            _accounts = await _duplicateReviewWorkflowService.GetAccountsAsync().ConfigureAwait(true);
            List<AccountOption> options = new() { new AccountOption(null, "All Accounts") };
            options.AddRange(_accounts.Select(account => new AccountOption(account.AccountId, account.DisplayName)));
            _accountFilter.DataSource = options;
            _accountFilter.DisplayMember = nameof(AccountOption.DisplayName);
            _accountFilter.ValueMember = nameof(AccountOption.AccountId);
        }

        private void LoadStatuses()
        {
            _statusFilter.Items.Clear();
            _statusFilter.Items.Add(new StatusOption(DuplicateReviewStatuses.NeedsReview, "Needs Review"));
            _statusFilter.Items.Add(new StatusOption(null, "All Duplicate Rows"));
            _statusFilter.Items.Add(new StatusOption(DuplicateReviewStatuses.ImportDuplicateReview, "Import Review Only"));
            _statusFilter.Items.Add(new StatusOption(DuplicateReviewStatuses.CrossAccountReview, "Cross-Account Review Only"));
            _statusFilter.Items.Add(new StatusOption(DuplicateReviewStatuses.ExactDuplicateSkipped, "Exact Skipped"));
            _statusFilter.Items.Add(new StatusOption(DuplicateReviewStatuses.DuplicateAccepted, "Import Accepted"));
            _statusFilter.Items.Add(new StatusOption(DuplicateReviewStatuses.DuplicateIgnored, "Import Ignored"));
            _statusFilter.Items.Add(new StatusOption(DuplicateReviewStatuses.CrossAccountKept, "Cross-Account Kept"));
            _statusFilter.Items.Add(new StatusOption(DuplicateReviewStatuses.CrossAccountCandidateDeleted, "Left Deleted"));
            _statusFilter.Items.Add(new StatusOption(DuplicateReviewStatuses.CrossAccountExistingDeleted, "Right Deleted"));
            _statusFilter.DisplayMember = nameof(StatusOption.DisplayName);
            _statusFilter.ValueMember = nameof(StatusOption.Status);
            _statusFilter.SelectedIndex = 0;
        }

        private async Task LoadDuplicatesAsync()
        {
            DuplicateReviewFilter filter = new()
            {
                AccountId = (_accountFilter.SelectedItem as AccountOption)?.AccountId,
                Status = (_statusFilter.SelectedItem as StatusOption)?.Status,
                SearchText = _search.Text,
                MaxRows = decimal.ToInt32(_maxRows.Value)
            };

            IReadOnlyList<DuplicateReviewItem> items = await _duplicateReviewWorkflowService.GetAsync(filter).ConfigureAwait(true);
            _bindingSource.DataSource = items;
            ShowSelectedDetails();
        }

        private async Task ScanCrossAccountAsync()
        {
            int created = await _duplicateReviewWorkflowService.ScanCrossAccountDuplicatesAsync().ConfigureAwait(true);
            MessageBox.Show(this, $"Added {created} cross-account duplicate review item(s).", "Duplicate Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadDuplicatesAsync().ConfigureAwait(true);
        }

        private async Task ApproveSelectedAsync()
        {
            if (_resolvingCandidate)
            {
                return;
            }

            DuplicateReviewItem? item = GetSelectedItem();

            if (item is null)
            {
                return;
            }

            if (item.IsCrossAccountReview)
            {
                MessageBox.Show(this, "Cross-account rows are already in Transactions. Use Keep Both, Delete Left, or Delete Right.", "Duplicate Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!string.Equals(item.Status, DuplicateReviewStatuses.ImportDuplicateReview, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Status, DuplicateReviewStatuses.ExactDuplicateSkipped, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Only Needs Review or Exact Skipped import rows can be accepted.", "Duplicate Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string prompt = string.Equals(item.Status, DuplicateReviewStatuses.ExactDuplicateSkipped, StringComparison.OrdinalIgnoreCase)
                ? "Recover this exact-skipped row as a separate transaction? Confirm it is an additional real purchase or payment, not the same event imported twice."
                : "Add this duplicate candidate as a real transaction?";
            DialogResult result = MessageBox.Show(this, prompt, "Accept duplicate", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            _resolvingCandidate = true;
            try
            {
                await _duplicateReviewWorkflowService.ApproveImportCandidateAsync(item.ImportRowId).ConfigureAwait(true);
                await LoadDuplicatesAsync().ConfigureAwait(true);
            }
            finally
            {
                _resolvingCandidate = false;
            }
        }

        private async Task IgnoreSelectedAsync()
        {
            if (_resolvingCandidate)
            {
                return;
            }

            DuplicateReviewItem? item = GetSelectedItem();

            if (item is null)
            {
                return;
            }

            if (item.IsCrossAccountReview)
            {
                MessageBox.Show(this, "Cross-account rows are already in Transactions. Use Keep Both, Delete Left, or Delete Right.", "Duplicate Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!string.Equals(item.Status, DuplicateReviewStatuses.ImportDuplicateReview, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Status, DuplicateReviewStatuses.ExactDuplicateSkipped, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Only Needs Review or Exact Skipped import rows can be ignored.", "Duplicate Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox.Show(this, "Ignore this candidate and leave it out of transactions?", "Ignore duplicate", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            _resolvingCandidate = true;
            try
            {
                await _duplicateReviewWorkflowService.IgnoreImportCandidateAsync(item.ImportRowId).ConfigureAwait(true);
                await LoadDuplicatesAsync().ConfigureAwait(true);
            }
            finally
            {
                _resolvingCandidate = false;
            }
        }

        private async Task KeepBothSelectedAsync()
        {
            DuplicateReviewItem? item = RequirePendingCrossAccountSelection();

            if (item is null || !item.CrossAccountDuplicateReviewId.HasValue)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, "Mark this cross-account pair as legitimate and keep both transactions?", "Keep both", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            await _duplicateReviewWorkflowService.KeepCrossAccountAsync(item.CrossAccountDuplicateReviewId.Value).ConfigureAwait(true);
            await LoadDuplicatesAsync().ConfigureAwait(true);
        }

        private async Task DeleteCandidateSelectedAsync()
        {
            DuplicateReviewItem? item = RequirePendingCrossAccountSelection();

            if (item is null || !item.CrossAccountDuplicateReviewId.HasValue)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, $"Delete the left/candidate transaction from {item.CandidateDisplayAccount}?", "Delete left transaction", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                return;
            }

            await _duplicateReviewWorkflowService.DeleteCrossAccountCandidateAsync(item.CrossAccountDuplicateReviewId.Value).ConfigureAwait(true);
            await LoadDuplicatesAsync().ConfigureAwait(true);
        }

        private async Task DeleteExistingSelectedAsync()
        {
            DuplicateReviewItem? item = RequirePendingCrossAccountSelection();

            if (item is null || !item.CrossAccountDuplicateReviewId.HasValue)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, $"Delete the right/existing transaction from {item.ExistingDisplayAccount}?", "Delete right transaction", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                return;
            }

            await _duplicateReviewWorkflowService.DeleteCrossAccountExistingAsync(item.CrossAccountDuplicateReviewId.Value).ConfigureAwait(true);
            await LoadDuplicatesAsync().ConfigureAwait(true);
        }

        private DuplicateReviewItem? RequirePendingCrossAccountSelection()
        {
            DuplicateReviewItem? item = GetSelectedItem();

            if (item is null)
            {
                return null;
            }

            if (!item.IsCrossAccountReview || !string.Equals(item.Status, DuplicateReviewStatuses.CrossAccountReview, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Select a pending cross-account review row first.", "Duplicate Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            return item;
        }

        private DuplicateReviewItem? GetSelectedItem()
        {
            return _bindingSource.Current as DuplicateReviewItem;
        }

        private void ShowSelectedDetails()
        {
            DuplicateReviewItem? item = GetSelectedItem();

            if (item is null)
            {
                _candidateDetails.Text = string.Empty;
                _existingDetails.Text = string.Empty;
                _reasonDetails.Text = string.Empty;
                return;
            }

            _candidateDetails.Text = BuildCandidateDetails(item);
            _existingDetails.Text = BuildExistingDetails(item);
            _reasonDetails.Text = BuildReasonDetails(item);
        }

        private static string BuildCandidateDetails(DuplicateReviewItem item)
        {
            StringBuilder builder = new();
            Append(builder, "Review Kind", item.ReviewKind);
            Append(builder, "Account", item.CandidateDisplayAccount);
            Append(builder, "Transaction ID", item.CandidateTransactionId);
            Append(builder, "Import Row", item.ImportRowId == 0 ? null : item.ImportRowId);
            Append(builder, "Row", item.RowNumber == 0 ? null : item.RowNumber);
            Append(builder, "Date", item.CandidateDate);
            Append(builder, "Posting", item.CandidatePostingDate);
            Append(builder, "Time", item.CandidateTime);
            Append(builder, "Amount", item.CandidateAmount?.ToString("C2"));
            Append(builder, "Type", item.CandidateType);
            Append(builder, "Description", item.CandidateDescription);
            Append(builder, "External ID", item.CandidateExternalTransactionId);
            Append(builder, "Funding", item.CandidateFundingSource);
            Append(builder, "Source Category", item.CandidateSourceCategory);
            Append(builder, "Category", item.CandidateCategory);
            Append(builder, "Category Type", item.CandidateCategoryType);
            Append(builder, "Income", item.CandidateIsIncome);
            Append(builder, "Transfer", item.CandidateIsTransfer);
            Append(builder, "Memo", item.CandidateMemo);
            return builder.ToString();
        }

        private static string BuildExistingDetails(DuplicateReviewItem item)
        {
            if (!item.ExistingTransactionId.HasValue)
            {
                return "No existing transaction was captured for this duplicate row.";
            }

            StringBuilder builder = new();
            Append(builder, "Account", item.ExistingDisplayAccount);
            Append(builder, "Transaction ID", item.ExistingTransactionId);
            Append(builder, "Date", item.ExistingDate);
            Append(builder, "Posting", item.ExistingPostingDate);
            Append(builder, "Time", item.ExistingTime);
            Append(builder, "Amount", item.ExistingAmount?.ToString("C2"));
            Append(builder, "Type", item.ExistingType);
            Append(builder, "Description", item.ExistingDescription);
            Append(builder, "External ID", item.ExistingExternalTransactionId);
            Append(builder, "Funding", item.ExistingFundingSource);
            Append(builder, "Source Category", item.ExistingSourceCategory);
            Append(builder, "Category", item.ExistingCategory);
            Append(builder, "Category Type", item.ExistingCategoryType);
            Append(builder, "Income", item.ExistingIsIncome);
            Append(builder, "Transfer", item.ExistingIsTransfer);
            Append(builder, "Memo", item.ExistingMemo);
            return builder.ToString();
        }

        private static string BuildReasonDetails(DuplicateReviewItem item)
        {
            StringBuilder builder = new();
            Append(builder, "Status", item.Status);
            Append(builder, "Confidence", item.DuplicateConfidence);
            Append(builder, "Reason", item.DuplicateReason);
            Append(builder, "Profile", item.ImportProfileCode);
            Append(builder, "File", item.OriginalFileName);
            builder.AppendLine();

            if (item.IsCrossAccountReview)
            {
                builder.AppendLine("Cross-account review rows are already in Transactions.");
                builder.AppendLine("A wallet purchase and its bank or card funding may both be valid records. Use Transaction Links to reconcile their economic effect before considering deletion.");
                builder.AppendLine("Keep Both marks the pair legitimate and stops showing it as pending.");
                builder.AppendLine("Delete Left removes the left/candidate transaction.");
                builder.AppendLine("Delete Right removes the right/existing transaction.");
            }
            else
            {
                builder.AppendLine("Accept / Add inserts the candidate into Transactions.");
                builder.AppendLine("Ignore / Delete leaves the candidate out of Transactions and removes it from pending review.");
                builder.AppendLine("Exact Skipped rows can also be accepted when identical source fields represent a separate real event.");
            }

            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string label, object? value)
        {
            builder.Append(label);
            builder.Append(": ");
            builder.AppendLine(value?.ToString() ?? string.Empty);
        }

        private sealed record AccountOption(int? AccountId, string DisplayName);
        private sealed record StatusOption(string? Status, string DisplayName);
    }
}
