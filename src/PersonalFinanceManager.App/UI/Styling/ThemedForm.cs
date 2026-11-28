namespace PersonalFinanceManager.App.UI.Styling
{
    public class ThemedForm : Form
    {
        private bool _themeSubscribed;
        private bool _applyingTheme;

        public ThemedForm()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = UiTheme.BodyFont;
            BackColor = UiTheme.AppBackground;
        }

        protected override void OnLoad(EventArgs e)
        {
            if (!_themeSubscribed)
            {
                UiTheme.ThemeChanged += HandleThemeChanged;
                _themeSubscribed = true;
            }

            OnThemeChanged(EventArgs.Empty);
            base.OnLoad(e);
        }

        protected virtual void OnThemeChanged(EventArgs e)
        {
            if (_applyingTheme)
            {
                return;
            }

            _applyingTheme = true;
            SuspendLayout();

            try
            {
                UiTheme.ApplyTo(this);
            }
            finally
            {
                ResumeLayout(true);
                _applyingTheme = false;
            }
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);

            if (IsHandleCreated && !IsDisposed && !Disposing)
            {
                OnThemeChanged(e);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _themeSubscribed)
            {
                UiTheme.ThemeChanged -= HandleThemeChanged;
                _themeSubscribed = false;
            }

            base.Dispose(disposing);
        }

        private void HandleThemeChanged(object? sender, EventArgs e)
        {
            if (!IsDisposed && !Disposing)
            {
                OnThemeChanged(e);
            }
        }
    }
}
