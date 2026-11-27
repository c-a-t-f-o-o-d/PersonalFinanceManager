using System.Globalization;
using PersonalFinanceManager.Core.Common;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class AccountEditForm : Form
    {
        private readonly Dictionary<string, Control> _fields = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Field> _definitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly ToolTip _toolTip = new();

        public AccountEditForm(Account account)
        {
            Account = account;
            InitializeLayout();
            LoadAccount();
        }

        public Account Account { get; }

        private void InitializeLayout()
        {
            Text = "Account";
            Width = 760;
            Height = 720;
            StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2
            };

            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);
            TabControl tabs = new() { Dock = DockStyle.Fill };
            root.Controls.Add(tabs, 0, 0);

            AddTab(tabs, "General", new[]
            {
                Field.Text("AccountNickname", "Account Nickname"),
                Field.Text("Institution", "Institution"),
                Field.Text("BaseUrl", "Base URL"),
                Field.Text("AccountSummaryUrl", "Account Summary URL"),
                Field.Combo("AccountType", "Account Type", new[] { "Checking", "Savings", "Cash", "Money Market", "PayPal", "Apple Cash", "Credit Card", "PayPal Smart Connect", "PayPal Credit", "Mortgage", "Vehicle Loan", "Personal Loan", "Investment", "401k", "IRA", "Other" }),
                Field.Combo("AccountOwner", "Account Owner", OwnerValues.All),
                Field.Text("AccountNumber", "Account Number")
            });

            AddTab(tabs, "Balances & Rates", new[]
            {
                Field.Decimal("PrincipalBalance", "Principal Balance"),
                Field.Decimal("AvailableBalance", "Available Balance"),
                Field.Decimal("CurrentBalance", "Current Balance"),
                Field.Date("BalanceAsOfDate", "Balance As Of (yyyy-MM-dd)"),
                Field.Decimal("OriginalLoanAmount", "Original Loan Amount"),
                Field.Decimal("CurrentInterestRate", "Current Interest Rate"),
                Field.Decimal("PaidInFullDiscount", "Paid In Full Discount")
            });

            AddTab(tabs, "Payments", new[]
            {
                Field.Decimal("LastPaymentAmount", "Last Payment Amount"),
                Field.Decimal("NextPaymentAmount", "Next Payment Amount"),
                Field.Date("NextPaymentDueDate", "Next Payment Due Date"),
                Field.Decimal("NextPaymentInterest", "Next Payment Interest"),
                Field.Decimal("LastPaymentInterestAmount", "Last Payment Interest Amount"),
                Field.Decimal("RegularMonthlyPayment", "Regular Monthly Payment"),
                Field.Decimal("LatePaymentFee", "Late Payment Fee"),
                Field.Int("LateAfterDays", "Late After Days"),
                Field.Date("LastPaymentDateReceived", "Last Payment Date Received"),
                Field.Text("Recurrence", "Recurrence")
            });

            AddTab(tabs, "Escrow & Fees", new[]
            {
                Field.Decimal("NextPaymentEscrowAmount", "Next Payment Escrow Amount"),
                Field.Date("LastEscrowAnalysisDate", "Last Escrow Analysis Date"),
                Field.Decimal("CurrentEscrowAccountBalance", "Current Escrow Account Balance"),
                Field.Decimal("CurrentEscrowPayment", "Current Escrow Payment"),
                Field.Decimal("ProjectedEscrowPayment", "Projected Escrow Payment"),
                Field.Date("ProjectedEscrowPaymentDate", "Projected Escrow Payment Date"),
                Field.Decimal("LastPaymentEscrow", "Last Payment Escrow"),
                Field.Text("EscrowDeletionFee", "Escrow Deletion Fee"),
                Field.Text("LateCharges", "Late Charges"),
                Field.Text("LoanAssumptionFee", "Loan Assumption Fee"),
                Field.Text("PartialReleaseFee", "Partial Release Fee"),
                Field.Text("PrincipalReductionReAmortizationFee", "Principal Reduction ReAmortization Fee")
            });

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            root.Controls.Add(buttons, 0, 1);
            Button save = new() { Text = "Save", Width = 100 };
            save.Click += (_, _) =>
            {
                try
                {
                    ValidateFields();
                    SaveAccount();
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (FormatException ex)
                {
                    MessageBox.Show(this, ex.Message, "Account", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            buttons.Controls.Add(save);
            Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100 };
            buttons.Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void AddTab(TabControl tabs, string title, IEnumerable<Field> fields)
        {
            Field[] fieldList = fields.ToArray();
            TabPage page = new(title);

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 2,
                RowCount = fieldList.Length + 1,
                Padding = new Padding(12)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.Controls.Add(layout);

            for (int row = 0; row < fieldList.Length; row++)
            {
                Field field = fieldList[row];
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                Label label = CreateFieldLabel(field.Label);
                Control control = CreateControl(field);
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(3, 4, 3, 4);
                layout.Controls.Add(label, 0, row);
                layout.Controls.Add(control, 1, row);
                _fields[field.Name] = control;
                _definitions[field.Name] = field;
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tabs.TabPages.Add(page);
        }

        private static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(3, 4, 3, 4)
            };
        }

        private static Control CreateControl(Field field)
        {
            if (field.Kind == FieldKind.Combo)
            {
                ComboBox combo = new() { DropDownStyle = ComboBoxStyle.DropDown };
                combo.Items.AddRange(field.Values ?? Array.Empty<string>());
                return combo;
            }

            return new TextBox();
        }

        private void LoadAccount()
        {
            Set("AccountNickname", Account.AccountNickname);
            Set("Institution", Account.Institution);
            Set("BaseUrl", Account.BaseUrl);
            Set("AccountSummaryUrl", Account.AccountSummaryUrl);
            Set("AccountType", Account.AccountType);
            Set("AccountOwner", Account.AccountOwner);
            Set("AccountNumber", Account.AccountNumber);
            ConfigureAccountNumberWarning();
            Set("PrincipalBalance", Account.PrincipalBalance);
            Set("AvailableBalance", Account.AvailableBalance);
            Set("CurrentBalance", Account.CurrentBalance);
            Set("BalanceAsOfDate", Account.BalanceAsOfDate);
            Set("OriginalLoanAmount", Account.OriginalLoanAmount);
            Set("CurrentInterestRate", Account.CurrentInterestRate);
            Set("LastPaymentAmount", Account.LastPaymentAmount);
            Set("NextPaymentAmount", Account.NextPaymentAmount);
            Set("NextPaymentDueDate", Account.NextPaymentDueDate);
            Set("NextPaymentInterest", Account.NextPaymentInterest);
            Set("NextPaymentEscrowAmount", Account.NextPaymentEscrowAmount);
            Set("LastEscrowAnalysisDate", Account.LastEscrowAnalysisDate);
            Set("CurrentEscrowAccountBalance", Account.CurrentEscrowAccountBalance);
            Set("CurrentEscrowPayment", Account.CurrentEscrowPayment);
            Set("ProjectedEscrowPayment", Account.ProjectedEscrowPayment);
            Set("ProjectedEscrowPaymentDate", Account.ProjectedEscrowPaymentDate);
            Set("LastPaymentInterestAmount", Account.LastPaymentInterestAmount);
            Set("LastPaymentEscrow", Account.LastPaymentEscrow);
            Set("RegularMonthlyPayment", Account.RegularMonthlyPayment);
            Set("LatePaymentFee", Account.LatePaymentFee);
            Set("EscrowDeletionFee", Account.EscrowDeletionFee);
            Set("LateCharges", Account.LateCharges);
            Set("LoanAssumptionFee", Account.LoanAssumptionFee);
            Set("PartialReleaseFee", Account.PartialReleaseFee);
            Set("PrincipalReductionReAmortizationFee", Account.PrincipalReductionReAmortizationFee);
            Set("LateAfterDays", Account.LateAfterDays);
            Set("LastPaymentDateReceived", Account.LastPaymentDateReceived);
            Set("Recurrence", Account.Recurrence);
            Set("PaidInFullDiscount", Account.PaidInFullDiscount);
        }

        private void ConfigureAccountNumberWarning()
        {
            if (!Account.AccountNumberCannotBeDecrypted || !_fields.TryGetValue("AccountNumber", out Control? control))
            {
                return;
            }

            control.BackColor = Color.FromArgb(255, 248, 220);

            if (control is TextBox textBox)
            {
                textBox.PlaceholderText = "Cannot decrypt here; leave blank to keep existing value or enter a new one";
            }

            _toolTip.SetToolTip(control, Account.AccountNumberProtectionMessage ?? "This account number could not be decrypted on this Windows profile.");
        }

        private void SaveAccount()
        {
            Account.AccountNickname = Get("AccountNickname");
            Account.Institution = Get("Institution");
            Account.BaseUrl = NullIfWhite(Get("BaseUrl"));
            Account.AccountSummaryUrl = NullIfWhite(Get("AccountSummaryUrl"));
            Account.AccountType = Get("AccountType");
            Account.AccountOwner = OwnerValues.Normalize(Get("AccountOwner")) ?? OwnerValues.Unassigned;
            string accountNumber = Get("AccountNumber");

            if (Account.AccountNumberCannotBeDecrypted && string.IsNullOrWhiteSpace(accountNumber))
            {
                Account.AccountNumber = null;
                Account.PreserveExistingAccountNumberOnSave = true;
            }
            else
            {
                Account.AccountNumber = NullIfWhite(accountNumber);
                Account.PreserveExistingAccountNumberOnSave = false;
                Account.AccountNumberCannotBeDecrypted = false;
                Account.AccountNumberProtectionMessage = null;
            }

            Account.PrincipalBalance = GetDecimal("PrincipalBalance");
            Account.AvailableBalance = GetDecimal("AvailableBalance");
            Account.CurrentBalance = GetDecimal("CurrentBalance");
            Account.BalanceAsOfDate = GetDate("BalanceAsOfDate");
            Account.OriginalLoanAmount = GetDecimal("OriginalLoanAmount");
            Account.CurrentInterestRate = GetDecimal("CurrentInterestRate");
            Account.LastPaymentAmount = GetDecimal("LastPaymentAmount");
            Account.NextPaymentAmount = GetDecimal("NextPaymentAmount");
            Account.NextPaymentDueDate = GetDate("NextPaymentDueDate");
            Account.NextPaymentInterest = GetDecimal("NextPaymentInterest");
            Account.NextPaymentEscrowAmount = GetDecimal("NextPaymentEscrowAmount");
            Account.LastEscrowAnalysisDate = GetDate("LastEscrowAnalysisDate");
            Account.CurrentEscrowAccountBalance = GetDecimal("CurrentEscrowAccountBalance");
            Account.CurrentEscrowPayment = GetDecimal("CurrentEscrowPayment");
            Account.ProjectedEscrowPayment = GetDecimal("ProjectedEscrowPayment");
            Account.ProjectedEscrowPaymentDate = GetDate("ProjectedEscrowPaymentDate");
            Account.LastPaymentInterestAmount = GetDecimal("LastPaymentInterestAmount");
            Account.LastPaymentEscrow = GetDecimal("LastPaymentEscrow");
            Account.RegularMonthlyPayment = GetDecimal("RegularMonthlyPayment");
            Account.LatePaymentFee = GetDecimal("LatePaymentFee");
            Account.EscrowDeletionFee = NullIfWhite(Get("EscrowDeletionFee"));
            Account.LateCharges = NullIfWhite(Get("LateCharges"));
            Account.LoanAssumptionFee = NullIfWhite(Get("LoanAssumptionFee"));
            Account.PartialReleaseFee = NullIfWhite(Get("PartialReleaseFee"));
            Account.PrincipalReductionReAmortizationFee = NullIfWhite(Get("PrincipalReductionReAmortizationFee"));
            Account.LateAfterDays = GetInt("LateAfterDays");
            Account.LastPaymentDateReceived = GetDate("LastPaymentDateReceived");
            Account.Recurrence = NullIfWhite(Get("Recurrence"));
            Account.PaidInFullDiscount = GetDecimal("PaidInFullDiscount");
        }

        private void Set(string name, object? value)
        {
            if (!_fields.TryGetValue(name, out Control? control))
            {
                return;
            }

            control.Text = value switch
            {
                null => string.Empty,
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                decimal amount => amount.ToString(CultureInfo.InvariantCulture),
                int number => number.ToString(CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        private string Get(string name)
        {
            return _fields.TryGetValue(name, out Control? control) ? control.Text.Trim() : string.Empty;
        }

        private decimal? GetDecimal(string name)
        {
            int scale = name is "CurrentInterestRate" or "NextPaymentInterest" ? 5 : 2;
            return FinancialInputParser.OptionalDecimal(Get(name), _definitions[name].Label, scale);
        }

        private int? GetInt(string name)
        {
            return FinancialInputParser.OptionalInteger(Get(name), _definitions[name].Label);
        }

        private DateOnly? GetDate(string name)
        {
            return FinancialInputParser.OptionalDate(Get(name), _definitions[name].Label);
        }

        private void ValidateFields()
        {
            foreach (Field field in _definitions.Values)
            {
                switch (field.Kind)
                {
                    case FieldKind.Decimal:
                        _ = GetDecimal(field.Name);
                        break;
                    case FieldKind.Int:
                        _ = GetInt(field.Name);
                        break;
                    case FieldKind.Date:
                        _ = GetDate(field.Name);
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(Get("AccountNickname")) || string.IsNullOrWhiteSpace(Get("AccountType")))
            {
                throw new FormatException("Account nickname and account type are required.");
            }

            if (GetDate("BalanceAsOfDate") > DateOnly.FromDateTime(DateTime.Today))
            {
                throw new FormatException("Balance as-of date cannot be in the future.");
            }
        }

        private static string? NullIfWhite(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private sealed record Field(string Name, string Label, FieldKind Kind, string[]? Values = null)
        {
            public static Field Text(string name, string label) => new(name, label, FieldKind.Text);
            public static Field Decimal(string name, string label) => new(name, label, FieldKind.Decimal);
            public static Field Date(string name, string label) => new(name, label, FieldKind.Date);
            public static Field Int(string name, string label) => new(name, label, FieldKind.Int);
            public static Field Combo(string name, string label, string[] values) => new(name, label, FieldKind.Combo, values);
        }

        private enum FieldKind
        {
            Text,
            Decimal,
            Date,
            Int,
            Combo
        }
    }
}
