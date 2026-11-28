using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PersonalFinanceManager.App.UI.Styling
{
    internal static class DateTimePickerTheme
    {
        private const uint _getPickerInfo = 0x100E;
        private static readonly ConditionalWeakTable<DateTimePicker, PickerWindows> _pickers = new();

        public static void Apply(DateTimePicker picker)
        {
            PickerWindows windows = _pickers.GetValue(picker, static control => new PickerWindows(control));
            windows.Refresh();
        }

        private static bool UseDarkRendering => UiTheme.IsDarkMode && !SystemInformation.HighContrast;

        private sealed class PickerWindows
        {
            private readonly DateTimePicker _picker;
            private readonly Dictionary<IntPtr, NativeFieldWindow> _windows = new();
            private bool _refreshing;

            public PickerWindows(DateTimePicker picker)
            {
                _picker = picker;
                picker.HandleDestroyed += (_, _) => ReleaseWindows();
                picker.Disposed += (_, _) => ReleaseWindows();
            }

            public void Refresh()
            {
                if (_refreshing || !_picker.IsHandleCreated || _picker.IsDisposed)
                {
                    return;
                }

                _refreshing = true;

                try
                {
                    foreach (IntPtr released in _windows.Where(pair => pair.Value.Handle == IntPtr.Zero).Select(pair => pair.Key).ToArray())
                    {
                        _windows.Remove(released);
                    }

                    Attach(_picker.Handle, Refresh);
                    DateTimePickerInfo info = new() { Size = (uint)Marshal.SizeOf<DateTimePickerInfo>() };
                    _ = SendPickerMessage(_picker.Handle, _getPickerInfo, IntPtr.Zero, ref info);
                    Attach(info.Edit, null);
                    Attach(info.UpDown, null);
                }
                finally
                {
                    _refreshing = false;
                }
            }

            private void Attach(IntPtr handle, Action? beforePaint)
            {
                if (handle == IntPtr.Zero)
                {
                    return;
                }

                if (!_windows.TryGetValue(handle, out NativeFieldWindow? window) || window.Handle != handle)
                {
                    window?.ReleaseHandle();
                    window = new NativeFieldWindow(handle, beforePaint);
                    _windows[handle] = window;
                }

                window.ApplyVisualStyles(!UseDarkRendering);
            }

            private void ReleaseWindows()
            {
                foreach (NativeFieldWindow window in _windows.Values)
                {
                    if (window.Handle != IntPtr.Zero)
                    {
                        window.ReleaseHandle();
                    }
                }

                _windows.Clear();
            }
        }

        private sealed class NativeFieldWindow : NativeWindow
        {
            private const int _paint = 0x000F;
            private const int _eraseBackground = 0x0014;
            private const int _print = 0x0317;
            private const int _printClient = 0x0318;
            private const int _printFlags = 0x0002 | 0x0004 | 0x0008 | 0x0010;
            private readonly Action? _beforePaint;
            private bool? _visualStylesEnabled;

            [ThreadStatic]
            private static bool _capturingNative;

            public NativeFieldWindow(IntPtr handle, Action? beforePaint)
            {
                _beforePaint = beforePaint;
                AssignHandle(handle);
            }

            public void ApplyVisualStyles(bool enabled)
            {
                if (_visualStylesEnabled == enabled)
                {
                    return;
                }

                _visualStylesEnabled = enabled;
                NativeTheme.SetVisualStyles(Handle, enabled);
            }

            protected override void WndProc(ref Message message)
            {
                if (!UseDarkRendering || _capturingNative)
                {
                    base.WndProc(ref message);
                    return;
                }

                if (message.Msg == _eraseBackground)
                {
                    message.Result = (IntPtr)1;
                    return;
                }

                if (message.Msg is _paint or _print or _printClient)
                {
                    _beforePaint?.Invoke();

                    if (message.Msg == _paint)
                    {
                        PaintStruct paint = new() { Reserved = new byte[32] };
                        IntPtr deviceContext = BeginPaint(Handle, ref paint);

                        try
                        {
                            DrawNativeField(deviceContext, clientOnly: true);
                        }
                        finally
                        {
                            _ = EndPaint(Handle, ref paint);
                        }
                    }
                    else
                    {
                        DrawNativeField(message.WParam, clientOnly: message.Msg == _printClient);
                    }

                    message.Result = IntPtr.Zero;
                    return;
                }

                int nativeMessage = message.Msg;
                base.WndProc(ref message);

                if (Handle != IntPtr.Zero && ChangesDisplay(nativeMessage))
                {
                    _ = RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0080 | 0x0100);
                }
            }

            private void DrawNativeField(IntPtr deviceContext, bool clientOnly)
            {
                if (deviceContext == IntPtr.Zero || !GetWindowRect(Handle, out NativeRectangle window))
                {
                    return;
                }

                int width = window.Right - window.Left;
                int height = window.Bottom - window.Top;
                NativePoint origin = default;

                if (width <= 0 || height <= 0 || !ClientToScreen(Handle, ref origin))
                {
                    return;
                }

                using Bitmap native = new(width, height, PixelFormat.Format32bppRgb);

                using (Graphics source = Graphics.FromImage(native))
                {
                    source.Clear(Color.White);
                    IntPtr sourceContext = source.GetHdc();
                    _capturingNative = true;

                    try
                    {
                        Message print = Message.Create(Handle, _print, sourceContext, (IntPtr)_printFlags);
                        base.WndProc(ref print);
                    }
                    finally
                    {
                        _capturingNative = false;
                        source.ReleaseHdc(sourceContext);
                    }
                }

                using ImageAttributes attributes = new();
                attributes.SetColorMatrix(CreatePaletteTransform());
                using Graphics target = Graphics.FromHdc(deviceContext);
                int left = clientOnly ? window.Left - origin.X : 0;
                int top = clientOnly ? window.Top - origin.Y : 0;
                target.DrawImage(native, new Rectangle(left, top, native.Width, native.Height), 0, 0, native.Width, native.Height, GraphicsUnit.Pixel, attributes);
            }

            private static ColorMatrix CreatePaletteTransform()
            {
                Color foreground = UiTheme.TextPrimary;
                Color background = UiTheme.InputBackground;
                float red = (foreground.R - background.R) / 255F;
                float green = (foreground.G - background.G) / 255F;
                float blue = (foreground.B - background.B) / 255F;

                // Recolor native glyphs and selection geometry without replacing date-segment input.
                return new ColorMatrix(new[]
                {
                    new[] { -0.2126F * red, -0.2126F * green, -0.2126F * blue, 0F, 0F },
                    new[] { -0.7152F * red, -0.7152F * green, -0.7152F * blue, 0F, 0F },
                    new[] { -0.0722F * red, -0.0722F * green, -0.0722F * blue, 0F, 0F },
                    new[] { 0F, 0F, 0F, 1F, 0F },
                    new[] { foreground.R / 255F, foreground.G / 255F, foreground.B / 255F, 0F, 1F }
                });
            }

            private static bool ChangesDisplay(int message)
            {
                return message is 0x0007 or 0x0008 or 0x000A or 0x000C
                    or 0x0100 or 0x0101 or 0x0102 or 0x0104 or 0x0105
                    or 0x0200 or 0x0201 or 0x0202 or 0x020A
                    or 0x1002 or 0x1004 or 0x1005 or 0x1032;
            }
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr SendPickerMessage(IntPtr window, uint message, IntPtr wParam, ref DateTimePickerInfo info);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr BeginPaint(IntPtr window, ref PaintStruct paint);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EndPaint(IntPtr window, ref PaintStruct paint);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RedrawWindow(IntPtr window, IntPtr updateRectangle, IntPtr updateRegion, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DateTimePickerInfo
        {
            public uint Size;
            public NativeRectangle CheckBounds;
            public uint CheckState;
            public NativeRectangle ButtonBounds;
            public uint ButtonState;
            public IntPtr Edit;
            public IntPtr UpDown;
            public IntPtr DropDown;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PaintStruct
        {
            public IntPtr DeviceContext;
            [MarshalAs(UnmanagedType.Bool)] public bool Erase;
            public NativeRectangle Bounds;
            [MarshalAs(UnmanagedType.Bool)] public bool Restore;
            [MarshalAs(UnmanagedType.Bool)] public bool Incremental;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved;
        }
    }
}
