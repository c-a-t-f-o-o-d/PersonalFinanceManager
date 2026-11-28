using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views;

public sealed class RulesView : AsyncPageControl, IPageLayoutControl
{
    private const string _allValue = "All";
    private const string _trueValue = "Yes";
    private const string _falseValue = "No";
    private const string _keepExistingValue = "Keep existing";
    private readonly BindingSource _bindingSource = new();
    private readonly ComboBox _activeFilter = new();
    private readonly ComboBox _categoryFilter = new();
    private readonly ComboBox _categoryTypeFilter = new();
    private readonly Label _countLabel = new();
    private readonly DataGridView _grid = new();
    private readonly ComboBox _incomeFilter = new();
    private readonly ComboBox _incomeOwnerFilter = new();
    private readonly ComboBox _matchModeFilter = new();
    private readonly IRuleWorkflowService _ruleService;
    private readonly TextBox _search = new();
    private readonly List<Button> _mutationButtons = new();
    private readonly ComboBox _transferFilter = new();
    private readonly ComboBox _typeFilter = new();
    private IReadOnlyList<FinancialRule> _allRules = Array.Empty<FinancialRule>();
    private IReadOnlyList<string> _categoryValues = Array.Empty<string>();
    private IReadOnlyList<string> _categoryTypeValues = Array.Empty<string>();
    private bool _loadingFilters;
    private bool _mutationInProgress;
    private bool _sortAscending = true;
    private string _sortProperty = nameof(FinancialRule.Priority);

    public RulesView(IRuleWorkflowService ruleService)
    {
        _ruleService = ruleService;
        InitializeLayout();
    }

