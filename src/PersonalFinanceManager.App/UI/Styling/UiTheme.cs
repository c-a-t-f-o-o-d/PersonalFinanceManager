namespace PersonalFinanceManager.App.UI.Styling
{
    public static class UiTheme
    {
        public static readonly Color AppBackground = Color.FromArgb(245, 247, 251);
        public static readonly Color SidebarBackground = Color.FromArgb(250, 252, 255);
        public static readonly Color CardBackground = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(220, 226, 235);
        public static readonly Color TextPrimary = Color.FromArgb(25, 34, 45);
        public static readonly Color TextSecondary = Color.FromArgb(91, 103, 119);
        public static readonly Color TextMuted = Color.FromArgb(123, 135, 150);
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentDark = Color.FromArgb(29, 78, 216);
        public static readonly Color AccentSoft = Color.FromArgb(228, 238, 255);
        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color GridHeader = Color.FromArgb(246, 249, 253);
        public static readonly Color GridSelected = Color.FromArgb(219, 234, 254);
        public static readonly Color GridLine = Color.FromArgb(226, 232, 240);
        public static Font TitleFont => new("Segoe UI", 18F, FontStyle.Bold);
        public static Font PageSubtitleFont => new("Segoe UI", 9.5F, FontStyle.Regular);
        public static Font SectionTitleFont => new("Segoe UI", 11F, FontStyle.Bold);
        public static Font BodyFont => new("Segoe UI", 9F, FontStyle.Regular);
        public static Font SmallFont => new("Segoe UI", 8.5F, FontStyle.Regular);
        public static Font ButtonFont => new("Segoe UI Semibold", 9F, FontStyle.Regular);

        public static void StylePrimaryButton(Button button)
        {
            button.Tag = "PrimaryButton";
            StyleButton(button, Accent, Color.White, AccentDark);
        }

        public static void StyleSecondaryButton(Button button)
        {
            StyleButton(button, CardBackground, TextPrimary, CardBorder);
        }

        public static void StyleNavButton(Button button, bool selected)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = ButtonFont;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.BackColor = selected ? AccentSoft : SidebarBackground;
            button.ForeColor = selected ? AccentDark : TextPrimary;
            button.Cursor = Cursors.Hand;
            button.Padding = new Padding(14, 0, 0, 0);
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
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Regular);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = GridHeader;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.DefaultCellStyle.Font = BodyFont;
            grid.DefaultCellStyle.BackColor = CardBackground;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = GridSelected;
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 255);
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Height = 30;
            grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, 32);
        }

        public static void ApplyTo(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);
            control.Font = BodyFont;
            control.ForeColor = TextPrimary;

            if (control is Button button)
            {
                button.AutoEllipsis = true;
                button.MinimumSize = new Size(Math.Min(button.Width, 1), Math.Max(30, button.Height));

                if (button.Height < 30)
                {
                    button.Height = 30;
                }

                if (string.Equals(Convert.ToString(button.Tag), "PrimaryButton", StringComparison.Ordinal))
                {
                    StylePrimaryButton(button);
                }
                else
                {
                    StyleSecondaryButton(button);
                }
            }
            else if (control is DataGridView grid)
            {
                StyleGrid(grid);
            }
            else if (control is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = Color.White;
                textBox.ForeColor = TextPrimary;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = Color.White;
                comboBox.ForeColor = TextPrimary;
                comboBox.IntegralHeight = false;
                comboBox.DropDownHeight = Math.Max(comboBox.DropDownHeight, 240);
            }
            else if (control is NumericUpDown numeric)
            {
                numeric.BorderStyle = BorderStyle.FixedSingle;
                numeric.BackColor = Color.White;
                numeric.ForeColor = TextPrimary;
            }
            else if (control is DateTimePicker picker)
            {
                picker.CalendarForeColor = TextPrimary;
                picker.CalendarMonthBackground = Color.White;
            }
            else if (control is Label label)
            {
                label.AutoEllipsis = true;
                string? labelRole = Convert.ToString(label.Tag);

                if (string.Equals(labelRole, "SecondaryText", StringComparison.Ordinal))
                {
                    label.ForeColor = TextSecondary;
                }
                else if (string.Equals(labelRole, "MutedText", StringComparison.Ordinal))
                {
                    label.ForeColor = TextMuted;
                }
                else
                {
                    label.ForeColor = TextPrimary;
                }
            }
            else if (control.GetType().Name == "CardPanel")
            {
                control.BackColor = CardBackground;
            }
            else if (control is TabPage)
            {
                control.BackColor = CardBackground;
                control.Padding = new Padding(8);
            }
            else if (control is TabControl tabControl)
            {
                tabControl.Padding = new Point(Math.Max(tabControl.Padding.X, 12), Math.Max(tabControl.Padding.Y, 6));
                tabControl.BackColor = IsInsideCardContext(control) ? CardBackground : AppBackground;
            }
            else if (control is CheckedListBox checkedListBox)
            {
                checkedListBox.IntegralHeight = false;
                checkedListBox.HorizontalScrollbar = false;
                checkedListBox.BackColor = IsInsideCardContext(control) ? CardBackground : Color.White;
                checkedListBox.ForeColor = TextPrimary;
            }
            else if (control is UserControl userControl)
            {
                userControl.AutoScroll = false;
                userControl.BackColor = IsInsideCardContext(control) ? CardBackground : AppBackground;
            }
            else if (control is Form form)
            {
                form.AutoScaleMode = AutoScaleMode.Dpi;
                form.AutoScroll = false;
                form.BackColor = AppBackground;
            }
            else if (control is FlowLayoutPanel flowLayoutPanel)
            {
                string? scrollRole = Convert.ToString(flowLayoutPanel.Tag);
                flowLayoutPanel.AutoScroll = string.Equals(scrollRole, "AllowScroll", StringComparison.Ordinal) || string.Equals(scrollRole, "Scrollable", StringComparison.Ordinal);
                control.BackColor = IsInsideCardContext(control) ? CardBackground : AppBackground;
            }
            else if (control is Panel or TableLayoutPanel)
            {
                control.BackColor = IsInsideCardContext(control) ? CardBackground : AppBackground;
            }

            foreach (Control child in control.Controls.Cast<Control>())
            {
                ApplyTo(child);
            }
        }

        private static bool IsInsideCardContext(Control control)
        {
            Control? current = control.Parent;

            while (current is not null)
            {
                if (current.GetType().Name == "CardPanel" || current is TabPage)
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static void StyleButton(Button button, Color backColor, Color foreColor, Color borderColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Font = ButtonFont;
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(5, 4, 5, 4);
        }
    }
}
