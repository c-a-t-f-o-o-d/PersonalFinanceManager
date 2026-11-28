using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class AccountsView : UserControl, IPageLayoutControl
    {
        private readonly IAccountWorkflowService _accountService;
        private readonly BindingSource _bindingSource = new();
        private readonly DataGridView _grid = new();

        public AccountsView(IAccountWorkflowService accountService)
        {
            _accountService = accountService;
            InitializeLayout();
        }

        public int PreferredPageHeight => 1050;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadAccountsAsync().ConfigureAwait(true);
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
            FlowLayoutPanel commands = new() { Dock = DockStyle.Fill };
            root.Controls.Add(commands, 0, 0);
            Button add = new() { Text = "Add", Width = 100 };
            add.Click += async (_, _) => await AddAsync().ConfigureAwait(true);
            commands.Controls.Add(add);
            Button edit = new() { Text = "Edit", Width = 100 };
            edit.Click += async (_, _) => await EditAsync().ConfigureAwait(true);
            commands.Controls.Add(edit);
            Button delete = new() { Text = "Delete", Width = 100 };
            delete.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);
            commands.Controls.Add(delete);
            Button refresh = new() { Text = "Refresh", Width = 100 };
            refresh.Click += async (_, _) => await LoadAccountsAsync().ConfigureAwait(true);
            commands.Controls.Add(refresh);
            Button reconcile = new() { Text = "Reconcile Statement", Width = 160 };

            reconcile.Click += (_, _) =>
            {
                if (_bindingSource.Current is Account account)
                {
                    using AccountReconciliationForm form = new(_accountService, account);
                    form.ShowDialog(this);
                }
            };

            commands.Controls.Add(reconcile);
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.ReadOnly = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.DataSource = _bindingSource;

            _grid.CellDoubleClick += async (_, args) =>
            {
                if (args.RowIndex >= 0)
                {
                    await EditAsync().ConfigureAwait(true);
                }
            };

            AddColumn("AccountNickname", "Nickname", 180);
            AddColumn("Institution", "Institution", 160);
            AddColumn("AccountType", "Type", 120);
            AddColumn("AccountNumberLastFour", "Last Four", 80);
            AddColumn("CurrentBalance", "Current", 100, "C2");
            AddColumn("AvailableBalance", "Available", 100, "C2");
            AddColumn("BalanceAsOfDate", "Balance As Of", 110);
            AddColumn("PrincipalBalance", "Principal", 100, "C2");
            AddColumn("CurrentInterestRate", "Rate", 80, "0.00000");
            AddColumn("NextPaymentDueDate", "Next Due", 100);
            AddColumn("NextPaymentAmount", "Next Payment", 110, "C2");
            root.Controls.Add(_grid, 0, 1);
            PageLayout.AutoSizeHeaders(root, 0);
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
            IReadOnlyList<Account> accounts = await _accountService.GetAccountsAsync().ConfigureAwait(true);
            _bindingSource.DataSource = accounts;
        }

        private async Task AddAsync()
        {
            using AccountEditForm form = new(new Account { AccountNickname = "New Account", Institution = string.Empty, AccountType = "Checking" });

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _accountService.SaveAccountAsync(form.Account).ConfigureAwait(true);
            await LoadAccountsAsync().ConfigureAwait(true);
        }

        private async Task EditAsync()
        {
            if (_bindingSource.Current is not Account selected)
            {
                return;
            }

            Account? full = await _accountService.GetAccountForEditAsync(selected.AccountId).ConfigureAwait(true);

            if (full is null)
            {
                return;
            }

            if (full.AccountNumberCannotBeDecrypted)
            {
                MessageBox.Show(
                    this,
                    full.AccountNumberProtectionMessage ?? "The saved account number could not be decrypted on this Windows profile. You can still edit the account. Leave the account number blank to preserve the existing encrypted value, or enter it again to replace it.",
                    "Account number protected on another machine",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            using AccountEditForm form = new(full);

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _accountService.SaveAccountAsync(form.Account).ConfigureAwait(true);
            await LoadAccountsAsync().ConfigureAwait(true);
        }

        private async Task DeleteAsync()
        {
            if (_bindingSource.Current is not Account selected)
            {
                return;
            }

            DialogResult confirm = MessageBox.Show(this, $"Delete account '{selected.DisplayName}'?", "Delete Account", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            await _accountService.DeleteAccountAsync(selected.AccountId).ConfigureAwait(true);
            await LoadAccountsAsync().ConfigureAwait(true);
        }
    }
}
