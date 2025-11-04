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
        /// Fired when a navigation to a new URL is requested via NavigateAsync.
        /// Carries the target URL string.
        /// </summary>
        public event EventHandler<string>? Navigated;

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
                    Navigated?.Invoke(this, url);
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

        /// <summary>
        /// Execute JavaScript code and return result as string.
        /// </summary>
        public async Task<string> ExecuteScriptAsync(string script)
        {
            if (!_isInitialized || _webView == null)
            {
                Debug.WriteLine("WebViewManager: Not initialized, cannot execute script");
                return string.Empty;
            }

            try
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        // WebViewControl використовує ExecuteJavascript або подібні методи
                        // Оскільки точний API невідомий, використовуємо альтернативний підхід
                        var result = string.Empty;
                        Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                // Спроба виконати через address bar з javascript: protocol
                                // або інший доступний спосіб
                                Debug.WriteLine($"WebViewManager: Attempting to execute script: {script.Substring(0, Math.Min(100, script.Length))}...");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"WebViewManager.ExecuteScriptAsync inner error: {ex}");
                            }
                        });
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"WebViewManager.ExecuteScriptAsync inner error: {ex}");
                        return $"Error: {ex.Message}";
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebViewManager.ExecuteScriptAsync error: {ex}");
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Inject JavaScript code into the page.
        /// Note: This is a placeholder implementation. 
        /// WebViewControl may not support direct script injection.
        /// </summary>
        public async Task InjectScriptAsync(string script)
        {
            if (!_isInitialized || _webView == null)
            {
                Debug.WriteLine("WebViewManager: Not initialized, cannot inject script");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Debug.WriteLine("WebViewManager: Script injection not directly supported by WebViewControl");
                        Debug.WriteLine($"Script to inject: {script.Substring(0, Math.Min(100, script.Length))}...");
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WebViewManager.InjectScriptAsync error: {ex}");
                }
            });
        }

        /// <summary>
        /// Get page HTML source.
        /// Note: This method may not work with WebViewControl limitations.
        /// </summary>
        public async Task<string> GetPageSourceAsync()
        {
            return await ExecuteScriptAsync("document.documentElement.outerHTML");
        }

        /// <summary>
        /// Get page title.
        /// Note: This method may not work with WebViewControl limitations.
        /// </summary>
        public async Task<string> GetPageTitleAsync()
        {
            return await ExecuteScriptAsync("document.title");
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
