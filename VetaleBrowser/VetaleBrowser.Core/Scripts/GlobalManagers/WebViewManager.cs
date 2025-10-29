using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Diagnostics;
using WebViewControl;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers
{
    /// <summary>
    /// Simple WebView manager that wraps WebViewControl and provides navigation methods.
    /// </summary>
    public class WebViewManager : IDisposable
    {
        private WebView? _webView;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initialize the manager with an existing WebView control instance.
        /// </summary>
        public void Initialize(WebView webView)
        {
            if (webView == null) throw new ArgumentNullException(nameof(webView));
            
            _webView = webView;
            _isInitialized = true;
            Debug.WriteLine("WebViewManager: Initialized with WebView instance");
        }

        /// <summary>
        /// Navigate to the specified URL.
        /// </summary>
        public async Task NavigateAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentNullException(nameof(url));
            if (!_isInitialized || _webView == null)
            {
                Debug.WriteLine("WebViewManager: Not initialized, cannot navigate");
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    Debug.WriteLine($"WebViewManager: Navigating to {url}");
                    _webView.Address = url;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WebViewManager.NavigateAsync failed: {ex}");
                }
            });
        }

        /// <summary>
        /// Get current URL.
        /// </summary>
        public string? GetCurrentUrl()
        {
            if (!_isInitialized || _webView == null) return null;
            return _webView.Address;
        }

        /// <summary>
        /// Go back in navigation history.
        /// </summary>
        public void GoBack()
        {
            if (_webView?.CanGoBack == true)
            {
                Dispatcher.UIThread.Post(() => _webView.GoBack());
            }
        }

        /// <summary>
        /// Go forward in navigation history.
        /// </summary>
        public void GoForward()
        {
            if (_webView?.CanGoForward == true)
            {
                Dispatcher.UIThread.Post(() => _webView.GoForward());
            }
        }

        /// <summary>
        /// Reload current page.
        /// </summary>
        public void Reload()
        {
            if (_isInitialized && _webView != null)
            {
                Dispatcher.UIThread.Post(() => _webView.Reload());
            }
        }

        public void Dispose()
        {
            try
            {
                if (_webView != null)
                {
                    _webView.Dispose();
                    _webView = null;
                }
                _isInitialized = false;
                Debug.WriteLine("WebViewManager: Disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebViewManager.Dispose failed: {ex}");
            }
        }
    }
}
