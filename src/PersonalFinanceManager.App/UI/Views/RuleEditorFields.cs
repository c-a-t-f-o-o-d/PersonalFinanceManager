namespace PersonalFinanceManager.App.UI.Views
{
    internal static class RuleEditorFields
    {
        public const string KeepExisting = "Keep existing";
        public const string Guidance = "Category describes what a transaction is for, such as Groceries. Category type is a reporting group, such as Food & Drink. Choose a suggested value or enter your own. Income and Transfer control financial treatment separately; Keep existing leaves that decision unchanged. Blank Merchant and Keep existing for Income Owner preserve their values.";

        public static void ConfigureChoices(ComboBox comboBox, IReadOnlyList<string> values, int maxLength)
        {
            comboBox.DropDownStyle = ComboBoxStyle.DropDown;
            comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
            comboBox.MaxLength = maxLength;

            foreach (string value in values)
            {
                comboBox.Items.Add(value);
            }
        }

        public static void ConfigureTreatment(ComboBox comboBox)
        {
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Items.AddRange(new object[] { KeepExisting, "Yes", "No" });
        }

        public static void SetTreatment(ComboBox comboBox, bool? value)
        {
            comboBox.SelectedIndex = value.HasValue ? (value.Value ? 1 : 2) : 0;
        }

        public static bool? GetTreatment(ComboBox comboBox)
        {
            return comboBox.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null
            };
        }

        public static Label CreateGuidance()
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = Guidance,
                Padding = new Padding(16, 12, 16, 8),
                Tag = "SecondaryText"
            };
        }
    }
}
