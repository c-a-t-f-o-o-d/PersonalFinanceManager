using System.Runtime.CompilerServices;
using PersonalFinanceManager.App.UI.Controls;

namespace PersonalFinanceManager.App.UI.Styling
{
    public static class UiTheme
    {
        private static readonly ConditionalWeakTable<Control, ControlStyle> _styles = new();
        private static readonly Font _titleFont = new("Segoe UI", 20F, FontStyle.Bold);
        private static readonly Font _subtitleFont = new("Segoe UI", 9.5F, FontStyle.Regular);
        private static readonly Font _sectionFont = new("Segoe UI", 11F, FontStyle.Bold);
        private static readonly Font _bodyFont = new("Segoe UI", 9F, FontStyle.Regular);
        private static readonly Font _smallFont = new("Segoe UI", 8.5F, FontStyle.Regular);
        private static readonly Font _buttonFont = new("Segoe UI Semibold", 9F, FontStyle.Regular);
        private static bool _changingTheme;

        public static event EventHandler? ThemeChanged;

        public static bool IsDarkMode { get; private set; }
        public static Color AppBackground => Surface(246, 248, 252, 17, 24, 39);
        public static Color SidebarBackground => Surface(250, 252, 255, 15, 23, 36);
        public static Color CardBackground => Surface(255, 255, 255, 25, 35, 51);
        public static Color InputBackground => Surface(255, 255, 255, 19, 29, 45);
        public static Color CardBorder => Surface(221, 227, 236, 54, 68, 88, SystemColors.WindowFrame);
        public static Color TextPrimary => Ink(26, 37, 55, 234, 240, 249);
        public static Color TextSecondary => Ink(81, 98, 120, 176, 191, 212);
        public static Color TextMuted => Ink(103, 118, 138, 151, 168, 192);
        public static Color Accent => Ink(37, 99, 235, 111, 166, 255, SystemColors.Highlight);
        public static Color AccentDark => Ink(29, 78, 216, 169, 200, 255, SystemColors.HighlightText);
        public static Color AccentSoft => Surface(232, 240, 255, 35, 56, 88, SystemColors.Highlight);
        public static Color Success => Ink(21, 128, 61, 94, 218, 146);
        public static Color Danger => Ink(185, 28, 28, 255, 143, 143);
        public static Color Warning => Ink(146, 81, 9, 251, 201, 105);
        public static Color WarningBackground => Surface(255, 248, 220, 66, 49, 26);
        public static Color GridHeader => Surface(242, 246, 251, 31, 44, 63);
        public static Color GridSelected => Surface(222, 235, 255, 43, 67, 101, SystemColors.Highlight);
        public static Color GridLine => Surface(229, 234, 241, 48, 62, 82, SystemColors.WindowFrame);
        public static Color GridAlternate => Surface(249, 251, 254, 28, 39, 57);
        public static Font TitleFont => _titleFont;
        public static Font PageSubtitleFont => _subtitleFont;
        public static Font SectionTitleFont => _sectionFont;
        public static Font BodyFont => _bodyFont;
        public static Font SmallFont => _smallFont;
        public static Font ButtonFont => _buttonFont;

        public static void SetDarkMode(bool useDarkMode)
        {
            SystemColorMode nativeMode = useDarkMode ? SystemColorMode.Dark : SystemColorMode.Classic;

            if (IsDarkMode == useDarkMode && System.Windows.Forms.Application.ColorMode == nativeMode)
            {
                return;
            }

            _changingTheme = true;

            try
            {
                IsDarkMode = useDarkMode;
                System.Windows.Forms.Application.SetColorMode(nativeMode);

                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
            finally
            {
                _changingTheme = false;
            }
        }

        public static void SetTextRole(Control control, string role)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentException.ThrowIfNullOrWhiteSpace(role);
            control.Tag = role;
            ControlStyle style = GetStyle(control);
            style.TextRole = role;
            ApplyForeground(control, style);
        }

