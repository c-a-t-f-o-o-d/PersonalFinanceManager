using System.ComponentModel;
using PersonalFinanceManager.App.UI.Styling;

namespace PersonalFinanceManager.App.UI.Controls
{
    public sealed class SidebarIconButton : Button
    {
        private SidebarIcon _icon;
        private bool _initialized;

        public SidebarIconButton()
        {
            ImageAlign = ContentAlignment.MiddleLeft;
            TextImageRelation = TextImageRelation.ImageBeforeText;
            _initialized = true;
            RefreshIcon();
        }

        [Category("Appearance")]
        [DefaultValue(SidebarIcon.Accounts)]
        public SidebarIcon Icon
        {
            get => _icon;
            set
            {
                if (_icon == value)
                {
                    return;
                }

                _icon = value;
                RefreshIcon();
            }
        }

        internal void RefreshIcon()
        {
            if (!_initialized || IsDisposed || Disposing)
            {
                return;
            }

            int size = LogicalToDeviceUnits(20);
            int textGap = string.IsNullOrEmpty(Text) ? 0 : LogicalToDeviceUnits(6);
            Image? previous = Image;
            Image = SidebarIconRenderer.Render(_icon, size, textGap, DeviceDpi, ForeColor);
            previous?.Dispose();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            RefreshIcon();
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            RefreshIcon();
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            RefreshIcon();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            RefreshIcon();
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            RefreshIcon();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _initialized = false;
                Image? previous = Image;
                Image = null;
                previous?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
