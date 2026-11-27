using PersonalFinanceManager.Application.Services.Contracts;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class AccountReconciliationForm : Form
    {
        private readonly IAccountWorkflowService _service;
        private readonly Account _account;
        private readonly DateTimePicker _fromDate = new() { Format = DateTimePickerFormat.Short };
        private readonly DateTimePicker _toDate = new() { Format = DateTimePickerFormat.Short };
        private readonly NumericUpDown _opening = MoneyInput();
        private readonly NumericUpDown _closing = MoneyInput();
        private readonly Label _result = new() { Dock = DockStyle.Fill, AutoSize = false };
        private readonly DataGridView _history = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false, AllowUserToAddRows = false };
        private readonly Button _check = new() { Text = "Check", Width = 100 };
        private readonly Button _record = new() { Text = "Check and Record", Width = 150 };

        public AccountReconciliationForm(IAccountWorkflowService service, Account account)
        {
            _service = service;
            _account = account;
            Text = $"Reconcile Statement — {account.DisplayName}";
            Width = 980;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);
            DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
            _fromDate.Value = month.AddMonths(-1);
            _toDate.Value = month.AddDays(-1);
            TableLayoutPanel layout = new() { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 8 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            string balanceDescription = AccountClassification.IsLiability(account.AccountType)
                ? "Enter balances owed as positive amounts. Purchases increase the balance; payments and refunds reduce it."
                : "Enter the statement's posted balance, including negative balances. Transfers remain part of each account's activity.";

            if (AccountClassification.IsWallet(account.AccountType))
            {
                balanceDescription += " A complete wallet balance ledger is required; PayPal purchase/account-activity imports cannot verify a wallet balance.";
            }

            Label help = new()
            {
                Text = balanceDescription + " Opening balance is before the first date; closing balance is at the end of the last date. Posting dates are used when available. Checking a statement does not alter transactions or saved account balances.",
                Dock = DockStyle.Fill
            };

            layout.Controls.Add(help, 0, 0);
            layout.SetColumnSpan(help, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            AddInput(layout, 1, "First statement date", _fromDate);
            AddInput(layout, 2, "Last statement date", _toDate);
            AddInput(layout, 3, "Opening statement balance", _opening);
            AddInput(layout, 4, "Closing statement balance", _closing);
            FlowLayoutPanel buttons = new() { Dock = DockStyle.Fill };
            buttons.Controls.Add(_check);
            buttons.Controls.Add(_record);
            _check.Click += async (_, _) => await CheckAsync(false);
            _record.Click += async (_, _) => await CheckAsync(true);
            layout.Controls.Add(buttons, 1, 5);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.Controls.Add(_result, 0, 6);
            layout.SetColumnSpan(_result, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            layout.Controls.Add(_history, 0, 7);
            layout.SetColumnSpan(_history, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            foreach ((string property, string header, string? format) in new (string, string, string?)[]
            {
                ("RecordedAtUtc", "Recorded (UTC)", "g"), ("FromDate", "From", null), ("ToDate", "Through", null),
                ("ClosingBalance", "Statement", "C2"), ("ExpectedClosingBalance", "Calculated", "C2"),
                ("Difference", "Difference", "C2"), ("TransactionCount", "Rows", null)
            })
            {
                _history.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = property,
                    HeaderText = header,
                    Width = property == "RecordedAtUtc" ? 160 : 110,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = format ?? string.Empty }
                });
            }
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                _history.DataSource = await _service.GetStatementHistoryAsync(_account.AccountId);
            }
            catch (Exception ex)
            {
                _result.Text = ex.Message;
            }
        }

        private async Task CheckAsync(bool record)
        {
            _check.Enabled = false;
            _record.Enabled = false;

            try
            {
                AccountReconciliation result = await _service.CheckStatementAsync(new AccountReconciliationRequest
                {
                    AccountId = _account.AccountId,
                    FromDate = DateOnly.FromDateTime(_fromDate.Value),
                    ToDate = DateOnly.FromDateTime(_toDate.Value),
                    OpeningBalance = _opening.Value,
                    ClosingBalance = _closing.Value
                }, record);

                _result.ForeColor = result.IsBalanced ? Color.DarkGreen : Color.Firebrick;
                _result.Text = $"{(result.IsBalanced ? "Balances match" : "Balance difference found")}. Calculated closing balance: {result.ExpectedClosingBalance:C2}; statement: {result.ClosingBalance:C2}; difference: {result.Difference:C2}.\n"
                    + $"Included {result.TransactionCount} account entries; {result.DateFallbackCount} use transaction date because no posting date was imported. "
                    + "A matching balance does not rule out offsetting missing entries. Recorded checks reflect the ledger at the time of the check.";

                if (record)
                {
                    _history.DataSource = await _service.GetStatementHistoryAsync(_account.AccountId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Statement Reconciliation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _check.Enabled = true;
                _record.Enabled = true;
            }
        }

        private static NumericUpDown MoneyInput() => new() { DecimalPlaces = 2, Minimum = -1000000000000M, Maximum = 1000000000000M, ThousandsSeparator = true, Width = 220 };

        private static void AddInput(TableLayoutPanel layout, int row, string caption, Control input)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            layout.Controls.Add(input, 1, row);
        }
    }
}