        public static void StylePrimaryButton(Button button)
        {
            ArgumentNullException.ThrowIfNull(button);
            button.Tag = "PrimaryButton";
            GetStyle(button).ButtonRole = "PrimaryButton";
            Color background = SystemInformation.HighContrast ? SystemColors.Highlight : IsDarkMode ? Color.FromArgb(49, 105, 205) : Accent;
            Color foreground = SystemInformation.HighContrast ? SystemColors.HighlightText : Color.White;
            StyleButton(button, background, foreground, Accent);
        }

        public static void StyleSecondaryButton(Button button)
        {
            ArgumentNullException.ThrowIfNull(button);
            GetStyle(button).ButtonRole = "SecondaryButton";
            StyleButton(button, CardBackground, TextPrimary, CardBorder);
        }

        public static void StyleNavButton(Button button, bool selected)
        {
            ArgumentNullException.ThrowIfNull(button);
            ControlStyle style = GetStyle(button);
            style.ButtonRole = "NavigationButton";
            style.Selected = selected;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AccentSoft;
            button.FlatAppearance.MouseDownBackColor = GridSelected;

            if (SystemInformation.HighContrast && !selected)
            {
                button.FlatAppearance.MouseOverBackColor = SystemColors.Window;
                button.FlatAppearance.MouseDownBackColor = SystemColors.Window;
            }

            button.Font = ButtonFont;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.BackColor = selected ? AccentSoft : SidebarBackground;
            button.ForeColor = selected ? AccentDark : TextSecondary;
            button.Cursor = Cursors.Hand;
            button.Padding = new Padding(14, 0, 0, 0);
            button.AccessibleDescription = selected ? "Current page" : null;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = CardBackground;
            grid.AllowUserToResizeColumns = true;
            grid.AllowUserToResizeRows = false;
            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = GridLine;
            grid.RowHeadersWidth = 28;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.ColumnHeadersDefaultCellStyle.BackColor = GridHeader;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.Font = ButtonFont;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = GridHeader;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.RowHeadersDefaultCellStyle.BackColor = GridHeader;
            grid.RowHeadersDefaultCellStyle.ForeColor = TextSecondary;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = GridSelected;
            grid.RowHeadersDefaultCellStyle.SelectionForeColor = SelectionText;
            grid.DefaultCellStyle.Font = BodyFont;
            grid.DefaultCellStyle.BackColor = CardBackground;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = GridSelected;
            grid.DefaultCellStyle.SelectionForeColor = SelectionText;
            grid.AlternatingRowsDefaultCellStyle.BackColor = GridAlternate;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, grid.LogicalToDeviceUnits(32));
            grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, grid.LogicalToDeviceUnits(36));
            grid.Invalidate();
        }

        public static void ApplyTo(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);

            if (control.IsDisposed || control.Disposing)
            {
                return;
            }

            ControlStyle style = GetStyle(control);
            style.Applying = true;

            try
            {
                ApplyControl(control, style);
                NativeTheme.Apply(control);

                foreach (Control child in control.Controls)
                {
                    ApplyTo(child);
                }

                control.Invalidate();
            }
            finally
            {
                style.Applying = false;
            }
        }

        private static Color SelectionText => SystemInformation.HighContrast ? SystemColors.HighlightText : TextPrimary;

        private static Color Surface(int red, int green, int blue, int darkRed, int darkGreen, int darkBlue, Color? highContrast = null)
        {
            if (SystemInformation.HighContrast)
            {
                return highContrast ?? SystemColors.Window;
            }

            return IsDarkMode ? Color.FromArgb(darkRed, darkGreen, darkBlue) : Color.FromArgb(red, green, blue);
        }

        private static Color Ink(int red, int green, int blue, int darkRed, int darkGreen, int darkBlue, Color? highContrast = null)
        {
            if (SystemInformation.HighContrast)
            {
                return highContrast ?? SystemColors.WindowText;
            }

            return IsDarkMode ? Color.FromArgb(darkRed, darkGreen, darkBlue) : Color.FromArgb(red, green, blue);
        }

        private static ControlStyle GetStyle(Control control)
        {
            return _styles.GetValue(control, CreateStyle);
        }

        private static ControlStyle CreateStyle(Control control)
        {
            ControlStyle style = new()
            {
                TextRole = GetTextRole(control),
                OriginalForeColor = control.ForeColor,
                SurfaceRole = GetSurfaceRole(control)
            };

            control.ControlAdded += (_, args) =>
            {
                if (args.Control is not null)
                {
                    ApplyTo(args.Control);
                }
            };

            control.HandleCreated += (_, _) => NativeTheme.Apply(control);

            if (control is ComboBox comboBox)
            {
                comboBox.DropDown += (_, _) => NativeTheme.Apply(comboBox);
            }

            if (control is DateTimePicker picker)
            {
                picker.DropDown += (_, _) => NativeTheme.ApplyCalendar(picker);
            }

            if (control is Label)
            {
                control.ForeColorChanged += (_, _) =>
                {
                    if (!style.Applying && !_changingTheme)
                    {
                        style.TextRole = GetColorRole(control.ForeColor) ?? "CustomText";
                        style.OriginalForeColor = control.ForeColor;
                        ApplyForeground(control, style);
                    }
                };
            }

            return style;
        }

        private static void ApplyControl(Control control, ControlStyle style)
        {
            if (control is Button button)
            {
                button.AutoEllipsis = true;

                if (style.ButtonRole == "NavigationButton")
                {
                    StyleNavButton(button, style.Selected);
                }
                else if (Equals(button.Tag, "PrimaryButton") || style.ButtonRole == "PrimaryButton")
                {
                    StylePrimaryButton(button);
                }
                else
                {
                    StyleSecondaryButton(button);
                }

                return;
            }

            ApplyForeground(control, style);

            switch (control)
            {
                case DataGridView grid:
                    StyleGrid(grid);
                    break;
                case TextBoxBase textBox:
                    textBox.BackColor = Equals(textBox.Tag, "WarningInput") ? WarningBackground : textBox.ReadOnly && textBox.BorderStyle == BorderStyle.None ? GetContainerBackground(textBox) : InputBackground;
                    break;
                case ComboBox comboBox:
                    comboBox.FlatStyle = FlatStyle.Flat;
                    comboBox.BackColor = InputBackground;
                    comboBox.IntegralHeight = false;
                    comboBox.DropDownHeight = Math.Max(comboBox.DropDownHeight, comboBox.LogicalToDeviceUnits(240));
                    break;
                case UpDownBase numeric:
                    numeric.BackColor = InputBackground;
                    break;
                case DateTimePicker picker:
                    picker.CalendarForeColor = TextPrimary;
                    picker.CalendarMonthBackground = InputBackground;
                    picker.CalendarTitleBackColor = GridHeader;
                    picker.CalendarTitleForeColor = TextPrimary;
                    picker.CalendarTrailingForeColor = TextMuted;
                    break;
                case Label label:
                    label.AutoEllipsis = true;
                    break;
                case CardPanel:
                case TabPage:
                    control.BackColor = CardBackground;
                    break;
                case ListBox listBox:
                    listBox.BackColor = InputBackground;
                    break;
                case Form:
                    control.BackColor = AppBackground;
                    break;
                case Panel:
                case UserControl:
                case TabControl:
                case GroupBox:
                    control.BackColor = style.SurfaceRole == "CardSurface" ? CardBackground : GetContainerBackground(control);
                    break;
                case CheckBox checkBox:
                    checkBox.UseVisualStyleBackColor = false;
                    checkBox.BackColor = GetContainerBackground(control);
                    break;
                case RadioButton radioButton:
                    radioButton.UseVisualStyleBackColor = false;
                    radioButton.BackColor = GetContainerBackground(control);
                    break;
                default:
                    if (style.SurfaceRole == "CardSurface")
                    {
                        control.BackColor = CardBackground;
                    }

                    break;
            }

            if (control is LinkLabel link)
            {
                link.LinkColor = Accent;
                link.ActiveLinkColor = AccentDark;
                link.VisitedLinkColor = Accent;
            }
        }

        private static void ApplyForeground(Control control, ControlStyle style)
        {
            bool wasApplying = style.Applying;
            style.Applying = true;

            try
            {
                string role = control.Tag is string tag && tag.EndsWith("Text", StringComparison.Ordinal) ? tag : style.TextRole;
                control.ForeColor = role switch
                {
                    "SecondaryText" => TextSecondary,
                    "MutedText" => TextMuted,
                    "AccentText" => Accent,
                    "SuccessText" => Success,
                    "DangerText" => Danger,
                    "WarningText" => Warning,
                    "CustomText" => style.OriginalForeColor,
                    _ => TextPrimary
                };
            }
            finally
            {
                style.Applying = wasApplying;
            }
        }

        private static string GetTextRole(Control control)
        {
            if (control.Tag is string role && role.EndsWith("Text", StringComparison.Ordinal))
            {
                return role;
            }

            return GetColorRole(control.ForeColor) ?? (control.ForeColor.IsSystemColor ? "PrimaryText" : "CustomText");
        }

        private static string? GetColorRole(Color color)
        {
            int argb = color.ToArgb();

            if (argb == TextSecondary.ToArgb())
            {
                return "SecondaryText";
            }

            if (argb == TextMuted.ToArgb())
            {
                return "MutedText";
            }

            if (argb == Success.ToArgb() || argb == Color.DarkGreen.ToArgb() || argb == Color.FromArgb(22, 163, 74).ToArgb())
            {
                return "SuccessText";
            }

            if (argb == Danger.ToArgb() || argb == Color.Firebrick.ToArgb() || argb == Color.FromArgb(220, 38, 38).ToArgb())
            {
                return "DangerText";
            }

            if (argb == Warning.ToArgb() || argb == Color.DarkOrange.ToArgb())
            {
                return "WarningText";
            }

            if (argb == Accent.ToArgb() || argb == AccentDark.ToArgb())
            {
                return "AccentText";
            }

            if (argb == TextPrimary.ToArgb())
            {
                return "PrimaryText";
            }

            return null;
        }

        private static string? GetSurfaceRole(Control control)
        {
            if (control is not CardPanel && control.BackColor.ToArgb() == CardBackground.ToArgb() && control.BackColor != Color.Transparent)
            {
                return "CardSurface";
            }

            return null;
        }

        private static Color GetContainerBackground(Control control)
        {
            for (Control? current = control; current is not null; current = current.Parent)
            {
                if (current is CardPanel or TabPage)
                {
                    return CardBackground;
                }

                if (Equals(current.Tag, "SidebarSurface"))
                {
                    return SidebarBackground;
                }

                if (_styles.TryGetValue(current, out ControlStyle? style) && style.SurfaceRole == "CardSurface")
                {
                    return CardBackground;
                }
            }

            return AppBackground;
        }

        private static void StyleButton(Button button, Color backColor, Color foreColor, Color borderColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = button.Tag is "PrimaryButton" ? IsDarkMode ? Color.FromArgb(53, 109, 211) : Color.FromArgb(29, 78, 216) : AccentSoft;
            button.FlatAppearance.MouseDownBackColor = button.Tag is "PrimaryButton" ? Color.FromArgb(29, 78, 216) : GridSelected;

            if (SystemInformation.HighContrast)
            {
                Color hover = button.Tag is "PrimaryButton" ? SystemColors.Highlight : SystemColors.Window;
                button.FlatAppearance.MouseOverBackColor = hover;
                button.FlatAppearance.MouseDownBackColor = hover;
            }

            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Font = ButtonFont;
            button.Cursor = Cursors.Hand;
        }

        private sealed class ControlStyle
        {
            public bool Applying { get; set; }
            public string TextRole { get; set; } = "PrimaryText";
            public Color OriginalForeColor { get; set; }
            public string? SurfaceRole { get; init; }
            public string? ButtonRole { get; set; }
            public bool Selected { get; set; }
        }
    }
}
