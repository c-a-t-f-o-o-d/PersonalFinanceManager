using PersonalFinanceManager.App.UI.Styling;

namespace PersonalFinanceManager.App.UI.Controls
{
    public abstract class AsyncPageControl : UserControl
    {
        private readonly CancellationTokenSource _pageLifetime = new();
        private readonly CancellationToken _pageLifetimeToken;
        private readonly Dictionary<string, LoadRequest> _requests = new(StringComparer.Ordinal);
        private readonly Label _loadStatus = new()
        {
            Name = "PageLoadStatus",
            Dock = DockStyle.Bottom,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleRole = AccessibleRole.StatusBar,
            Visible = false
        };
        private bool _initialLoadStarted;
        private bool _lifetimeEnded;

        protected AsyncPageControl()
        {
            _pageLifetimeToken = _pageLifetime.Token;
            _loadStatus.Height = LogicalToDeviceUnits(28);
            Controls.Add(_loadStatus);
        }

        protected bool IsPageActive => !_lifetimeEnded && !IsDisposed && !Disposing;
        protected CancellationToken PageLifetimeToken => _pageLifetimeToken;

        protected bool TryBeginInitialLoad()
        {
            if (!IsPageActive || _initialLoadStarted)
            {
                return false;
            }

            _initialLoadStarted = true;
            return true;
        }

        protected async Task<bool> LoadLatestAsync<T>(string key, string loadingMessage, Func<CancellationToken, Task<T>> load, Action<T> apply)
        {
            if (!IsPageActive)
            {
                return false;
            }

            _requests.TryGetValue(key, out LoadRequest? previous);
            LoadRequest request = new(_pageLifetimeToken, loadingMessage);
            _requests[key] = request;
            previous?.Cancel();
            UpdateLoadStatus();

            try
            {
                T result = await load(request.Token).ConfigureAwait(true);

                if (IsCurrent(key, request) && !request.Token.IsCancellationRequested)
                {
                    apply(result);

                    if (IsCurrent(key, request))
                    {
                        _requests.Remove(key);
                    }

                    return true;
                }

                return false;
            }
            catch (OperationCanceledException) when (request.Token.IsCancellationRequested)
            {
                if (IsCurrent(key, request))
                {
                    _requests.Remove(key);
                }

                return false;
            }
            catch (Exception)
            {
                if (IsCurrent(key, request))
                {
                    request.Failed = true;
                }

                throw;
            }
            finally
            {
                request.Dispose();
                UpdateLoadStatus();
            }
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            _loadStatus.Height = LogicalToDeviceUnits(28);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_lifetimeEnded)
            {
                _lifetimeEnded = true;
                _pageLifetime.Cancel();
                _pageLifetime.Dispose();
                _requests.Clear();
            }

            base.Dispose(disposing);
        }

        private bool IsCurrent(string key, LoadRequest request)
        {
            return IsPageActive && _requests.TryGetValue(key, out LoadRequest? current) && ReferenceEquals(current, request);
        }

        private void UpdateLoadStatus()
        {
            if (!IsPageActive)
            {
                return;
            }

            LoadRequest? failed = _requests.Values.FirstOrDefault(request => request.Failed);
            _loadStatus.Text = failed is not null
                ? "Some data could not be loaded. Refresh or reopen this page to try again."
                : string.Join("  ", _requests.Values.Select(request => request.Message));
            _loadStatus.AccessibleName = _loadStatus.Text;
            UiTheme.SetTextRole(_loadStatus, failed is not null ? "WarningText" : "SecondaryText");
            _loadStatus.Visible = _requests.Count > 0;
            _loadStatus.SendToBack();
        }

        private sealed class LoadRequest : IDisposable
        {
            private CancellationTokenSource? _cancellation;

            public LoadRequest(CancellationToken pageLifetimeToken, string message)
            {
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(pageLifetimeToken);
                Token = _cancellation.Token;
                Message = message;
            }

            public CancellationToken Token { get; }
            public string Message { get; }
            public bool Failed { get; set; }

            public void Cancel()
            {
                _cancellation?.Cancel();
            }

            public void Dispose()
            {
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }
    }
}
