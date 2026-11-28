using PersonalFinanceManager.App.UI.Styling;

namespace PersonalFinanceManager.App.UI.Controls
{
    public sealed class PageHost : Panel
    {
        private bool _resizingContent;

        public PageHost(Control view)
        {
            ArgumentNullException.ThrowIfNull(view);
            HostedView = view;
            ScrollMode = view is IPageLayoutControl layout ? layout.ScrollMode : PageScrollMode.Viewport;
            Name = "PageHost";
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            BackColor = UiTheme.AppBackground;
            AutoScroll = ScrollMode == PageScrollMode.Page;
            view.Margin = Padding.Empty;
            view.Location = Point.Empty;
            view.BackColor = UiTheme.AppBackground;

            if (view is ScrollableControl scrollable)
            {
                scrollable.AutoScroll = false;
            }

            view.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            view.Dock = ScrollMode == PageScrollMode.Viewport ? DockStyle.Fill : DockStyle.None;
            Controls.Add(view);
        }

        public Control HostedView { get; }
        public PageScrollMode ScrollMode { get; }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (ScrollMode == PageScrollMode.Page && !_resizingContent && HostedView is not null && !HostedView.IsDisposed)
            {
                _resizingContent = true;

                try
                {
                    int width = Math.Max(0, ClientSize.Width);
                    HostedView.Width = width;
                    int minimumHeight = HostedView is IPageLayoutControl layout ? HostedView.LogicalToDeviceUnits(layout.PreferredPageHeight) : 0;
                    int preferredHeight = Math.Max(minimumHeight, HostedView.GetPreferredSize(new Size(width, 0)).Height);
                    AutoScrollMinSize = new Size(0, preferredHeight);
                    HostedView.Size = new Size(width, Math.Max(ClientSize.Height, preferredHeight));
                    HostedView.Location = AutoScrollPosition;
                }
                finally
                {
                    _resizingContent = false;
                }
            }

            base.OnLayout(levent);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            PerformLayout();
        }
    }
}