    public int PreferredPageHeight => 1050;

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (TryBeginInitialLoad())
        {
            await LoadRulesAsync().ConfigureAwait(true);
        }
    }

    private void InitializeLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(0)
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4)
        };

        root.Controls.Add(commands, 0, 0);
        Button add = new() { Text = "Add", Width = 90 };
        add.Click += async (_, _) => await AddAsync().ConfigureAwait(true);
        commands.Controls.Add(add);
        Button edit = new() { Text = "Edit", Width = 90 };
        edit.Click += async (_, _) => await EditAsync().ConfigureAwait(true);
        commands.Controls.Add(edit);
        Button delete = new() { Text = "Delete", Width = 90 };
        delete.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);
        commands.Controls.Add(delete);
        Button import = new() { Text = "Import Rules XLSX (Replace)", Width = 190 };
        import.Click += async (_, _) => await ImportWorkbookAsync().ConfigureAwait(true);
        commands.Controls.Add(import);
        Button reapply = new() { Text = "Reapply Rules to Imports", Width = 190 };
        reapply.Click += async (_, _) => await ReapplyRulesAsync().ConfigureAwait(true);
        commands.Controls.Add(reapply);
        _mutationButtons.AddRange([add, edit, delete, import, reapply]);
        Button refresh = new() { Text = "Refresh", Width = 90 };
        refresh.Click += async (_, _) => await LoadRulesAsync().ConfigureAwait(true);
        commands.Controls.Add(refresh);

        CardPanel guidanceCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 12)
        };

        Label guidance = new()
        {
            Dock = DockStyle.Fill,
            Text = "Category describes a purpose, such as Groceries; Category Type is a reporting group, such as Food & Drink. You can choose suggestions or enter your own values.\r\n"
                + "Income and Transfer are separate financial decisions. Keep existing leaves those decisions unchanged.\r\n"
                + "Inactive starter templates are examples: edit their descriptions and activate after review. Lower priority numbers win; only the first matching active rule applies.",
            Tag = "SecondaryText"
        };

        guidanceCard.Controls.Add(guidance);
        root.Controls.Add(guidanceCard, 0, 1);

        CardPanel filterCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 14, 10)
        };

        root.Controls.Add(filterCard, 0, 2);

        TableLayoutPanel filterLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };

        filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        filterCard.Controls.Add(filterLayout);

        FlowLayoutPanel filterRow1 = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = false
        };

        filterLayout.Controls.Add(filterRow1, 0, 0);
        filterRow1.Controls.Add(MakeLabel("Search", 58));
        _search.Width = 260;
        _search.TextChanged += (_, _) => ApplyFilters();
        filterRow1.Controls.Add(_search);
        filterRow1.Controls.Add(MakeLabel("Type", 42));
        ConfigureDropDownFilter(_typeFilter, 130);
        filterRow1.Controls.Add(_typeFilter);
        filterRow1.Controls.Add(MakeLabel("Category", 72));
        ConfigureDropDownFilter(_categoryFilter, 160);
        filterRow1.Controls.Add(_categoryFilter);
        filterRow1.Controls.Add(MakeLabel("Category Type", 105));
        ConfigureDropDownFilter(_categoryTypeFilter, 170);
        filterRow1.Controls.Add(_categoryTypeFilter);
        filterRow1.Controls.Add(MakeLabel("Match", 54));
        ConfigureDropDownFilter(_matchModeFilter, 125);
        filterRow1.Controls.Add(_matchModeFilter);

        FlowLayoutPanel filterRow2 = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = false
        };

        filterLayout.Controls.Add(filterRow2, 0, 1);
        filterRow2.Controls.Add(MakeLabel("Income Owner", 104));
        ConfigureDropDownFilter(_incomeOwnerFilter, 128);
        filterRow2.Controls.Add(_incomeOwnerFilter);
        AddTriStateFilter(filterRow2, "Income", _incomeFilter, includeKeepExisting: true);
        AddTriStateFilter(filterRow2, "Transfer", _transferFilter, includeKeepExisting: true);
        AddTriStateFilter(filterRow2, "Active", _activeFilter);
        Button clear = new() { Text = "Clear Filters", Width = 115 };
        clear.Click += (_, _) => ClearFilters();
        filterRow2.Controls.Add(clear);
        _countLabel.AutoSize = true;
        _countLabel.Margin = new Padding(14, 9, 4, 4);
        _countLabel.Tag = "SecondaryText";
        filterRow2.Controls.Add(_countLabel);
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.DataSource = _bindingSource;
        _grid.ColumnHeaderMouseClick += (_, e) => SortByColumn(e.ColumnIndex);
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += async (_, _) => await EditAsync().ConfigureAwait(true);
        AddColumn(nameof(FinancialRule.Priority), "Priority", 70);
        AddColumn(nameof(FinancialRule.RuleName), "Rule", 220);
        AddColumn(nameof(FinancialRule.StarterRuleCode), "Source", 85);
        AddColumn(nameof(FinancialRule.MatchDescription), "Description", 340);
        AddColumn(nameof(FinancialRule.DescriptionMatchMode), "Description Matching", 150);
        AddColumn(nameof(FinancialRule.MatchType), "Type", 110);
        AddColumn(nameof(FinancialRule.Category), "Category", 160);
        AddColumn(nameof(FinancialRule.SpendingClass), "Category Type", 150);
        AddColumn(nameof(FinancialRule.IncomeOwner), "Income Owner", 110);
        AddColumn(nameof(FinancialRule.IsIncome), "Income", 125);
        AddColumn(nameof(FinancialRule.IsTransfer), "Transfer", 125);
        AddColumn(nameof(FinancialRule.IsActive), "Active", 80);
        root.Controls.Add(_grid, 0, 3);
        PageLayout.AutoSizeHeaders(root, 0, 1, 2);
        UiTheme.ApplyTo(this);
    }

    private static Label MakeLabel(string text, int width)
    {
        return new Label
        {
            Text = text,
            Width = width,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(3, 7, 3, 3)
        };
    }

    private static void ConfigureDropDownFilter(ComboBox comboBox, int width)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Width = width;
    }

    private void AddTriStateFilter(FlowLayoutPanel panel, string label, ComboBox comboBox, bool includeKeepExisting = false)
    {
        panel.Controls.Add(MakeLabel(label, 70));
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Width = includeKeepExisting ? 130 : 92;
        comboBox.Items.AddRange(new object[] { _allValue, _trueValue, _falseValue });

        if (includeKeepExisting)
        {
            comboBox.Items.Add(_keepExistingValue);
        }

        comboBox.SelectedIndex = 0;
        comboBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        panel.Controls.Add(comboBox);
    }

    private void AddColumn(string property, string header, int width)
    {
        DataGridViewTextBoxColumn column = new()
        {
            DataPropertyName = property,
            HeaderText = header,
            Width = width,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        _grid.Columns.Add(column);
    }

    private Task LoadRulesAsync()
    {
        return LoadLatestAsync("rules", "Loading rules and categories...", async cancellationToken =>
        {
            Task<IReadOnlyList<FinancialRule>> rules = _ruleService.GetRulesAsync(cancellationToken);
            Task<IReadOnlyList<string>> categories = _ruleService.GetCategoryValuesAsync(cancellationToken);
            Task<IReadOnlyList<string>> categoryTypes = _ruleService.GetCategoryTypeValuesAsync(cancellationToken);
            await Task.WhenAll(rules, categories, categoryTypes).ConfigureAwait(false);

            return (Rules: await rules.ConfigureAwait(false), Categories: await categories.ConfigureAwait(false),
                CategoryTypes: await categoryTypes.ConfigureAwait(false));
        }, result =>
        {
            _allRules = result.Rules;
            _categoryValues = result.Categories;
            _categoryTypeValues = result.CategoryTypes;
            LoadFilterValues();
            ApplyFilters();
        });
    }

    private void LoadFilterValues()
    {
        _loadingFilters = true;

        try
        {
            LoadComboValues(_typeFilter, _allRules.Select(rule => rule.MatchType));
            LoadComboValues(_categoryFilter, _allRules.Select(rule => rule.Category));
            LoadComboValues(_categoryTypeFilter, _allRules.Select(rule => rule.SpendingClass));
            LoadComboValues(_matchModeFilter, _allRules.Select(rule => rule.DescriptionMatchMode));
            LoadComboValues(_incomeOwnerFilter, _allRules.Select(rule => rule.IncomeOwner));
        }
        finally
        {
            _loadingFilters = false;
        }
    }

    private void LoadComboValues(ComboBox comboBox, IEnumerable<string?> values)
    {
        string current = comboBox.Text;
        comboBox.SelectedIndexChanged -= ComboFilterSelectedIndexChanged;
        comboBox.Items.Clear();
        comboBox.Items.Add(_allValue);

        foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value))
        {
            comboBox.Items.Add(value);
        }

        comboBox.SelectedIndex = comboBox.Items.Contains(current) ? comboBox.Items.IndexOf(current) : 0;
        comboBox.SelectedIndexChanged += ComboFilterSelectedIndexChanged;
    }

    private void ComboFilterSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (!_loadingFilters)
        {
            ApplyFilters();
        }
    }

    private void ApplyFilters()
    {
        if (!IsPageActive || _loadingFilters)
        {
            return;
        }

        IEnumerable<FinancialRule> query = _allRules;
        string search = _search.Text.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(rule => Contains(rule.RuleName, search) || Contains(rule.MatchDescription, search) || Contains(rule.MatchType, search) || Contains(rule.Category, search) || Contains(rule.SpendingClass, search) || Contains(rule.IncomeOwner, search) || Contains(rule.Notes, search));
        }

        query = ApplyTextFilter(query, _typeFilter.Text, rule => rule.MatchType);
        query = ApplyTextFilter(query, _categoryFilter.Text, rule => rule.Category);
        query = ApplyTextFilter(query, _categoryTypeFilter.Text, rule => rule.SpendingClass);
        query = ApplyTextFilter(query, _matchModeFilter.Text, rule => rule.DescriptionMatchMode);
        query = ApplyTextFilter(query, _incomeOwnerFilter.Text, rule => rule.IncomeOwner);
        query = ApplyBooleanFilter(query, _incomeFilter.Text, rule => rule.IsIncome);
        query = ApplyBooleanFilter(query, _transferFilter.Text, rule => rule.IsTransfer);
        query = ApplyBooleanFilter(query, _activeFilter.Text, rule => rule.IsActive);
        List<FinancialRule> filtered = SortRules(query).ToList();
        _bindingSource.DataSource = filtered;
        _countLabel.Text = $"{filtered.Count:N0} of {_allRules.Count:N0} rules | {_allRules.Count(rule => rule.IsActive):N0} active";
        UpdateSortGlyphs();
    }

    private static IEnumerable<FinancialRule> ApplyTextFilter(IEnumerable<FinancialRule> query, string selectedValue, Func<FinancialRule, string?> selector)
    {
        if (string.IsNullOrWhiteSpace(selectedValue) || string.Equals(selectedValue, _allValue, StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        return query.Where(rule => string.Equals(selector(rule)?.Trim(), selectedValue.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<FinancialRule> ApplyBooleanFilter(IEnumerable<FinancialRule> query, string selectedValue, Func<FinancialRule, bool?> selector)
    {
        if (string.IsNullOrWhiteSpace(selectedValue) || string.Equals(selectedValue, _allValue, StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        if (string.Equals(selectedValue, _keepExistingValue, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(rule => !selector(rule).HasValue);
        }

        bool expected = string.Equals(selectedValue, _trueValue, StringComparison.OrdinalIgnoreCase);
        return query.Where(rule => selector(rule) == expected);
    }

    private IEnumerable<FinancialRule> SortRules(IEnumerable<FinancialRule> rules)
    {
        return _sortProperty switch
        {
            nameof(FinancialRule.RuleName) => Sort(rules, rule => rule.RuleName),
            nameof(FinancialRule.StarterRuleCode) => Sort(rules, rule => rule.StarterRuleCode is null ? "Custom" : "Starter"),
            nameof(FinancialRule.MatchDescription) => Sort(rules, rule => rule.MatchDescription),
            nameof(FinancialRule.DescriptionMatchMode) => Sort(rules, rule => rule.DescriptionMatchMode),
            nameof(FinancialRule.MatchType) => Sort(rules, rule => rule.MatchType ?? string.Empty),
            nameof(FinancialRule.Category) => Sort(rules, rule => rule.Category ?? string.Empty),
            nameof(FinancialRule.SpendingClass) => Sort(rules, rule => rule.SpendingClass ?? string.Empty),
            nameof(FinancialRule.IncomeOwner) => Sort(rules, rule => rule.IncomeOwner ?? string.Empty),
            nameof(FinancialRule.IsIncome) => Sort(rules, rule => rule.IsIncome),
            nameof(FinancialRule.IsTransfer) => Sort(rules, rule => rule.IsTransfer),
            nameof(FinancialRule.IsActive) => Sort(rules, rule => rule.IsActive),
            _ => Sort(rules, rule => rule.Priority)
        };
    }

    private IEnumerable<FinancialRule> Sort<TKey>(IEnumerable<FinancialRule> rules, Func<FinancialRule, TKey> selector)
    {
        IOrderedEnumerable<FinancialRule> ordered = _sortAscending ? rules.OrderBy(selector) : rules.OrderByDescending(selector);
        return ordered.ThenBy(rule => rule.Priority).ThenBy(rule => rule.RuleName);
    }

    private void SortByColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
        {
            return;
        }

        string property = _grid.Columns[columnIndex].DataPropertyName;

        if (string.IsNullOrWhiteSpace(property))
        {
            return;
        }

        if (string.Equals(_sortProperty, property, StringComparison.Ordinal))
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortProperty = property;
            _sortAscending = true;
        }

        ApplyFilters();
    }

    private void UpdateSortGlyphs()
    {
        foreach (DataGridViewColumn column in _grid.Columns.Cast<DataGridViewColumn>())
        {
            column.HeaderCell.SortGlyphDirection = string.Equals(column.DataPropertyName, _sortProperty, StringComparison.Ordinal) ? (_sortAscending ? SortOrder.Ascending : SortOrder.Descending) : SortOrder.None;
        }
    }

    private void ClearFilters()
    {
        _search.Clear();
        ResetCombo(_typeFilter);
        ResetCombo(_categoryFilter);
        ResetCombo(_categoryTypeFilter);
        ResetCombo(_matchModeFilter);
        ResetCombo(_incomeOwnerFilter);
        ResetCombo(_incomeFilter);
        ResetCombo(_transferFilter);
        ResetCombo(_activeFilter);
        ApplyFilters();
    }

    private static void ResetCombo(ComboBox comboBox)
    {
        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static bool Contains(string? value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        string property = _grid.Columns[e.ColumnIndex].DataPropertyName;

        if (property == nameof(FinancialRule.StarterRuleCode))
        {
            e.Value = string.IsNullOrWhiteSpace(e.Value as string) ? "Custom" : "Starter";
            e.FormattingApplied = true;
            return;
        }

        if (property is not nameof(FinancialRule.IsIncome) and not nameof(FinancialRule.IsTransfer) and not nameof(FinancialRule.IsActive))
        {
            return;
        }

        bool? value = e.Value switch
        {
            bool boolValue => boolValue,
            string text when bool.TryParse(text, out bool boolValue) => boolValue,
            _ => null
        };

        if (!value.HasValue)
        {
            e.Value = _keepExistingValue;
            e.CellStyle.ForeColor = UiTheme.TextMuted;
            e.FormattingApplied = true;
            return;
        }

        e.Value = value.Value ? _trueValue : _falseValue;
        e.CellStyle.ForeColor = value.Value ? UiTheme.Success : UiTheme.TextMuted;
        e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
        e.FormattingApplied = true;
    }

    private async Task AddAsync()
    {
        if (!IsPageActive || _mutationInProgress)
        {
            return;
        }

        using RuleEditForm form = new(new FinancialRule { RuleName = "New Rule", Priority = 100, IsActive = true }, _categoryValues, _categoryTypeValues);

        if (form.ShowDialog(this) != DialogResult.OK || !IsPageActive)
        {
            return;
        }

        await RunRuleMutationAsync(async () =>
        {
            await _ruleService.SaveRuleAsync(form.Rule, CancellationToken.None).ConfigureAwait(true);

            if (IsPageActive)
            {
                await LoadRulesAsync().ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    private async Task EditAsync()
    {
        if (!IsPageActive || _mutationInProgress || _bindingSource.Current is not FinancialRule selected)
        {
            return;
        }

        FinancialRule copy = Copy(selected);
        using RuleEditForm form = new(copy, _categoryValues, _categoryTypeValues);

        if (form.ShowDialog(this) != DialogResult.OK || !IsPageActive)
        {
            return;
        }

        await RunRuleMutationAsync(async () =>
        {
            await _ruleService.SaveRuleAsync(form.Rule, CancellationToken.None).ConfigureAwait(true);

            if (IsPageActive)
            {
                await LoadRulesAsync().ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    private async Task DeleteAsync()
    {
        if (!IsPageActive || _mutationInProgress || _bindingSource.Current is not FinancialRule selected)
        {
            return;
        }

        DialogResult confirm = MessageBox.Show(this, $"Delete rule '{selected.RuleName}'?", "Delete Rule", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes || !IsPageActive)
        {
            return;
        }

        await RunRuleMutationAsync(async () =>
        {
            await _ruleService.DeleteRuleAsync(selected.RuleId, CancellationToken.None).ConfigureAwait(true);

            if (IsPageActive)
            {
                await LoadRulesAsync().ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    private async Task ImportWorkbookAsync()
    {
        if (!IsPageActive || _mutationInProgress)
        {
            return;
        }

        using OpenFileDialog dialog = new()
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            Title = "Select Rules Workbook"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || !IsPageActive)
        {
            return;
        }

        RuleImportResult result = _ruleService.ParseWorkbook(dialog.FileName);

        if (result.Errors.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, result.Errors.Take(10)), "Rules Import Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            if (!IsPageActive || result.Rows.Count == 0)
            {
                return;
            }
        }

        DialogResult confirm = MessageBox.Show(this, $"This will replace all current rules with {result.Rows.Count} workbook rule(s). Continue?", "Replace Rules", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes || !IsPageActive)
        {
            return;
        }

        await ImportConfirmedRulesAsync(result.Rows).ConfigureAwait(true);
    }

    private Task ImportConfirmedRulesAsync(IReadOnlyList<RuleImportRow> rows)
    {
        return RunRuleMutationAsync(async () =>
        {
            await _ruleService.ReplaceRulesWithWorkbookRowsAsync(rows, CancellationToken.None).ConfigureAwait(true);

            if (!IsPageActive)
            {
                return;
            }

            await LoadRulesAsync().ConfigureAwait(true);

            if (IsPageActive)
            {
                MessageBox.Show(this, $"Imported {rows.Count} rule(s).", "Rules Imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        });
    }

    private async Task ReapplyRulesAsync()
    {
        if (!IsPageActive || _mutationInProgress)
        {
            return;
        }

        DialogResult confirm = MessageBox.Show(this, "Reapply active rules to imported transactions? This refreshes rule-based categories and financial treatment. Manual classifications and reviewed transfer links are preserved.", "Reapply Rules", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes || !IsPageActive)
        {
            return;
        }

        await ReapplyConfirmedRulesAsync().ConfigureAwait(true);
    }

    private Task ReapplyConfirmedRulesAsync()
    {
        return RunRuleMutationAsync(async () =>
        {
            int count = await _ruleService.ReapplyRulesAsync(CancellationToken.None).ConfigureAwait(true);

            if (IsPageActive)
            {
                MessageBox.Show(this, $"Reapplied rules to {count} imported transaction(s).", "Rules Reapplied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        });
    }

    private async Task RunRuleMutationAsync(Func<Task> operation)
    {
        if (!IsPageActive || _mutationInProgress)
        {
            return;
        }

        _mutationInProgress = true;

        foreach (Button button in _mutationButtons)
        {
            button.Enabled = false;
        }

        try
        {
            await operation().ConfigureAwait(true);
        }
        finally
        {
            _mutationInProgress = false;

            if (IsPageActive)
            {
                foreach (Button button in _mutationButtons)
                {
                    button.Enabled = true;
                }
            }
        }
    }

    private static FinancialRule Copy(FinancialRule rule)
    {
        return new FinancialRule
        {
            RuleId = rule.RuleId,
            RuleName = rule.RuleName,
            Priority = rule.Priority,
            MatchDescription = rule.MatchDescription,
            MatchType = rule.MatchType,
            DescriptionMatchMode = rule.DescriptionMatchMode,
            TypeMatchMode = rule.TypeMatchMode,
            Category = rule.Category,
            SpendingClass = rule.SpendingClass,
            Merchant = rule.Merchant,
            IncomeOwner = rule.IncomeOwner,
            IsIncome = rule.IsIncome,
            IsTransfer = rule.IsTransfer,
            IsActive = rule.IsActive,
            Notes = rule.Notes,
            StarterRuleCode = rule.StarterRuleCode
        };
    }
}
