using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class RecurringBillsView : UserControl, IPageLayoutControl
    {
        private readonly IRecurringBillWorkflowService _billService;
        private readonly BindingSource _billSource = new();
        private readonly BindingSource _candidateSource = new();
        private readonly DataGridView _billGrid = new();
        private readonly DataGridView _candidateGrid = new();
        private readonly NumericUpDown _minimumTransactions = new();
        private readonly NumericUpDown _minimumMonths = new();
        private IReadOnlyList<Account> _accounts = Array.Empty<Account>();
        private IReadOnlyList<string> _categories = Array.Empty<string>();
        private IReadOnlyList<string> _categoryTypes = Array.Empty<string>();
        private IReadOnlyList<string> _requirementTypes = Array.Empty<string>();

        public RecurringBillsView(IRecurringBillWorkflowService billService)
        {
            _billService = billService;
            InitializeLayout();
        }

        public int PreferredPageHeight => 1100;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadLookupValuesAsync().ConfigureAwait(true);
            await RefreshBillsAsync().ConfigureAwait(true);
            await DetectCandidatesAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 4
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            Controls.Add(root);
            FlowLayoutPanel billCommands = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            root.Controls.Add(billCommands, 0, 0);
            Button add = new() { Text = "Add Bill", Width = 100 };
            add.Click += async (_, _) => await AddBillAsync().ConfigureAwait(true);
            billCommands.Controls.Add(add);
            Button edit = new() { Text = "Edit Bill", Width = 100 };
            edit.Click += async (_, _) => await EditBillAsync().ConfigureAwait(true);
            billCommands.Controls.Add(edit);
            Button delete = new() { Text = "Delete Bill", Width = 100 };
            delete.Click += async (_, _) => await DeleteBillAsync().ConfigureAwait(true);
            billCommands.Controls.Add(delete);
            Button refresh = new() { Text = "Refresh", Width = 100 };
            refresh.Click += async (_, _) => await RefreshAllAsync().ConfigureAwait(true);
            billCommands.Controls.Add(refresh);

            Label help = new()
            {
                Text = "Required recurring bills are used later for planning. Candidates are inferred from repeated categorized expenses.",
                AutoSize = true,
                Margin = new Padding(16, 10, 3, 3)
            };

            billCommands.Controls.Add(help);
            ConfigureBillGrid();
            root.Controls.Add(_billGrid, 0, 1);
            FlowLayoutPanel candidateCommands = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            root.Controls.Add(candidateCommands, 0, 2);
            candidateCommands.Controls.Add(MakeLabel("Min transactions", 110));
            _minimumTransactions.Minimum = 2;
            _minimumTransactions.Maximum = 24;
            _minimumTransactions.Value = 2;
            _minimumTransactions.Width = 60;
            candidateCommands.Controls.Add(_minimumTransactions);
            candidateCommands.Controls.Add(MakeLabel("Min months", 80));
            _minimumMonths.Minimum = 2;
            _minimumMonths.Maximum = 24;
            _minimumMonths.Value = 2;
            _minimumMonths.Width = 60;
            candidateCommands.Controls.Add(_minimumMonths);
            Button detect = new() { Text = "Detect Candidates", Width = 140 };
            detect.Click += async (_, _) => await DetectCandidatesAsync().ConfigureAwait(true);
            candidateCommands.Controls.Add(detect);
            Button create = new() { Text = "Create Bill From Candidate", Width = 190 };
            create.Click += async (_, _) => await CreateBillFromCandidateAsync().ConfigureAwait(true);
            candidateCommands.Controls.Add(create);

            Label candidateHelp = new()
            {
                Text = "Review candidates first. Dining/shopping patterns may be wants, not required bills.",
                AutoSize = true,
                Margin = new Padding(16, 10, 3, 3)
            };

            candidateCommands.Controls.Add(candidateHelp);
            ConfigureCandidateGrid();
            root.Controls.Add(_candidateGrid, 0, 3);
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

        private void ConfigureBillGrid()
        {
            _billGrid.Dock = DockStyle.Fill;
            _billGrid.AutoGenerateColumns = false;
            _billGrid.AllowUserToAddRows = false;
            _billGrid.ReadOnly = true;
            _billGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _billGrid.MultiSelect = false;
            _billGrid.DataSource = _billSource;
            _billGrid.CellDoubleClick += async (_, _) => await EditBillAsync().ConfigureAwait(true);
            AddBillColumn("BillName", "Bill", 180);
            AddBillColumn("Payee", "Payee", 220);
            AddBillColumn("AccountNickname", "Account", 150);
            AddBillColumn("TypicalAmount", "Typical", 95, "C2");
            AddBillColumn("DueDay", "Due Day", 75);
            AddBillColumn("NextDueDate", "Next Due", 95);
            AddBillColumn("Frequency", "Frequency", 90);
            AddBillColumn("Category", "Category", 140);
            AddBillColumn("SpendingClass", "Category Type", 140);
            AddBillColumn("RequirementType", "Need/Want/etc.", 120);
            AddBillColumn("IsRequired", "Required", 80);
            AddBillColumn("IsActive", "Active", 70);
            AddBillColumn("Notes", "Notes", 240);
        }

        private void ConfigureCandidateGrid()
        {
            _candidateGrid.Dock = DockStyle.Fill;
            _candidateGrid.AutoGenerateColumns = false;
            _candidateGrid.AllowUserToAddRows = false;
            _candidateGrid.ReadOnly = true;
            _candidateGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _candidateGrid.MultiSelect = false;
            _candidateGrid.DataSource = _candidateSource;
            AddCandidateColumn("Payee", "Candidate Payee", 250);
            AddCandidateColumn("AccountNickname", "Account", 150);
            AddCandidateColumn("AverageAmount", "Average", 95, "C2");
            AddCandidateColumn("MinimumAmount", "Min", 85, "C2");
            AddCandidateColumn("MaximumAmount", "Max", 85, "C2");
            AddCandidateColumn("TransactionCount", "Count", 70);
            AddCandidateColumn("MonthCount", "Months", 70);
            AddCandidateColumn("SuggestedDueDay", "Due Day", 75);
            AddCandidateColumn("Category", "Category", 130);
            AddCandidateColumn("SpendingClass", "Category Type", 130);
            AddCandidateColumn("SuggestedRequirementType", "Need/Want/etc.", 120);
            AddCandidateColumn("FirstSeenDate", "First Seen", 95);
            AddCandidateColumn("LastSeenDate", "Last Seen", 95);
            AddCandidateColumn("AlreadyExists", "Exists", 70);
            AddCandidateColumn("SourceDescription", "Source Description", 340);
        }

        private void AddBillColumn(string property, string header, int width, string? format = null)
        {
            _billGrid.Columns.Add(MakeTextColumn(property, header, width, format));
        }

        private void AddCandidateColumn(string property, string header, int width, string? format = null)
        {
            _candidateGrid.Columns.Add(MakeTextColumn(property, header, width, format));
        }

        private static DataGridViewTextBoxColumn MakeTextColumn(string property, string header, int width, string? format = null)
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

            return column;
        }

        private async Task LoadLookupValuesAsync()
        {
            _accounts = await _billService.GetAccountsAsync().ConfigureAwait(true);
            _categories = await _billService.GetCategoryValuesAsync().ConfigureAwait(true);
            _categoryTypes = await _billService.GetCategoryTypeValuesAsync().ConfigureAwait(true);
            _requirementTypes = await _billService.GetRequirementTypesAsync().ConfigureAwait(true);
        }

        private async Task RefreshAllAsync()
        {
            await LoadLookupValuesAsync().ConfigureAwait(true);
            await RefreshBillsAsync().ConfigureAwait(true);
            await DetectCandidatesAsync().ConfigureAwait(true);
        }

        private async Task RefreshBillsAsync()
        {
            _billSource.DataSource = await _billService.GetBillsAsync().ConfigureAwait(true);
        }

        private async Task DetectCandidatesAsync()
        {
            int minTransactions = decimal.ToInt32(_minimumTransactions.Value);
            int minMonths = decimal.ToInt32(_minimumMonths.Value);
            IReadOnlyList<RecurringBillCandidate> candidates = await _billService.GetMonthlyCandidatesAsync(minTransactions, minMonths).ConfigureAwait(true);
            _candidateSource.DataSource = candidates;
        }

        private RecurringBill? SelectedBill()
        {
            return _billGrid.CurrentRow?.DataBoundItem as RecurringBill;
        }

        private RecurringBillCandidate? SelectedCandidate()
        {
            return _candidateGrid.CurrentRow?.DataBoundItem as RecurringBillCandidate;
        }

        private async Task AddBillAsync()
        {
            RecurringBill bill = new()
            {
                RequirementType = BillRequirementTypes.Need,
                Frequency = BillFrequencies.Monthly,
                IsRequired = true,
                IsActive = true
            };

            using RecurringBillEditForm form = new(bill, _accounts, _categories, _categoryTypes, _requirementTypes);

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                await _billService.SaveBillAsync(bill).ConfigureAwait(true);
                await RefreshBillsAsync().ConfigureAwait(true);
            }
        }

        private async Task EditBillAsync()
        {
            RecurringBill? selected = SelectedBill();

            if (selected is null)
            {
                return;
            }

            RecurringBill? bill = await _billService.GetBillAsync(selected.RecurringBillId).ConfigureAwait(true);

            if (bill is null)
            {
                MessageBox.Show(this, "The selected bill no longer exists.", "Recurring Bills", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshBillsAsync().ConfigureAwait(true);
                return;
            }

            using RecurringBillEditForm form = new(bill, _accounts, _categories, _categoryTypes, _requirementTypes);

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                await _billService.SaveBillAsync(bill).ConfigureAwait(true);
                await RefreshBillsAsync().ConfigureAwait(true);
            }
        }

        private async Task DeleteBillAsync()
        {
            RecurringBill? selected = SelectedBill();

            if (selected is null)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, $"Delete recurring bill '{selected.BillName}'?", "Recurring Bills", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                return;
            }

            await _billService.DeleteBillAsync(selected.RecurringBillId).ConfigureAwait(true);
            await RefreshBillsAsync().ConfigureAwait(true);
        }

        private async Task CreateBillFromCandidateAsync()
        {
            RecurringBillCandidate? candidate = SelectedCandidate();

            if (candidate is null)
            {
                return;
            }

            RecurringBill bill = new()
            {
                AccountId = candidate.AccountId,
                BillName = candidate.Payee.Length > 200 ? candidate.Payee[..200] : candidate.Payee,
                Payee = candidate.Payee,
                Category = candidate.Category,
                SpendingClass = candidate.SpendingClass,
                RequirementType = candidate.SuggestedRequirementType,
                TypicalAmount = candidate.AverageAmount,
                DueDay = candidate.SuggestedDueDay,
                NextDueDate = NextMonthlyDate(candidate.SuggestedDueDay),
                Frequency = BillFrequencies.Monthly,
                IsRequired = !string.Equals(candidate.SuggestedRequirementType, BillRequirementTypes.Want, StringComparison.OrdinalIgnoreCase),
                IsActive = true,
                SourceDescription = candidate.SourceDescription,
                FirstSeenDate = candidate.FirstSeenDate,
                LastSeenDate = candidate.LastSeenDate,
                TransactionCount = candidate.TransactionCount,
                Notes = "Created from recurring transaction candidate."
            };

            using RecurringBillEditForm form = new(bill, _accounts, _categories, _categoryTypes, _requirementTypes);

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                await _billService.SaveBillAsync(bill).ConfigureAwait(true);
                await RefreshBillsAsync().ConfigureAwait(true);
                await DetectCandidatesAsync().ConfigureAwait(true);
            }
        }

        private static DateOnly NextMonthlyDate(int dueDay)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);
            int validDay = Math.Clamp(dueDay, 1, DateTime.DaysInMonth(today.Year, today.Month));
            DateOnly candidate = new(today.Year, today.Month, validDay);

            if (candidate < today)
            {
                DateOnly nextMonth = today.AddMonths(1);
                int nextDay = Math.Clamp(dueDay, 1, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                return new DateOnly(nextMonth.Year, nextMonth.Month, nextDay);
            }

            return candidate;
        }
    }
}
