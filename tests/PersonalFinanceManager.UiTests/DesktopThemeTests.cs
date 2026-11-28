using System.Runtime.InteropServices;
using PersonalFinanceManager.App.UI;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.App.UI.Views;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Configuration;
using PersonalFinanceManager.Data.Logging;
using static PersonalFinanceManager.UiTests.UiTestSupport;
using WinFormsApplication = System.Windows.Forms.Application;

[assembly: DoNotParallelize]

namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class DesktopThemeTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void ThemeTogglePreservesEditsSelectionFormatsAndTextHierarchy()
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(false);
                using ThemedForm form = new() { Width = 760, Height = 500 };
                using Font headingFont = new("Segoe UI", 20F, FontStyle.Bold);
                Label heading = new() { Text = "Monthly overview", Font = headingFont, AutoSize = true, Location = new Point(24, 24) };
                Label status = new() { Text = "Balances match", AutoSize = true, Location = new Point(24, 70) };
                TextBox notes = new() { Text = "Unsaved payment note", Location = new Point(24, 110), Width = 420 };
                Button primary = new() { Text = "Save", Location = new Point(470, 24), Width = 100 };
                Button secondary = new() { Text = "Cancel", Location = new Point(580, 24), Width = 100 };
                UiTheme.StylePrimaryButton(primary);
                UiTheme.StyleSecondaryButton(secondary);
                DataGridView grid = new()
                {
                    Location = new Point(24, 160),
                    Size = new Size(670, 240),
                    AllowUserToAddRows = false,
                    AutoGenerateColumns = false
                };
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Amount",
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" }
                });
                grid.Rows.Add(125M);
                grid.Rows.Add(250M);
                form.Controls.AddRange([heading, status, notes, primary, secondary, grid]);
                UiTheme.SetTextRole(status, "SuccessText");
                form.Show();
                grid.CurrentCell = grid.Rows[1].Cells[0];
                notes.Select(8, 7);
                IntPtr formHandle = form.Handle;
                IntPtr notesHandle = notes.Handle;
                IntPtr gridHandle = grid.Handle;

                foreach (bool dark in new[] { true, false, true })
                {
                    UiTheme.SetDarkMode(dark);
                    WinFormsApplication.DoEvents();
                    Assert.AreEqual("Unsaved payment note", notes.Text);
                    Assert.AreEqual(formHandle, form.Handle);
                    Assert.AreEqual(notesHandle, notes.Handle);
                    Assert.AreEqual(gridHandle, grid.Handle);
                    Assert.AreEqual(8, notes.SelectionStart);
                    Assert.AreEqual(7, notes.SelectionLength);
                    Assert.AreEqual(1, grid.CurrentCell.RowIndex);
                    Assert.AreEqual("C2", grid.Columns[0].DefaultCellStyle.Format);
                    Assert.AreEqual(20F, heading.Font.Size);
                    Assert.IsTrue(heading.Font.Bold);
                    Assert.AreEqual(UiTheme.Success, status.ForeColor);
                    Assert.AreEqual(UiTheme.InputBackground, notes.BackColor);
                    Assert.AreEqual(UiTheme.GridSelected, grid.DefaultCellStyle.SelectionBackColor);
                    AssertReadable(UiTheme.TextPrimary, UiTheme.CardBackground);
                    AssertReadable(UiTheme.TextSecondary, UiTheme.CardBackground);
                    AssertReadable(UiTheme.Success, UiTheme.CardBackground);
                    AssertReadable(UiTheme.Danger, UiTheme.CardBackground);
                    AssertReadable(UiTheme.Warning, UiTheme.CardBackground);
                    AssertReadable(grid.DefaultCellStyle.SelectionForeColor, grid.DefaultCellStyle.SelectionBackColor);
                    AssertNativeCaptionMode(form, dark);

                    if (dark && !SystemInformation.HighContrast)
                    {
                        foreach (Color surface in new[] { UiTheme.AppBackground, UiTheme.SidebarBackground, UiTheme.CardBackground,
                            UiTheme.InputBackground, UiTheme.GridHeader, UiTheme.GridSelected, UiTheme.GridAlternate,
                            UiTheme.CardBorder, UiTheme.AccentSoft, primary.BackColor, primary.FlatAppearance.MouseOverBackColor })
                        {
                            Assert.AreEqual(surface.R, surface.G, "Dark surfaces should use neutral grayscale colors.");
                            Assert.AreEqual(surface.G, surface.B, "Dark surfaces should not have a blue tint.");
                        }
                    }

                    foreach (Button button in new[] { primary, secondary })
                    {
                        AssertReadable(button.ForeColor, button.BackColor);
                        AssertReadable(button.ForeColor, button.FlatAppearance.MouseOverBackColor);
                        AssertReadable(button.ForeColor, button.FlatAppearance.MouseDownBackColor);
                    }
                }

                Label lateStatus = new() { Text = "Review required", AutoSize = true, Location = new Point(450, 110) };
                UiTheme.SetTextRole(lateStatus, "WarningText");
                form.Controls.Add(lateStatus);
                Assert.AreEqual(UiTheme.Warning, lateStatus.ForeColor);
                form.Close();
            });
        }

        [TestMethod]
        public void ShellAndSettingsTogglesPreserveUnsavedConnectionEdits()
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(false);
                string directory = Path.Combine(Path.GetTempPath(), "PFM_UiTests_" + Guid.NewGuid().ToString("N"));

                try
                {
                    SettingsService settings = new(directory);
                    settings.Save(new AppSettings());
                    using MainForm main = new(settings, new FileLogger());
                    main.Show();
                    WinFormsApplication.DoEvents();
                    Descendants(main).OfType<Button>().Single(button => button.AccessibleName == "Settings").PerformClick();
                    SettingsView page = Descendants(main).OfType<SettingsView>().Single();
                    TextBox connection = Descendants(page).OfType<TextBox>().Single(input => input.Name == "ConnectionString");
                    CheckBox darkMode = Descendants(page).OfType<CheckBox>().Single(input => input.Name == "UseDarkMode");
                    const string unsavedConnection = "Data Source=example.invalid;Initial Catalog=Preview;Integrated Security=True";
                    connection.Text = unsavedConnection;
                    Capture(main, "settings-light");

                    Descendants(main).OfType<Button>().Single(button => button.Name == "ThemeToggle").PerformClick();
                    WinFormsApplication.DoEvents();
                    Assert.IsTrue(UiTheme.IsDarkMode);
                    Assert.IsTrue(darkMode.Checked);
                    Assert.AreSame(page, Descendants(main).OfType<SettingsView>().Single());
                    Assert.AreEqual(unsavedConnection, connection.Text);
                    Assert.IsTrue(settings.Load().UseDarkMode);
                    Assert.AreEqual(string.Empty, settings.Load().ConnectionString);
                    Capture(main, "settings-dark");

                    darkMode.Checked = false;
                    WinFormsApplication.DoEvents();
                    Assert.IsFalse(UiTheme.IsDarkMode);
                    Assert.IsFalse(settings.Load().UseDarkMode);
                    Assert.AreEqual(unsavedConnection, connection.Text);
                    Assert.AreSame(page, Descendants(main).OfType<SettingsView>().Single());
                    main.Close();
                }
                finally
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
            });
        }

        [TestMethod]
        public void SavingCategoryRulePreservesNeutralFinancialFlags()
        {
            RunOnUiThread(() =>
            {
                FinancialRule rule = new()
                {
                    RuleName = "Example grocery merchant",
                    MatchDescription = "EXAMPLE GROCERY MARKET",
                    Category = "Groceries",
                    SpendingClass = "Food & Drink",
                    IsIncome = null,
                    IsTransfer = null,
                    IncomeOwner = null
                };
                using RuleEditForm editor = new(rule, ["Groceries"], ["Food & Drink"]);
                editor.Show();
                Descendants(editor).OfType<Button>().Single(button => button.Text == "Save").PerformClick();
                Assert.AreEqual(DialogResult.OK, editor.DialogResult);
                Assert.IsNull(rule.IsIncome);
                Assert.IsNull(rule.IsTransfer);
                Assert.IsNull(rule.IncomeOwner);
            });
        }

        [TestMethod]
        public void ExistingTypeOnlyRuleCanStillBeSaved()
        {
            RunOnUiThread(() =>
            {
                FinancialRule rule = new()
                {
                    RuleId = 1,
                    RuleName = "Review uncategorized withdrawals",
                    MatchDescription = string.Empty,
                    MatchType = "Withdrawal",
                    Category = "Review",
                    IsIncome = null,
                    IsTransfer = null
                };
                using RuleEditForm editor = new(rule);
                editor.Show();
                Descendants(editor).OfType<Button>().Single(button => button.Text == "Save").PerformClick();
                Assert.AreEqual(DialogResult.OK, editor.DialogResult);
                Assert.AreEqual(string.Empty, rule.MatchDescription);
                Assert.AreEqual("Withdrawal", rule.MatchType);
            });
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void RenamingBillRetainsHistoryUnlessExplicitlyDisabled(bool preserveFormerNames)
        {
            RunOnUiThread(() =>
            {
                Account account = new() { AccountId = 1, AccountNickname = "Household checking", AccountType = "Checking" };
                RecurringBill bill = new()
                {
                    RecurringBillId = 17,
                    AccountId = 1,
                    BillName = "Home mortgage",
                    Payee = "Former Home Servicer",
                    SourceDescription = "FORMER HOME SERVICER PAYMENT",
                    Category = "Mortgage",
                    SpendingClass = "Housing",
                    TypicalAmount = 1800M,
                    DueDay = 1
                };
                using RecurringBillEditForm editor = new(bill, [account], ["Mortgage"], ["Housing"], [BillRequirementTypes.Need], showMatching: true);
                editor.Show();
                TextBox payee = Descendants(editor).OfType<TextBox>().Single(input => input.Name == "CurrentPayeeTextBox");
                TextBox description = Descendants(editor).OfType<TextBox>().Single(input => input.Name == "StatementDescriptionTextBox");
                CheckBox retain = Descendants(editor).OfType<CheckBox>().Single(input => input.Name == "PreserveFormerNamesCheckBox");
                Assert.IsTrue(retain.Checked, "A saved bill should preserve its prior matching names by default.");
                retain.Checked = preserveFormerNames;
                payee.Text = "Current Home Servicer";
                description.Text = "CURRENT HOME SERVICER PAYMENT";
                Descendants(editor).OfType<Button>().Single(button => button.Name == "SaveRecurringBillButton").PerformClick();
                Assert.AreEqual(DialogResult.OK, editor.DialogResult);
                Assert.AreEqual(17, bill.RecurringBillId);
                Assert.AreEqual(1800M, bill.TypicalAmount);
                Assert.AreEqual(BillFrequencies.Monthly, bill.Frequency);
                Assert.AreEqual("Current Home Servicer", bill.Payee);
                Assert.AreEqual("CURRENT HOME SERVICER PAYMENT", bill.SourceDescription);
                Assert.AreEqual(preserveFormerNames, bill.PayeeAliases.Contains("Former Home Servicer"));
                Assert.AreEqual(preserveFormerNames, bill.SourceDescriptionAliases.Contains("FORMER HOME SERVICER PAYMENT"));
            });
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DesktopAndEditorsRenderWithSyntheticData(bool dark)
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(dark);
                string configurationDirectory = Path.Combine(Path.GetTempPath(), "PFM_UiTests_" + Guid.NewGuid().ToString("N"));

                try
                {
                    SettingsService settings = new(configurationDirectory);
                    settings.Save(new AppSettings { UseDarkMode = dark });
                    using MainForm main = new(settings, new FileLogger());
                    main.Show();
                    WinFormsApplication.DoEvents();
                    Capture(main, "setup" + (dark ? "-dark" : "-light"));
                    main.Size = main.MinimumSize;
                    WinFormsApplication.DoEvents();
                    Capture(main, "setup-minimum" + (dark ? "-dark" : "-light"));
                    PageHost setupHost = Descendants(main).OfType<PageHost>().Single();
                    Assert.IsFalse(setupHost.HorizontalScroll.Visible);
                    SetupView setup = Descendants(main).OfType<SetupView>().Single();

                    foreach (Label label in Descendants(setup).OfType<Label>().Where(label => !label.AutoEllipsis && label.Text.Length > 20))
                    {
                        Size required = label.GetPreferredSize(new Size(label.Width, 0));
                        Assert.IsTrue(label.Height >= required.Height, $"Setup text is clipped at minimum width: {label.Text}");
                    }

                    CardPanel lastCard = setup.Controls.OfType<CardPanel>().OrderBy(card => card.Bottom).Last();
                    setupHost.ScrollControlIntoView(lastCard);
                    WinFormsApplication.DoEvents();
                    Rectangle lastCardBounds = setupHost.RectangleToClient(lastCard.RectangleToScreen(lastCard.ClientRectangle));
                    Assert.IsTrue(setupHost.ClientRectangle.Contains(lastCardBounds), "The setup scrollbar must reach the final card.");
                    Assert.IsFalse(setupHost.HorizontalScroll.Visible);
                    Assert.IsFalse(setup.VerticalScroll.Visible);
                    Capture(main, "setup-minimum-bottom" + (dark ? "-dark" : "-light"));
                    main.Close();

                    Account account = new()
                    {
                        AccountId = 1,
                        AccountNickname = "Household checking",
                        Institution = "Example Financial",
                        AccountType = "Checking"
                    };
                    using AccountEditForm accountEditor = new(account);
                    ShowAndCapture(accountEditor, "account", dark);

                    using TransactionEditForm transactionEditor = new(new ImportedTransaction
                    {
                        AccountId = 1,
                        TransactionDate = new DateOnly(2026, 11, 1),
                        Amount = -1800M,
                        Description = "Example Home Loan Payment",
                        Category = "Mortgage",
                        SpendingClass = "Housing",
                        ClassificationIsManual = true
                    }, [account], ["Mortgage", "Groceries"], ["Housing", "Food"]);
                    ShowAndCapture(transactionEditor, "transaction", dark);

                    using RuleEditForm ruleEditor = new(new FinancialRule
                    {
                        RuleName = "Monthly account fee",
                        MatchDescription = "MONTHLY SERVICE FEE",
                        Category = "Bank Fees",
                        SpendingClass = "Financial",
                        IsIncome = null,
                        IsTransfer = null,
                        IsActive = true
                    }, ["Bank Fees", "Mortgage", "Groceries"], ["Financial", "Housing", "Food"]);
                    ShowAndCapture(ruleEditor, "rule", dark);
                    Assert.IsNull(ruleEditor.Rule.IsIncome);
                    Assert.IsNull(ruleEditor.Rule.IsTransfer);

                    using RecurringBillEditForm billEditor = new(new RecurringBill
                    {
                        RecurringBillId = 1,
                        BillName = "Home mortgage",
                        Payee = "Current Home Servicer",
                        SourceDescription = "CURRENT HOME SERVICER PAYMENT",
                        PayeeAliases = ["Former Home Servicer"],
                        SourceDescriptionAliases = ["FORMER HOME SERVICER PAYMENT"],
                        AccountId = account.AccountId,
                        Category = "Mortgage",
                        SpendingClass = "Housing",
                        TypicalAmount = 1800M,
                        DueDay = 1,
                        RequirementType = BillRequirementTypes.Debt
                    }, [account], ["Mortgage"], ["Housing"], [BillRequirementTypes.Need, BillRequirementTypes.Debt]);
                    billEditor.Show();
                    WinFormsApplication.DoEvents();
                    Capture(billEditor, "recurring-bill" + (dark ? "-dark" : "-light"));
                    TabControl billTabs = Descendants(billEditor).OfType<TabControl>().Single();
                    billTabs.SelectedTab = billTabs.TabPages.Cast<TabPage>().Single(page => page.Text == "Matching");
                    WinFormsApplication.DoEvents();
                    Capture(billEditor, "recurring-bill-matching" + (dark ? "-dark" : "-light"));
                    Assert.AreEqual(1800M, billEditor.Bill.TypicalAmount);
                    Assert.HasCount(1, billEditor.Bill.PayeeAliases);
                    Assert.IsTrue(Descendants(billEditor).OfType<TextBox>().Any(input => input.Text == "Former Home Servicer"));
                    billEditor.Close();
                }
                finally
                {
                    if (Directory.Exists(configurationDirectory))
                    {
                        Directory.Delete(configurationDirectory, true);
                    }
                }
            });
        }

        [TestMethod]
        public void ExpenseReportKeepsLoadedDataWhenThemeChanges()
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(false);
                using ThemedForm host = new() { Text = "Expense analysis", Width = 1380, Height = 1050 };
                ReportsView report = new(new PreviewAnalysisService()) { Dock = DockStyle.Fill };
                host.Controls.Add(report);
                host.Show();
                WinFormsApplication.DoEvents();
                DataGridView[] grids = Descendants(report).OfType<DataGridView>().ToArray();
                Assert.IsTrue(grids.Any(grid => grid.RowCount > 0), "The preview must exercise a populated report.");
                int[] rowCounts = grids.Select(grid => grid.RowCount).ToArray();
                Capture(host, "expenses-light");

                UiTheme.SetDarkMode(true);
                WinFormsApplication.DoEvents();
                CollectionAssert.AreEqual(rowCounts, grids.Select(grid => grid.RowCount).ToArray());
                Assert.IsTrue(grids.All(grid => grid.BackgroundColor == UiTheme.CardBackground));
                Capture(host, "expenses-dark");
                host.Close();
            });
        }

        private void ShowAndCapture(Form form, string name, bool dark)
        {
            form.Show();
            WinFormsApplication.DoEvents();
            Assert.AreEqual(UiTheme.AppBackground, form.BackColor);
            Capture(form, name + (dark ? "-dark" : "-light"));
            form.Close();
        }

        private void Capture(Form form, string name)
        {
            UiTestSupport.Capture(form, name, TestContext);
        }

        private static void AssertReadable(Color foreground, Color background)
        {
            static double Luminance(Color color)
            {
                static double Channel(byte value)
                {
                    double channel = value / 255D;
                    return channel <= 0.04045D ? channel / 12.92D : Math.Pow((channel + 0.055D) / 1.055D, 2.4D);
                }

                return 0.2126D * Channel(color.R) + 0.7152D * Channel(color.G) + 0.0722D * Channel(color.B);
            }

            double first = Luminance(foreground);
            double second = Luminance(background);
            double ratio = (Math.Max(first, second) + 0.05D) / (Math.Min(first, second) + 0.05D);
            Assert.IsGreaterThanOrEqualTo(4.5D, ratio, $"Text contrast is {ratio:F2}:1 for {foreground} on {background}.");
        }

        private static void AssertNativeCaptionMode(Form form, bool dark)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !SystemInformation.HighContrast)
            {
                int result = DwmGetWindowAttribute(form.Handle, 20, out int enabled, sizeof(int));
                Assert.AreEqual(0, result, "The native caption appearance should be readable on Windows 11 or later.");
                Assert.AreEqual(dark ? 1 : 0, enabled, "The native title bar should switch with the client area.");
            }
        }

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    }
}
