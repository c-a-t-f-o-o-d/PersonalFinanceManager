using System.ComponentModel;
using System.Globalization;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Salary;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class SalaryAnalysisView : AsyncPageControl, IPageLayoutControl
    {
        private readonly IAnalysisService _analysisService;
        private readonly SalaryRequirementCalculator _calculator = new();
        private readonly TabControl _sections = new();
        private readonly TabPage _resultsTab = new("Results");
        private readonly ComboBox _accountFilter = new();
        private readonly DateTimePicker _fromDate = new();
        private readonly DateTimePicker _toDate = new();
        private readonly ComboBox _filingStatus = new();
        private readonly ComboBox _spendingMode = new();
        private readonly FlowLayoutPanel _scopeList = new();
        private readonly List<CheckBox> _scopeCheckBoxes = new();
        private readonly NumericUpDown _monthlyCushion = new();
        private readonly NumericUpDown _federalOnlyPreTax = new();
        private readonly NumericUpDown _ficaExemptPreTax = new();
        private readonly NumericUpDown _postTaxDeductions = new();
        private readonly NumericUpDown _yourAllowances = new();
        private readonly NumericUpDown _spouseAllowances = new();
        private readonly NumericUpDown _spouseGrossIncome = new();
        private readonly CheckBox _useSpousePostedIncome = new() { Text = "Use posted spouse net income (uncheck to model spouse gross)", Checked = true, AutoSize = true };
        private readonly Label _spousePostedIncomeSummary = new();
        private readonly NumericUpDown _allowanceAnnualValue = new();
        private readonly NumericUpDown _payPeriods = new();
        private readonly Label _modeHelp = new();
        private readonly Label _periodSummary = new();
        private readonly Label _scopeSummary = new();
        private readonly TextBox _reviewNotes = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly Label _spendingMetric = new();
        private readonly Label _salaryMetric = new();
        private readonly Label _takeHomeMetric = new();
        private readonly Label _taxMetric = new();
        private readonly SalaryBarControl _barChart = new();
        private readonly DataGridView _breakdownGrid = new();
        private readonly DataGridView _inputGrid = new();
        private readonly DataGridView _monthlyGrid = new();
        private readonly BindingSource _breakdownSource = new();
        private readonly BindingSource _inputSource = new();
        private readonly BindingSource _monthlySource = new();
        private bool _bindingAccounts;
        private IReadOnlyList<SalarySpendingScopeOption> _scopeOptions = Array.Empty<SalarySpendingScopeOption>();
        private decimal _spousePostedMonthlyNetIncome;

        public SalaryAnalysisView(IAnalysisService analysisService)
        {
            _analysisService = analysisService;
            InitializeLayout();
            BindAccounts(Array.Empty<Account>());
            SetLastMonthsWithoutRefresh(12);
        }

        public int PreferredPageHeight => 1520;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!TryBeginInitialLoad())
            {
                return;
            }

            await Task.WhenAll(LoadAccountsAsync(), RefreshAsync()).ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            AutoScroll = false;
            BackColor = UiTheme.AppBackground;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.AppBackground,
                AutoScroll = false,
                Margin = Padding.Empty
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            TableLayoutPanel actions = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };

            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Button calculate = new() { Text = "Calculate", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 8, 5) };
            UiTheme.StylePrimaryButton(calculate);

            calculate.Click += async (_, _) => await Task.WhenAll(LoadAccountsAsync(), RefreshAsync(showResults: true)).ConfigureAwait(true);

            actions.Controls.Add(calculate, 0, 0);
            _periodSummary.Dock = DockStyle.Fill;
            _periodSummary.TextAlign = ContentAlignment.MiddleLeft;
            _periodSummary.Tag = "SecondaryText";
            _periodSummary.AutoEllipsis = true;
            actions.Controls.Add(_periodSummary, 1, 0);
            root.Controls.Add(actions, 0, 0);

            _sections.Name = "SalarySections";
            _sections.Dock = DockStyle.Fill;
            _sections.Padding = new Point(12, 6);
            root.Controls.Add(_sections, 0, 1);
            TabPage scopeTab = new("Spending Basis") { AutoScroll = true };
            TabPage bucketsTab = new("Spending Buckets");
            TabPage taxTab = new("Tax & Paycheck") { AutoScroll = true };
            TabPage allowanceTab = new("Allowances") { AutoScroll = true };
            _sections.TabPages.AddRange([scopeTab, bucketsTab, taxTab, allowanceTab, _resultsTab]);
            BuildSpendingBasisTab(scopeTab);
            BuildSpendingBucketsTab(bucketsTab);
            BuildTaxTab(taxTab);
            BuildAllowanceTab(allowanceTab);
            BuildResultsTab(_resultsTab);

            Label disclaimer = new()
            {
                Dock = DockStyle.Fill,
                Text = "Planning estimate only. Federal allowance fields are legacy planning inputs, not current W-4 advice. Tennessee earned-income withholding is treated as $0.",
                Tag = "SecondaryText",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            root.Controls.Add(disclaimer, 0, 2);
            UiTheme.ApplyTo(this);
        }

        private void BuildResultsTab(Control parent)
        {
            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8),
                Margin = Padding.Empty
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parent.Controls.Add(layout);

            TableLayoutPanel metricRow = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                BackColor = UiTheme.AppBackground,
                Margin = new Padding(0, 0, 0, 8)
            };

            layout.Controls.Add(metricRow, 0, 0);
            CardPanel[] cards =
            [
                BuildMetricCard("Monthly target", _spendingMetric),
                BuildMetricCard("Required gross salary", _salaryMetric),
                BuildMetricCard("Take-home estimate", _takeHomeMetric),
                BuildMetricCard("Taxes and FICA", _taxMetric)
            ];

            metricRow.Controls.AddRange(cards);
            metricRow.Resize += (_, _) => ArrangeMetricCards(layout, metricRow, cards);
            ArrangeMetricCards(layout, metricRow, cards);

            CardPanel gridCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Margin = Padding.Empty
            };

            layout.Controls.Add(gridCard, 0, 1);
            BuildGridTabs(gridCard);
        }

        private static void ArrangeMetricCards(TableLayoutPanel parent, TableLayoutPanel metrics, IReadOnlyList<CardPanel> cards)
        {
            int columns = metrics.ClientSize.Width < metrics.LogicalToDeviceUnits(850) ? 2 : 4;
            int rows = cards.Count / columns;
            int height = metrics.LogicalToDeviceUnits(96) * rows;

            if (metrics.ColumnCount == columns && metrics.RowCount == rows && parent.RowStyles[0].Height == height)
            {
                return;
            }

            metrics.SuspendLayout();
            metrics.ColumnStyles.Clear();
            metrics.RowStyles.Clear();
            metrics.ColumnCount = columns;
            metrics.RowCount = rows;

            for (int column = 0; column < columns; column++)
            {
                metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
            }

            for (int row = 0; row < rows; row++)
            {
                metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
            }

            for (int index = 0; index < cards.Count; index++)
            {
                metrics.SetCellPosition(cards[index], new TableLayoutPanelCellPosition(index % columns, index / columns));
            }

            parent.RowStyles[0].Height = height;
            metrics.ResumeLayout(true);
        }

        private void BuildSpendingBasisTab(Control parent)
        {
            TableLayoutPanel left = new()
            {
                Dock = DockStyle.Top,
                Height = 360,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(12),
                BackColor = UiTheme.CardBackground,
                Margin = Padding.Empty
            };

            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            for (int index = 0; index < 5; index++)
            {
                left.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            }

            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parent.Controls.Add(left);
            FlowLayoutPanel row1 = BuildFlowRow(true);
            left.Controls.Add(row1, 0, 0);
            AddFilterLabel(row1, "Account", 74);
            _accountFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _accountFilter.Width = 405;
            row1.Controls.Add(_accountFilter);
            _accountFilter.SelectedIndexChanged += async (_, _) =>
            {
                if (!_bindingAccounts && IsPageActive)
                {
                    await LoadSpendingScopeOptionsAsync().ConfigureAwait(true);
                }
            };
            FlowLayoutPanel row2 = BuildFlowRow(true);
            left.Controls.Add(row2, 0, 1);
            AddFilterLabel(row2, "From", 74);
            ConfigureDatePicker(_fromDate);
            row2.Controls.Add(_fromDate);
            AddFilterLabel(row2, "To", 36);
            ConfigureDatePicker(_toDate);
            row2.Controls.Add(_toDate);
            Button refreshBuckets = new() { Text = "Update Buckets", Width = 126, Height = 30 };
            refreshBuckets.Click += async (_, _) => await LoadSpendingScopeOptionsAsync().ConfigureAwait(true);
            row2.Controls.Add(refreshBuckets);
            FlowLayoutPanel row3 = BuildFlowRow(true);
            left.Controls.Add(row3, 0, 2);
            AddFilterLabel(row3, "Quick range", 74);
            AddRangeButton(row3, "Current Year", async () => await SetCurrentYearAsync().ConfigureAwait(true), 106);
            AddRangeButton(row3, "Last 3 Mo", async () => await SetLastMonthsAsync(3).ConfigureAwait(true), 92);
            AddRangeButton(row3, "Last 12 Mo", async () => await SetLastMonthsAsync(12).ConfigureAwait(true), 98);
            AddRangeButton(row3, "All", async () => await SetAllAsync().ConfigureAwait(true), 60);
            FlowLayoutPanel row4 = BuildFlowRow(true);
            left.Controls.Add(row4, 0, 3);
            AddFilterLabel(row4, "Basis", 74);
            _spendingMode.DropDownStyle = ComboBoxStyle.DropDownList;

            _spendingMode.Items.AddRange(new object[]
            {
                SalarySpendingModeLabels.AllExpenseTrend,
                SalarySpendingModeLabels.RequiredBillsOnly,
                SalarySpendingModeLabels.SelectedCategoriesOnly,
                SalarySpendingModeLabels.RequiredBillsPlusSelectedCategories
            });

            _spendingMode.SelectedIndex = 0;
            _spendingMode.Width = 380;
            _spendingMode.SelectedIndexChanged += (_, _) => UpdateModeHelp();
            row4.Controls.Add(_spendingMode);
            FlowLayoutPanel row5 = BuildFlowRow(true);
            left.Controls.Add(row5, 0, 4);
            AddFilterLabel(row5, "Cushion/mo", 74);
            ConfigureMoneyInput(_monthlyCushion, 0M, 100000M, 100M, 116);
            row5.Controls.Add(_monthlyCushion);
            _modeHelp.Dock = DockStyle.Fill;
            _modeHelp.Tag = "SecondaryText";
            _modeHelp.Padding = new Padding(3, 4, 20, 0);
            _modeHelp.AutoEllipsis = true;
            left.Controls.Add(_modeHelp, 0, 5);

            UpdateModeHelp();
        }

        private void BuildSpendingBucketsTab(Control parent)
        {
            TableLayoutPanel right = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = UiTheme.CardBackground,
                Padding = new Padding(12),
                Margin = Padding.Empty
            };

            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            parent.Controls.Add(right);
            Label scopeTitle = new()
            {
                Text = "Optional spending buckets",
                Dock = DockStyle.Fill,
                Font = UiTheme.SectionTitleFont
            };

            right.Controls.Add(scopeTitle, 0, 0);
            ConfigureScopeList(_scopeList);
            right.Controls.Add(_scopeList, 0, 1);
            FlowLayoutPanel bucketButtons = BuildFlowRow(true);
            right.Controls.Add(bucketButtons, 0, 2);
            Button selectAll = new() { Text = "Select All", Width = 90, Height = 28 };
            selectAll.Click += (_, _) => SetAllScopeItems(true);
            bucketButtons.Controls.Add(selectAll);
            Button clear = new() { Text = "Clear", Width = 78, Height = 28 };
            clear.Click += (_, _) => SetAllScopeItems(false);
            bucketButtons.Controls.Add(clear);
            _scopeSummary.Dock = DockStyle.Fill;
            _scopeSummary.Tag = "SecondaryText";
            _scopeSummary.TextAlign = ContentAlignment.MiddleLeft;
            _scopeSummary.AutoEllipsis = true;
            right.Controls.Add(_scopeSummary, 0, 3);
        }

        private void BuildTaxTab(Control parent)
        {
            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Top,
                Height = 240,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(16, 12, 16, 12),
                BackColor = UiTheme.CardBackground
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            for (int index = 0; index < 5; index++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            }

            parent.Controls.Add(layout);
            AddLabeledControl(layout, 0, "Filing status", _filingStatus);
            _filingStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _filingStatus.Items.AddRange(new object[] { "Single", "Married Filing Jointly", "Head of Household" });
            _filingStatus.SelectedIndex = 1;
            _filingStatus.Width = 220;
            AddLabeledControl(layout, 1, "Pay periods per year", _payPeriods);
            _payPeriods.Minimum = 1;
            _payPeriods.Maximum = 52;
            _payPeriods.Value = 26;
            _payPeriods.Width = 120;
            AddLabeledControl(layout, 2, "401k / federal-only pre-tax per month", _federalOnlyPreTax);
            ConfigureMoneyInput(_federalOnlyPreTax, 0M, 100000M, 100M, 170);
            AddLabeledControl(layout, 3, "Health/HSA FICA-exempt pre-tax per month", _ficaExemptPreTax);
            ConfigureMoneyInput(_ficaExemptPreTax, 0M, 100000M, 100M, 170);
            AddLabeledControl(layout, 4, "Post-tax deductions per month", _postTaxDeductions);
            ConfigureMoneyInput(_postTaxDeductions, 0M, 100000M, 100M, 170);
        }

        private void BuildAllowanceTab(Control parent)
        {
            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Top,
                Height = 400,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(16, 12, 16, 12),
                BackColor = UiTheme.CardBackground
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            for (int index = 0; index < 6; index++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parent.Controls.Add(layout);
            AddLabeledControl(layout, 0, "My legacy allowances", _yourAllowances);
            ConfigureWholeNumberInput(_yourAllowances, 0, 20, 0, 90);
            AddLabeledControl(layout, 1, "Spouse legacy allowances", _spouseAllowances);
            ConfigureWholeNumberInput(_spouseAllowances, 0, 20, 3, 90);
            AddLabeledControl(layout, 2, "Allowance value per year", _allowanceAnnualValue);
            ConfigureMoneyInput(_allowanceAnnualValue, 0M, 20000M, 100M, 170);
            _allowanceAnnualValue.Value = SalaryRequirementCalculator.LegacyW4AllowanceAmountAnnual;
            AddLabeledControl(layout, 3, "Spouse gross income per year", _spouseGrossIncome);
            ConfigureMoneyInput(_spouseGrossIncome, 0M, 1000000M, 1000M, 180);
            _useSpousePostedIncome.AutoSize = false;
            _useSpousePostedIncome.Dock = DockStyle.Fill;
            layout.Controls.Add(_useSpousePostedIncome, 0, 4);
            layout.SetColumnSpan(_useSpousePostedIncome, 2);
            _spousePostedIncomeSummary.Dock = DockStyle.Fill;
            _spousePostedIncomeSummary.Tag = "SecondaryText";
            _spousePostedIncomeSummary.TextAlign = ContentAlignment.MiddleLeft;
            _spousePostedIncomeSummary.AutoEllipsis = true;
            layout.Controls.Add(_spousePostedIncomeSummary, 0, 5);
            layout.SetColumnSpan(_spousePostedIncomeSummary, 2);

            Label help = new()
            {
                Text = "Posted-net mode counts spouse deposits once; spouse gross supplies joint tax-bracket context only. Gross mode estimates spouse take-home and ignores posted deposits. Legacy allowances are a withholding estimate, not a reduction in tax liability.",
                Dock = DockStyle.Fill,
                Tag = "SecondaryText",
                Padding = new Padding(0, 10, 16, 0)
            };

            layout.SetColumnSpan(help, 2);
            layout.Controls.Add(help, 0, 6);
        }

        private static void AddLabeledControl(TableLayoutPanel layout, int row, string labelText, Control input)
        {
            Label label = new()
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            layout.Controls.Add(label, 0, row);
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            layout.Controls.Add(input, 1, row);
        }

        private static FlowLayoutPanel BuildFlowRow(bool wrapContents)
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = wrapContents,
                AutoScroll = false,
                BackColor = UiTheme.CardBackground
            };
        }

        private static void AddRangeButton(FlowLayoutPanel parent, string text, Func<Task> action, int width)
        {
            Button button = new()
            {
                Text = text,
                Width = width,
                Height = 30
            };

            button.Click += async (_, _) => await action().ConfigureAwait(true);
            parent.Controls.Add(button);
        }

        private static void AddFilterLabel(Control parent, string text, int width)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Width = width,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(5, 8, 4, 3),
                AutoEllipsis = true
            });
        }

        private static void ConfigureDatePicker(DateTimePicker picker)
        {
            picker.Width = 126;
            picker.Format = DateTimePickerFormat.Short;
            picker.ShowCheckBox = true;
            picker.Checked = false;
        }

        private static void ConfigureMoneyInput(NumericUpDown input, decimal minimum, decimal maximum, decimal increment, int width)
        {
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.DecimalPlaces = 2;
            input.Increment = increment;
            input.Width = width;
            input.ThousandsSeparator = true;
        }

        private static void ConfigureWholeNumberInput(NumericUpDown input, int minimum, int maximum, int value, int width)
        {
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.DecimalPlaces = 0;
            input.Value = value;
            input.Width = width;
        }

        private void ConfigureScopeList(FlowLayoutPanel list)
        {
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.Tag = "Scrollable";
            list.TabStop = true;
            list.BackColor = UiTheme.InputBackground;
            list.Padding = new Padding(4, 4, 4, 4);
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Margin = new Padding(0, 0, 0, 6);
            list.Resize += (_, _) => AdjustScopeListItemWidths();
            list.MouseEnter += (_, _) => list.Focus();
        }

        private void AdjustScopeListItemWidths()
        {
            int width = Math.Max(1, _scopeList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - _scopeList.Padding.Horizontal - 4);

            foreach (Control control in _scopeList.Controls.Cast<Control>())
            {
                control.Width = width;
                int textWidth = Math.Max(1, width - control.LogicalToDeviceUnits(32));
                Size textSize = TextRenderer.MeasureText(control.Text, control.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                control.Height = Math.Max(control.LogicalToDeviceUnits(30), textSize.Height + control.LogicalToDeviceUnits(8));
            }

            UpdateScopeListScrollExtent();
        }

        private void UpdateScopeListScrollExtent()
        {
            int height = _scopeList.Controls.Cast<Control>().Sum(control => control.Height + control.Margin.Vertical) + _scopeList.Padding.Vertical + 8;
            _scopeList.AutoScrollMinSize = new Size(0, height);
        }

        private static CardPanel BuildMetricCard(string title, Label valueLabel)
        {
            CardPanel card = new()
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 10, 0),
                Padding = new Padding(14, 10, 14, 10)
            };

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                BackColor = UiTheme.CardBackground
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);

            Label titleLabel = new()
            {
                Text = title,
                Dock = DockStyle.Fill,
                Tag = "SecondaryText",
                AutoEllipsis = true
            };

            layout.Controls.Add(titleLabel, 0, 0);
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Regular);
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.AutoEllipsis = true;
            layout.Controls.Add(valueLabel, 0, 1);
            return card;
        }

        private void BuildChartCard(Control parent)
        {
            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                BackColor = UiTheme.CardBackground
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parent.Controls.Add(layout);

            layout.Controls.Add(new Label
            {
                Text = "Gross Salary to Net Take-Home",
                Dock = DockStyle.Fill,
                Font = UiTheme.SectionTitleFont,
                AutoEllipsis = true
            }, 0, 0);

            layout.Controls.Add(new Label
            {
                Text = "Estimated annual gross pay, taxes, deductions, spouse income, and take-home required for the selected spending basis",
                Dock = DockStyle.Fill,
                Tag = "SecondaryText",
                AutoEllipsis = true
            }, 0, 1);

            _barChart.Dock = DockStyle.Fill;
            _barChart.Margin = new Padding(0, 8, 0, 0);
            layout.Controls.Add(_barChart, 0, 2);
        }

        private void BuildGridTabs(Control parent)
        {
            TabControl tabs = new() { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
            parent.Controls.Add(tabs);
            TabPage chartTab = new("Take-home chart") { AutoScroll = true };
            CardPanel chartCard = new() { Dock = DockStyle.Top, Height = 280, Padding = new Padding(12), Margin = Padding.Empty };
            chartTab.Controls.Add(chartCard);
            BuildChartCard(chartCard);
            chartTab.Resize += (_, _) => chartCard.Height = Math.Max(chartTab.ClientSize.Height, chartCard.LogicalToDeviceUnits(260));
            tabs.TabPages.Add(chartTab);
            TabPage breakdownTab = new("Tax Breakdown");
            tabs.TabPages.Add(breakdownTab);
            ConfigureBreakdownGrid();
            breakdownTab.Controls.Add(_breakdownGrid);
            TabPage inputTab = new("Salary Inputs");
            tabs.TabPages.Add(inputTab);
            ConfigureInputGrid();
            inputTab.Controls.Add(_inputGrid);
            TabPage monthlyTab = new("Monthly Transaction Trend");
            tabs.TabPages.Add(monthlyTab);
            ConfigureMonthlyGrid();
            monthlyTab.Controls.Add(_monthlyGrid);
            TabPage notesTab = new("Review notes");
            notesTab.Controls.Add(_reviewNotes);
            tabs.TabPages.Add(notesTab);
        }

        private void ConfigureBreakdownGrid()
        {
            ConfigureStandardGrid(_breakdownGrid, _breakdownSource);
            _breakdownGrid.Columns.Add(MakeTextColumn("Item", "Item", 220));
            _breakdownGrid.Columns.Add(MakeTextColumn("AnnualAmount", "Annual", 120, "C2"));
            _breakdownGrid.Columns.Add(MakeTextColumn("MonthlyAmount", "Monthly", 120, "C2"));
            _breakdownGrid.Columns.Add(MakeFillColumn("Notes", "Notes", 360));
        }

        private void ConfigureInputGrid()
        {
            ConfigureStandardGrid(_inputGrid, _inputSource);
            _inputGrid.Columns.Add(MakeTextColumn("Source", "Source", 120));
            _inputGrid.Columns.Add(MakeTextColumn("Name", "Name", 240));
            _inputGrid.Columns.Add(MakeTextColumn("Category", "Category", 150));
            _inputGrid.Columns.Add(MakeTextColumn("SpendingClass", "Category Type", 150));
            _inputGrid.Columns.Add(MakeTextColumn("MonthlyAmount", "Monthly", 120, "C2"));
            _inputGrid.Columns.Add(MakeTextColumn("AnnualizedAmount", "Annualized", 120, "C2"));
            _inputGrid.Columns.Add(MakeTextColumn("Count", "Count", 80));
            _inputGrid.Columns.Add(MakeFillColumn("Notes", "Notes", 360));
        }

        private void ConfigureMonthlyGrid()
        {
            ConfigureStandardGrid(_monthlyGrid, _monthlySource);
            _monthlyGrid.Columns.Add(MakeTextColumn("MonthLabel", "Month", 130));
            _monthlyGrid.Columns.Add(MakeTextColumn("TotalAmount", "Spending", 140, "C2"));
            _monthlyGrid.Columns.Add(MakeTextColumn("TransactionCount", "Transactions", 110));
        }

        private static void ConfigureStandardGrid(DataGridView grid, BindingSource source)
        {
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowUserToAddRows = false;
            grid.ReadOnly = true;
            grid.DataSource = source;
            UiTheme.StyleGrid(grid);
        }

        private static DataGridViewTextBoxColumn MakeTextColumn(string propertyName, string headerText, int width, string? format = null)
        {
            DataGridViewTextBoxColumn column = new()
            {
                DataPropertyName = propertyName,
                HeaderText = headerText,
                Width = width
            };

            if (!string.IsNullOrWhiteSpace(format))
            {
                column.DefaultCellStyle.Format = format;
            }

            return column;
        }

        private static DataGridViewTextBoxColumn MakeFillColumn(string propertyName, string headerText, int minimumWidth)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = headerText,
                MinimumWidth = minimumWidth,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
        }

        private Task LoadAccountsAsync()
        {
            return LoadLatestAsync("accounts", "Loading account options...", _analysisService.GetAccountsAsync, BindAccounts);
        }

        private void BindAccounts(IReadOnlyList<Account> accounts)
        {
            int? selectedAccountId = (_accountFilter.SelectedItem as AccountOption)?.AccountId;
            List<AccountOption> options = new() { new AccountOption(null, "All Accounts") };
            options.AddRange(accounts.Select(account => new AccountOption(account.AccountId, account.DisplayName)));
            _bindingAccounts = true;

            try
            {
                _accountFilter.DisplayMember = nameof(AccountOption.DisplayName);
                _accountFilter.ValueMember = nameof(AccountOption.AccountId);
                _accountFilter.DataSource = options;
                _accountFilter.SelectedItem = options.FirstOrDefault(option => option.AccountId == selectedAccountId) ?? options[0];
            }
            finally
            {
                _bindingAccounts = false;
            }
        }

        private async Task LoadSpendingScopeOptionsAsync()
        {
            if (!IsPageActive)
            {
                return;
            }

            SalarySpendingFilter filter = BuildScopeOptionFilter();

            if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
            {
                return;
            }

            await LoadLatestAsync("salary-data", "Loading spending buckets...",
                cancellationToken => _analysisService.GetSalarySpendingScopeOptionsAsync(filter, cancellationToken), ApplyScopeOptions).ConfigureAwait(true);
        }

        private void ApplyScopeOptions(IReadOnlyList<SalarySpendingScopeOption> options)
        {
            HashSet<string> selectedKeys = GetSelectedBuckets().Select(BuildBucketKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _scopeOptions = options;
            _scopeList.SuspendLayout();
            _scopeList.Controls.Clear();
            _scopeCheckBoxes.Clear();

            foreach (SalarySpendingScopeOption option in _scopeOptions)
            {
                CheckBox checkBox = BuildScopeCheckBox(option, selectedKeys.Contains(BuildBucketKey(option)));
                _scopeCheckBoxes.Add(checkBox);
                _scopeList.Controls.Add(checkBox);
            }

            if (_scopeOptions.Count == 0)
            {
                Label emptyLabel = new()
                {
                    Text = "No spending buckets were found for the selected account and date range.",
                    Height = 32,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Tag = "SecondaryText",
                    AutoEllipsis = true
                };

                _scopeList.Controls.Add(emptyLabel);
            }

            AdjustScopeListItemWidths();
            UpdateScopeListScrollExtent();
            _scopeList.ResumeLayout(true);
            UpdateModeHelp();
            UpdateScopeSummary();
            Parent?.PerformLayout();
        }

        private CheckBox BuildScopeCheckBox(SalarySpendingScopeOption option, bool isChecked)
        {
            CheckBox checkBox = new()
            {
                Text = FormatBucketText(option),
                Checked = isChecked,
                AutoSize = false,
                Width = Math.Max(1, _scopeList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - _scopeList.Padding.Horizontal - 4),
                Height = 30,
                Tag = option,
                TextAlign = ContentAlignment.MiddleLeft,
                CheckAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(3, 0, 0, 0),
                Margin = new Padding(0, 0, 0, 2),
                Cursor = Cursors.Hand
            };

            checkBox.CheckedChanged += (_, _) => UpdateScopeSummary();
            checkBox.MouseEnter += (_, _) => _scopeList.Focus();
            return checkBox;
        }

        private SalarySpendingFilter BuildScopeOptionFilter()
        {
            AccountOption? option = _accountFilter.SelectedItem as AccountOption;

            return new SalarySpendingFilter
            {
                AccountId = option?.AccountId,
                FromDate = _fromDate.Checked ? DateOnly.FromDateTime(_fromDate.Value.Date) : null,
                ToDate = _toDate.Checked ? DateOnly.FromDateTime(_toDate.Value.Date) : null,
                SpendingMode = SalarySpendingMode.AllExpenseTrend
            };
        }

        private async Task SetCurrentYearAsync()
        {
            DateTime today = DateTime.Today;
            _fromDate.Checked = true;
            _fromDate.Value = new DateTime(today.Year, 1, 1);
            _toDate.Checked = true;
            _toDate.Value = today;
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task SetLastMonthsAsync(int monthCount)
        {
            SetLastMonthsWithoutRefresh(monthCount);
            await RefreshAsync().ConfigureAwait(true);
        }

        private void SetLastMonthsWithoutRefresh(int monthCount)
        {
            DateTime today = DateTime.Today;
            DateTime from = new DateTime(today.Year, today.Month, 1).AddMonths(-(monthCount - 1));
            _fromDate.Checked = true;
            _fromDate.Value = from;
            _toDate.Checked = true;
            _toDate.Value = today;
        }

        private async Task SetAllAsync()
        {
            _fromDate.Checked = false;
            _toDate.Checked = false;
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task RefreshAsync(bool showResults = false)
        {
            if (!IsPageActive)
            {
                return;
            }

            SalarySpendingFilter spendingFilter = BuildSpendingFilter();

            if (spendingFilter.FromDate.HasValue && spendingFilter.ToDate.HasValue && spendingFilter.FromDate > spendingFilter.ToDate)
            {
                MessageBox.Show(this, "The start date must be on or before the end date.", "Salary Needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SalarySpendingFilter scopeFilter = BuildScopeOptionFilter();
            SalaryRequirementInput salaryInput = BuildSalaryInput(0M);
            HashSet<string> selectedKeys = spendingFilter.SelectedSpendingBuckets.Select(BuildBucketKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

            await LoadLatestAsync("salary-data", "Calculating salary needs...", async cancellationToken =>
            {
                IReadOnlyList<SalarySpendingScopeOption> options = await _analysisService.GetSalarySpendingScopeOptionsAsync(scopeFilter, cancellationToken).ConfigureAwait(true);
                cancellationToken.ThrowIfCancellationRequested();
                spendingFilter.SelectedSpendingBuckets = options.Where(option => selectedKeys.Contains(BuildBucketKey(option))).ToArray();
                SalarySpendingAnalysis spending = await _analysisService.GetSalarySpendingAnalysisAsync(spendingFilter, cancellationToken).ConfigureAwait(true);
                return (Options: options, Spending: spending);
            }, result =>
            {
                ApplyScopeOptions(result.Options);
                ApplyAnalysis(result.Spending, salaryInput);

                if (showResults)
                {
                    _sections.SelectedTab = _resultsTab;
                }
            }).ConfigureAwait(true);
        }

        private void ApplyAnalysis(SalarySpendingAnalysis spending, SalaryRequirementInput input)
        {
            IReadOnlyList<IncomeOwnerSummary> incomeSummaries = spending.IncomeOwnerSummaries;
            UpdateIncomeOwnerSummary(incomeSummaries);

            if (!spending.Period.HasObservedTransactions)
            {
                _spousePostedIncomeSummary.Text = "Posted income coverage is unknown for this period.";
            }

            _reviewNotes.Lines = spending.Warnings.ToArray();
            _monthlySource.DataSource = spending.MonthlyTrend;
            _inputSource.DataSource = spending.InputLines;

            if (!spending.CanEstimate)
            {
                _salaryMetric.Text = "Review inputs";
                _takeHomeMetric.Text = "Unknown";
                _taxMetric.Text = "Unknown";
                _spendingMetric.Text = "Review inputs";
                _barChart.Result = null;
                _breakdownSource.DataSource = null;
                _periodSummary.Text = "A reliable salary estimate is unavailable. See Review notes for missing history, bill amounts, or overlapping inputs.";
                return;
            }

            input.TargetMonthlySpending = spending.TargetMonthlySpending;
            input.SpouseMonthlyNetIncome = input.SpouseIncomeMode == SpouseIncomeMode.PostedNet ? Math.Max(0M, _spousePostedMonthlyNetIncome) : 0M;

            if (input.SpouseIncomeMode == SpouseIncomeMode.PostedNet &&
                input.SpouseMonthlyNetIncome > 0M &&
                input.SpouseAnnualGrossIncome == 0M &&
                input.FilingStatus == FilingStatus.MarriedFilingJointly)
            {
                _reviewNotes.Lines = spending.Warnings.Append("Spouse gross income is not supplied. The joint marginal-tax estimate may be too low; enter gross income for tax context when known.").ToArray();
            }

            SalaryRequirementResult result;

            try
            {
                result = _calculator.CalculateRequiredSalary(input);
            }
            catch (ArgumentException ex)
            {
                _salaryMetric.Text = "Review inputs";
                _takeHomeMetric.Text = "Unknown";
                _taxMetric.Text = "Unknown";
                _barChart.Result = null;
                _breakdownSource.DataSource = null;
                _periodSummary.Text = ex.Message;
                return;
            }

            _monthlySource.DataSource = spending.MonthlyTrend;
            _inputSource.DataSource = spending.InputLines;
            _breakdownSource.DataSource = result.BreakdownLines;
            _barChart.Result = result;
            _spendingMetric.Text = result.TargetMonthlyTakeHome.ToString("C2", CultureInfo.CurrentCulture);
            _salaryMetric.Text = result.RequiredGrossAnnualSalary.ToString("C0", CultureInfo.CurrentCulture);
            _takeHomeMetric.Text = result.EstimatedMonthlyTakeHome.ToString("C2", CultureInfo.CurrentCulture) + " / mo";
            _taxMetric.Text = result.TotalEstimatedTax.ToString("C0", CultureInfo.CurrentCulture) + $" ({result.EffectiveTaxRate:N2}%)";
            _periodSummary.Text = BuildPeriodSummary(spending, result);
            UpdateScopeSummary();
        }

        private SalarySpendingFilter BuildSpendingFilter()
        {
            AccountOption? option = _accountFilter.SelectedItem as AccountOption;

            return new SalarySpendingFilter
            {
                AccountId = option?.AccountId,
                FromDate = _fromDate.Checked ? DateOnly.FromDateTime(_fromDate.Value.Date) : null,
                ToDate = _toDate.Checked ? DateOnly.FromDateTime(_toDate.Value.Date) : null,
                SpendingMode = GetSpendingMode(),
                SelectedSpendingBuckets = GetSelectedBuckets()
            };
        }

        private void UpdateIncomeOwnerSummary(IReadOnlyList<IncomeOwnerSummary> summaries)
        {
            IncomeOwnerSummary? spouse = summaries.FirstOrDefault(summary => string.Equals(summary.IncomeOwner, OwnerValues.Spouse, StringComparison.OrdinalIgnoreCase));
            IncomeOwnerSummary? me = summaries.FirstOrDefault(summary => string.Equals(summary.IncomeOwner, OwnerValues.Me, StringComparison.OrdinalIgnoreCase));
            IncomeOwnerSummary? unassigned = summaries.FirstOrDefault(summary => string.Equals(summary.IncomeOwner, OwnerValues.Unassigned, StringComparison.OrdinalIgnoreCase));
            _spousePostedMonthlyNetIncome = spouse?.MonthlyAverageNetIncome ?? 0M;

            _spousePostedIncomeSummary.Text = $"Spouse posted net income: {_spousePostedMonthlyNetIncome:C2}/mo"
                + $" | My posted net income: {(me?.MonthlyAverageNetIncome ?? 0M):C2}/mo"
                + $" | Unassigned income: {(unassigned?.MonthlyAverageNetIncome ?? 0M):C2}/mo";
        }

        private SalaryRequirementInput BuildSalaryInput(decimal targetMonthlySpending)
        {
            return new SalaryRequirementInput
            {
                TargetMonthlySpending = targetMonthlySpending,
                MonthlyCushion = _monthlyCushion.Value,
                PreTaxFederalOnlyMonthly = _federalOnlyPreTax.Value,
                PreTaxFederalAndFicaMonthly = _ficaExemptPreTax.Value,
                PostTaxMonthlyDeductions = _postTaxDeductions.Value,
                UseLegacyAllowanceEstimate = _yourAllowances.Value > 0M || _spouseAllowances.Value > 0M,
                YourLegacyAllowances = decimal.ToInt32(_yourAllowances.Value),
                SpouseLegacyAllowances = decimal.ToInt32(_spouseAllowances.Value),
                LegacyAllowanceAnnualValue = _allowanceAnnualValue.Value,
                SpouseIncomeMode = _useSpousePostedIncome.Checked ? SpouseIncomeMode.PostedNet : SpouseIncomeMode.EstimatedFromGross,
                SpouseAnnualGrossIncome = _spouseGrossIncome.Value,
                SpouseMonthlyNetIncome = _useSpousePostedIncome.Checked ? Math.Max(0M, _spousePostedMonthlyNetIncome) : 0M,
                FilingStatus = GetFilingStatus(),
                PayPeriodsPerYear = decimal.ToInt32(_payPeriods.Value)
            };
        }

        private SalarySpendingMode GetSpendingMode()
        {
            string selected = _spendingMode.SelectedItem?.ToString() ?? SalarySpendingModeLabels.AllExpenseTrend;

            return selected switch
            {
                SalarySpendingModeLabels.RequiredBillsOnly => SalarySpendingMode.RequiredBillsOnly,
                SalarySpendingModeLabels.SelectedCategoriesOnly => SalarySpendingMode.SelectedCategoriesOnly,
                SalarySpendingModeLabels.RequiredBillsPlusSelectedCategories => SalarySpendingMode.RequiredBillsPlusSelectedCategories,
                _ => SalarySpendingMode.AllExpenseTrend
            };
        }

        private IReadOnlyList<SalarySpendingScopeOption> GetSelectedBuckets()
        {
            return _scopeCheckBoxes.Where(checkBox => checkBox.Checked).Select(checkBox => checkBox.Tag).OfType<SalarySpendingScopeOption>().ToList();
        }

        private void SetAllScopeItems(bool isChecked)
        {
            foreach (CheckBox checkBox in _scopeCheckBoxes)
            {
                checkBox.Checked = isChecked;
            }

            UpdateScopeSummary();
        }

        private void UpdateModeHelp()
        {
            SalarySpendingMode mode = GetSpendingMode();
            _scopeList.Enabled = true;

            foreach (CheckBox checkBox in _scopeCheckBoxes)
            {
                checkBox.Enabled = true;
            }

            _modeHelp.Text = mode switch
            {
                SalarySpendingMode.RequiredBillsOnly => "Uses only active required bills. This is useful for a bare-minimum survival number.",
                SalarySpendingMode.SelectedCategoriesOnly => "Uses only the checked items on Spending Buckets. Use this for focused questions such as groceries, medical, or gas only.",
                SalarySpendingMode.RequiredBillsPlusSelectedCategories => "Uses required bills plus selected net spending. Matching historical bill payments are replaced by planned bills; uncertain overlaps require review.",
                _ => "Uses net expenses after refunds. Confirmed transfers, mirrored purchases, and allocated funding are excluded; import coverage still requires review."
            };

            UpdateScopeSummary();
        }

        private void UpdateScopeSummary()
        {
            IReadOnlyList<SalarySpendingScopeOption> selectedBuckets = GetSelectedBuckets();
            int checkedCount = selectedBuckets.Count;
            decimal checkedMonthly = selectedBuckets.Sum(option => option.MonthlyAverage);
            SalarySpendingMode mode = GetSpendingMode();

            string selectionText = mode is SalarySpendingMode.SelectedCategoriesOnly or SalarySpendingMode.RequiredBillsPlusSelectedCategories
                ? $"{checkedCount} bucket(s) selected, {checkedMonthly:C2}/mo selected-period average"
                : $"{_scopeOptions.Count} available spending bucket(s). Bucket selections are ignored in the current basis.";

            _scopeSummary.Text = selectionText;
        }

        private FilingStatus GetFilingStatus()
        {
            return _filingStatus.SelectedIndex switch
            {
                1 => FilingStatus.MarriedFilingJointly,
                2 => FilingStatus.HeadOfHousehold,
                _ => FilingStatus.Single
            };
        }

        private static string BuildPeriodSummary(SalarySpendingAnalysis spending, SalaryRequirementResult result)
        {
            string period = spending.FirstTransactionDate.HasValue && spending.LastTransactionDate.HasValue
                ? spending.FirstTransactionDate.Value.ToString("MMM yyyy", CultureInfo.CurrentCulture) + " - " + spending.LastTransactionDate.Value.ToString("MMM yyyy", CultureInfo.CurrentCulture)
                : "No transaction spending rows found";

            return $"{period}   Basis: {spending.SpendingMode}   Transactions: {spending.TransactionMonthlySpending:C2}/mo   Bills: {spending.RequiredBillsMonthlySpending:C2}/mo   Gross/paycheck: {result.RequiredGrossPerPayPeriod:C2}";
        }

        private static string BuildBucketKey(SalarySpendingScopeOption option)
        {
            return (option.Category ?? string.Empty).Trim() + "|" + (option.SpendingClass ?? string.Empty).Trim();
        }

        private sealed record AccountOption(int? AccountId, string DisplayName);

        private static string FormatBucketText(SalarySpendingScopeOption option)
        {
            return $"{option.Category} / {option.SpendingClass}    {option.MonthlyAverage:C0}/mo avg    {option.TransactionCount} txn across {option.PeriodMonthCount} mo";
        }
    }

    public sealed class SalaryBarControl : Control
    {
        public SalaryBarControl()
        {
            DoubleBuffered = true;
            BackColor = UiTheme.CardBackground;
            MinimumSize = new Size(500, 150);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SalaryRequirementResult? Result
        {
            get;
            set
            {
                field = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (Result is null || Result.CashBeforeModeledTax <= 0M)
            {
                DrawEmpty(e.Graphics);
                return;
            }

            Rectangle bounds = ClientRectangle;
            Rectangle chart = new(bounds.Left + 18, bounds.Top + 20, bounds.Width - 36, 50);
            decimal gross = Result.CashBeforeModeledTax;

            List<Segment> segments =
            [
                new("Federal", Result.FederalIncomeTax, Color.FromArgb(37, 99, 235)),
                new("Social Security", Result.SocialSecurityTax, Color.FromArgb(239, 68, 68)),
                new("Medicare", Result.MedicareTax + Result.AdditionalMedicareTax, Color.FromArgb(245, 158, 11)),
                new("Pre/Post Ded.", Result.AnnualPreTaxDeductions + Result.AnnualPostTaxDeductions, Color.FromArgb(139, 92, 246)),
                new("Take-home", Result.EstimatedAnnualTakeHome, Color.FromArgb(22, 163, 74))
            ];

            float x = chart.Left;

            foreach (Segment segment in segments.Where(segment => segment.Amount > 0M))
            {
                float width = (float)(segment.Amount / gross) * chart.Width;
                using SolidBrush brush = new(segment.Color);
                e.Graphics.FillRectangle(brush, x, chart.Top, Math.Max(1F, width), chart.Height);
                x += width;
            }

            using Pen border = new(UiTheme.CardBorder);
            e.Graphics.DrawRectangle(border, chart);
            DrawLegend(e.Graphics, segments, chart.Bottom + 18);
        }

        private void DrawEmpty(Graphics graphics)
        {
            using SolidBrush brush = new(UiTheme.TextMuted);
            graphics.DrawString("No spending rows available for the selected salary basis.", UiTheme.BodyFont, brush, new PointF(12, 16));
        }

        private void DrawLegend(Graphics graphics, IReadOnlyList<Segment> segments, int y)
        {
            int x = 18;

            foreach (Segment segment in segments.Where(segment => segment.Amount > 0M))
            {
                using SolidBrush swatch = new(segment.Color);
                using SolidBrush textBrush = new(UiTheme.TextPrimary);
                graphics.FillRectangle(swatch, x, y + 3, 12, 12);
                string text = $"{segment.Label}: {segment.Amount:C0}";
                graphics.DrawString(text, UiTheme.SmallFont, textBrush, x + 18, y);
                x += Math.Max(130, TextRenderer.MeasureText(text, UiTheme.SmallFont).Width + 34);

                if (x > Width - 180)
                {
                    x = 18;
                    y += 24;
                }
            }
        }

        private sealed record Segment(string Label, decimal Amount, Color Color);
    }
}
