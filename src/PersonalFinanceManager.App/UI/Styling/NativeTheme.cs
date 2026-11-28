using System.Runtime.InteropServices;

namespace PersonalFinanceManager.App.UI.Styling
{
    internal static class NativeTheme
    {
        private const int _immersiveDarkMode = 20;
        private const uint _getMonthCalendar = 0x1008;
        private const uint _setMonthCalendarColor = 0x100A;
        private const uint _getMinimumCalendarRectangle = 0x1009;
        private const uint _getMaximumTodayWidth = 0x1015;

        public static void Apply(Control control)
        {
            if (!OperatingSystem.IsWindows() || !control.IsHandleCreated || control.IsDisposed)
            {
                return;
            }

            bool dark = UiTheme.IsDarkMode && !SystemInformation.HighContrast;

            if (control is Form form && form.TopLevel)
            {
                int enabled = dark ? 1 : 0;
                _ = DwmSetWindowAttribute(form.Handle, _immersiveDarkMode, ref enabled, sizeof(int));
            }

            // Match the native theme classes used by Windows Forms 10 without recreating handles.
            if (control is DateTimePicker picker)
            {
                DateTimePickerTheme.Apply(picker);
                ApplyCalendar(picker);
            }
            else if (control is ComboBox comboBox)
            {
                _ = SetWindowTheme(comboBox.Handle, dark ? "DarkMode_CFD" : null, null);
                ComboBoxInfo info = new() { Size = (uint)Marshal.SizeOf<ComboBoxInfo>() };

                if (GetComboBoxInfo(comboBox.Handle, ref info) && info.List != IntPtr.Zero)
                {
                    _ = SetWindowTheme(info.List, dark ? "DarkMode_Explorer" : null, null);
                }
            }
            else if (control is TabControl)
            {
                _ = SetWindowTheme(control.Handle, null, dark ? "DarkMode::FileExplorerBannerContainer" : null);
            }
            else if (control is ButtonBase or TextBoxBase or ListBox or ScrollableControl or ScrollBar or UpDownBase)
            {
                _ = SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : null, null);
            }

        }

        public static void SetVisualStyles(IntPtr window, bool enabled)
        {
            _ = SetWindowTheme(window, enabled ? null : string.Empty, enabled ? null : string.Empty);
        }

        public static void ApplyCalendar(DateTimePicker picker)
        {
            if (!OperatingSystem.IsWindows() || !picker.IsHandleCreated || picker.IsDisposed)
            {
                return;
            }

            IntPtr pickerHandle = picker.Handle;
            IntPtr calendar = SendMessage(pickerHandle, _getMonthCalendar, IntPtr.Zero, IntPtr.Zero);

            if (calendar != IntPtr.Zero)
            {
                bool dark = UiTheme.IsDarkMode && !SystemInformation.HighContrast;
                SetVisualStyles(calendar, !dark);
                SetCalendarColor(calendar, 0, UiTheme.InputBackground);
                SetCalendarColor(calendar, 1, UiTheme.TextPrimary);
                // The classic calendar also uses its title background color for weekday text.
                Color titleBackground = SystemInformation.HighContrast ? SystemColors.Highlight : dark ? UiTheme.TextMuted : UiTheme.GridSelected;
                Color titleForeground = SystemInformation.HighContrast ? SystemColors.HighlightText : dark ? UiTheme.InputBackground : UiTheme.TextPrimary;
                SetCalendarColor(calendar, 2, titleBackground);
                SetCalendarColor(calendar, 3, titleForeground);
                SetCalendarColor(calendar, 4, UiTheme.InputBackground);
                SetCalendarColor(calendar, 5, UiTheme.TextMuted);
                FitCalendar(picker, calendar);

                // The date picker applies its initial popup bounds after the DropDown notification returns.
                if (!picker.IsDisposed && !picker.Disposing && picker.IsHandleCreated && picker.Handle == pickerHandle)
                {
                    _ = picker.BeginInvoke((Action)(() =>
                    {
                        if (!picker.IsDisposed && !picker.Disposing && picker.IsHandleCreated && picker.Handle == pickerHandle
                            && SendMessage(pickerHandle, _getMonthCalendar, IntPtr.Zero, IntPtr.Zero) == calendar)
                        {
                            FitCalendar(picker, calendar);
                            _ = RedrawWindow(calendar, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0004 | 0x0080 | 0x0100 | 0x0400);
                        }
                    }));
                }
            }
        }

