using System.Globalization;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.App.UI.Styling;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class QuickRuleForm : ThemedForm
    {
        private readonly TextBox _ruleName = new() { MaxLength = 200 };
        private readonly TextBox _priority = new();
        private readonly TextBox _description = new() { MaxLength = 1000 };
        private readonly TextBox _type = new();
        private readonly ComboBox _descriptionMode = new();
        private readonly ComboBox _typeMode = new();
        private readonly ComboBox _category = new();
        private readonly ComboBox _categoryType = new();
        private readonly TextBox _merchant = new() { MaxLength = 200 };
        private readonly ComboBox _incomeOwner = new();
        private readonly ComboBox _isIncome = new();
        private readonly ComboBox _isTransfer = new();
        private readonly CheckBox _isActive = new() { Text = "Active", Checked = true };

        public QuickRuleForm(FinancialRule rule, IReadOnlyList<string> categories, IReadOnlyList<string> categoryTypes)
        {
            Rule = rule;
            InitializeLayout(categories, categoryTypes);
            LoadRule();
        }

        public FinancialRule Rule { get; }

        private void InitializeLayout(IReadOnlyList<string> categories, IReadOnlyList<string> categoryTypes)
        {
            Text = "Create Rule From Transaction";
            Width = 780;
            Height = 740;
            MinimumSize = new Size(640, 560);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);
            MinimizeBox = false;
            MaximizeBox = false;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            Controls.Add(root);
            root.Controls.Add(RuleEditorFields.CreateGuidance(), 0, 0);

            TableLayoutPanel fields = new()
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 2,
                Padding = new Padding(12)
            };

            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(fields, 0, 1);
            _descriptionMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _descriptionMode.Items.AddRange(new object[] { RuleMatchModes.Exact, RuleMatchModes.StartsWith, RuleMatchModes.Contains });
            _typeMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _typeMode.Items.AddRange(new object[] { RuleMatchModes.Exact, RuleMatchModes.Contains, RuleMatchModes.StartsWith });
            RuleEditorFields.ConfigureChoices(_category, categories, 200);
            RuleEditorFields.ConfigureChoices(_categoryType, categoryTypes, 100);
            RuleEditorFields.ConfigureTreatment(_isIncome);
            RuleEditorFields.ConfigureTreatment(_isTransfer);
            _incomeOwner.DropDownStyle = ComboBoxStyle.DropDown;
            _incomeOwner.Items.Add(RuleEditorFields.KeepExisting);
            _incomeOwner.Items.AddRange(OwnerValues.All);
            int row = 0;
            AddRow(fields, ref row, "Rule Name", _ruleName);
            AddRow(fields, ref row, "Priority (lower wins)", _priority);
            AddRow(fields, ref row, "Description", _description, 70);
            AddRow(fields, ref row, "Description Matching", _descriptionMode);
            AddRow(fields, ref row, "Type", _type);
            AddRow(fields, ref row, "Type Matching", _typeMode);
            AddRow(fields, ref row, "Category", _category);
            AddRow(fields, ref row, "Category Type / Group", _categoryType);
            AddRow(fields, ref row, "Merchant", _merchant);
            AddRow(fields, ref row, "Income Owner", _incomeOwner);
            AddRow(fields, ref row, "Income", _isIncome);
            AddRow(fields, ref row, "Transfer", _isTransfer);
            AddRow(fields, ref row, "Active", _isActive);
            FinalizeFixedRows(fields, row);

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            root.Controls.Add(buttons, 0, 2);
            Button save = new() { Text = "Create + Apply", DialogResult = DialogResult.OK, Width = 150 };
            _isActive.CheckedChanged += (_, _) => save.Text = _isActive.Checked ? "Create + Apply" : "Save Inactive Rule";

            save.Click += (_, _) =>
            {
                if (!SaveRule())
                {
                    DialogResult = DialogResult.None;
                }
            };

            buttons.Controls.Add(save);
            Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100 };
            buttons.Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private static void AddRow(TableLayoutPanel fields, ref int row, string labelText, Control control, int height = 36)
        {
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

            Label label = new()
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(3, 4, 3, 4)
            };

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(3, 4, 3, 4);
            fields.Controls.Add(label, 0, row);
            fields.Controls.Add(control, 1, row);
            row++;
        }

        private static void FinalizeFixedRows(TableLayoutPanel fields, int rows)
        {
            fields.RowCount = rows + 1;
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        }

        private void LoadRule()
        {
            _ruleName.Text = Rule.RuleName;
            _priority.Text = Rule.Priority.ToString(CultureInfo.InvariantCulture);
            _description.Text = Rule.MatchDescription;
            _type.Text = Rule.MatchType ?? string.Empty;
            _descriptionMode.Text = string.IsNullOrWhiteSpace(Rule.DescriptionMatchMode) ? RuleMatchModes.Exact : Rule.DescriptionMatchMode;
            _typeMode.Text = string.IsNullOrWhiteSpace(Rule.TypeMatchMode) ? RuleMatchModes.Exact : Rule.TypeMatchMode;
            _category.Text = Rule.Category ?? string.Empty;
            _categoryType.Text = Rule.SpendingClass ?? string.Empty;
            _merchant.Text = Rule.Merchant ?? string.Empty;
            _incomeOwner.Text = Rule.IncomeOwner ?? RuleEditorFields.KeepExisting;
            RuleEditorFields.SetTreatment(_isIncome, Rule.IsIncome);
            RuleEditorFields.SetTreatment(_isTransfer, Rule.IsTransfer);
            _isActive.Checked = Rule.IsActive;
        }

        private bool SaveRule()
        {
            if (string.IsNullOrWhiteSpace(_description.Text))
            {
                MessageBox.Show(this, "Enter rule description text.", "Rule", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_category.Text) || string.IsNullOrWhiteSpace(_categoryType.Text))
            {
                MessageBox.Show(this, "Enter both category and category type.", "Rule", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!int.TryParse(_priority.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int priority))
            {
                MessageBox.Show(this, "Enter a whole number for priority. Lower numbers are applied first.", "Rule", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            Rule.RuleName = TrimToLength(string.IsNullOrWhiteSpace(_ruleName.Text) ? _description.Text.Trim() : _ruleName.Text.Trim(), 200);
            Rule.Priority = priority;
            Rule.MatchDescription = _description.Text.Trim();
            Rule.MatchType = NullIfWhite(_type.Text);
            Rule.DescriptionMatchMode = string.IsNullOrWhiteSpace(_descriptionMode.Text) ? RuleMatchModes.Exact : _descriptionMode.Text.Trim();
            Rule.TypeMatchMode = string.IsNullOrWhiteSpace(_typeMode.Text) ? RuleMatchModes.Exact : _typeMode.Text.Trim();
            Rule.Category = NullIfWhite(_category.Text);
            Rule.SpendingClass = NullIfWhite(_categoryType.Text);
            Rule.Merchant = NullIfWhite(_merchant.Text);
            Rule.IncomeOwner = string.Equals(_incomeOwner.Text.Trim(), RuleEditorFields.KeepExisting, StringComparison.OrdinalIgnoreCase) ? null : OwnerValues.Normalize(_incomeOwner.Text);
            Rule.IsIncome = RuleEditorFields.GetTreatment(_isIncome);
            Rule.IsTransfer = RuleEditorFields.GetTreatment(_isTransfer);
            Rule.IsActive = _isActive.Checked;
            return true;
        }

        private static string TrimToLength(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private static string? NullIfWhite(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
