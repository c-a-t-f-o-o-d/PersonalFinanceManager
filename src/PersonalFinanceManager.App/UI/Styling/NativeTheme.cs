using System.Runtime.InteropServices;

namespace PersonalFinanceManager.App.UI.Styling
{
    internal static class NativeTheme
    {
        private const int _immersiveDarkMode = 20;
        private const uint _getMonthCalendar = 0x1008;

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
            if (control is ComboBox comboBox)
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
            else if (control is ButtonBase or TextBoxBase or ListBox or DateTimePicker or ScrollableControl or ScrollBar or UpDownBase)
            {
                _ = SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : null, null);
            }

            if (control is DateTimePicker picker)
            {
                ApplyCalendar(picker);
            }
        }

        public static void ApplyCalendar(DateTimePicker picker)
        {
            if (!OperatingSystem.IsWindows() || !picker.IsHandleCreated || picker.IsDisposed)
            {
                return;
            }

            IntPtr calendar = SendMessage(picker.Handle, _getMonthCalendar, IntPtr.Zero, IntPtr.Zero);

            if (calendar != IntPtr.Zero)
            {
                bool dark = UiTheme.IsDarkMode && !SystemInformation.HighContrast;
                _ = SetWindowTheme(calendar, dark ? "DarkMode_Explorer" : null, null);
            }
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