        private static void FitCalendar(DateTimePicker picker, IntPtr calendar)
        {
            NativeRectangle minimum = default;

            if (SendMessage(calendar, _getMinimumCalendarRectangle, IntPtr.Zero, ref minimum) == IntPtr.Zero || !GetWindowRect(calendar, out NativeRectangle bounds))
            {
                return;
            }

            // Disabling visual styles changes the native calendar metrics without resizing its popup.
            int todayWidth = (int)SendMessage(calendar, _getMaximumTodayWidth, IntPtr.Zero, IntPtr.Zero);
            int width = Math.Max(bounds.Right - bounds.Left, Math.Max(minimum.Right, todayWidth));
            int height = Math.Max(bounds.Bottom - bounds.Top, minimum.Bottom);

            IntPtr popup = calendar;
            NativeRectangle popupBounds = bounds;

            if ((GetWindowLong(calendar, -16) & 0x40000000) != 0)
            {
                IntPtr ancestor = GetAncestor(calendar, 2);
                IntPtr form = picker.FindForm()?.Handle ?? IntPtr.Zero;

                // Some Windows versions host the calendar inside a separate native popup.
                // Child positions use parent-client coordinates, so leave the child's origin unchanged.
                if (ancestor == IntPtr.Zero || ancestor == calendar || ancestor == form
                    || (GetWindowLong(ancestor, -16) & unchecked((int)0x80000000)) == 0
                    || !GetWindowRect(ancestor, out popupBounds))
                {
                    _ = SetWindowPos(calendar, IntPtr.Zero, 0, 0, width, height, 0x0002 | 0x0004 | 0x0010);
                    return;
                }

                popup = ancestor;
            }

            int popupWidth = popupBounds.Right - popupBounds.Left + width - (bounds.Right - bounds.Left);
            int popupHeight = popupBounds.Bottom - popupBounds.Top + height - (bounds.Bottom - bounds.Top);
            NativePoint clientOrigin = default;

            if (popup != calendar && GetClientRect(popup, out NativeRectangle client) && ClientToScreen(popup, ref clientOrigin))
            {
                popupWidth = Math.Max(popupWidth, bounds.Left - clientOrigin.X + width + popupBounds.Right - popupBounds.Left - client.Right);
                popupHeight = Math.Max(popupHeight, bounds.Top - clientOrigin.Y + height + popupBounds.Bottom - popupBounds.Top - client.Bottom);
            }

            if (width == bounds.Right - bounds.Left && height == bounds.Bottom - bounds.Top
                && popupWidth == popupBounds.Right - popupBounds.Left && popupHeight == popupBounds.Bottom - popupBounds.Top)
            {
                return;
            }

            int left = picker.DropDownAlign == LeftRightAlignment.Right ? popupBounds.Right - popupWidth : popupBounds.Left;
            int top = popupBounds.Top;

            if (GetWindowRect(picker.Handle, out NativeRectangle pickerBounds) && popupBounds.Bottom <= pickerBounds.Top)
            {
                top = popupBounds.Bottom - popupHeight;
            }

            Rectangle workingArea = Screen.FromHandle(picker.Handle).WorkingArea;
            left = Math.Clamp(left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - popupWidth));
            top = Math.Clamp(top, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - popupHeight));
            _ = SetWindowPos(popup, IntPtr.Zero, left, top, popupWidth, popupHeight, 0x0004 | 0x0010);

            if (popup != calendar)
            {
                _ = SetWindowPos(calendar, IntPtr.Zero, 0, 0, width, height, 0x0002 | 0x0004 | 0x0010);
            }
        }

        private static void SetCalendarColor(IntPtr calendar, int portion, Color color)
        {
            _ = SendMessage(calendar, _setMonthCalendarColor, (IntPtr)portion, (IntPtr)ColorTranslator.ToWin32(color));
        }

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int SetWindowTheme(IntPtr window, string? applicationName, string? classNames);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetComboBoxInfo(IntPtr window, ref ComboBoxInfo info);

        [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, ref NativeRectangle rectangle);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr window, out NativeRectangle rectangle);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int left, int top, int width, int height, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RedrawWindow(IntPtr window, IntPtr updateRectangle, IntPtr updateRegion, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ComboBoxInfo
        {
            public uint Size;
            public NativeRectangle ItemBounds;
            public NativeRectangle ButtonBounds;
            public uint ButtonState;
            public IntPtr ComboBox;
            public IntPtr Item;
            public IntPtr List;
        }
    }
}
