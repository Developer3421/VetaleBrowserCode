using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using Avalonia.Threading;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers
{
    /// <summary>
    /// WebView error handler for intercepting and localizing loading errors.
    /// Uses Title and Address monitoring to detect chrome-error pages.
    /// </summary>
    public class WebViewErrorHandler : IDisposable
    {
        private readonly IBrowserView _webView;
        private bool _isAttached;
        private string? _pendingUrl;
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private string? _lastErrorUrl;
        private int _lastErrorCode;
        private DateTime _lastErrorTime;
        private string? _lastValidUrl;
        
        // Indicates that an explicit error was already received from an event (LoadError etc.)
        // for the current navigation, so no need to duplicate it via Title/Address/ConsoleMessage fallback
        private bool _hasExplicitErrorForCurrentNav;

        /// <summary>
        /// Error occurrence event with localized content
        /// </summary>
        public event EventHandler<BrowserErrorEventArgs>? ErrorOccurred;

        public WebViewErrorHandler(IBrowserView webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        }

        /// <summary>
        /// Attaches error handlers to the WebView
        /// </summary>
        public void Attach()
        {
            if (_isAttached) return;

            try
            {
                // First try to subscribe to the WebView's own events
                AttachWebViewEvents();
                
                // Then look for the internal AvaloniaCefBrowser
                AttachAvaloniaCefBrowserEvents();
                
                // Keep PropertyChanged as fallback (update _lastValidUrl + chrome-error/title hack as last line of defense)
                _webView.PropertyChanged += OnWebViewPropertyChanged;

                _isAttached = true;
                Debug.WriteLine("[WebViewErrorHandler] Error handlers attached (AvaloniaCefBrowser + PropertyChanged)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] Failed to attach handlers: {ex}");
            }
        }
        
        /// <summary>
        /// Try to subscribe to events directly on the WebView
        /// </summary>
        private void AttachWebViewEvents()
        {
            var webViewType = _webView.InnerView.GetType();
            Debug.WriteLine($"[WebViewErrorHandler] Attaching to WebView events...");
            
            // Print all WebView events for diagnostics
            Debug.WriteLine("[WebViewErrorHandler] WebView events:");
            foreach (var evt in webViewType.GetEvents(BindingFlags.Instance | BindingFlags.Public))
            {
                Debug.WriteLine($"[WebViewErrorHandler]   Event: {evt.Name}");
            }
            
            // WebView from WebViewControl-Avalonia may have its own error events
            // Try different name variants
            string[] errorEventNames = { 
                "LoadError", "LoadFailed", "NavigationError", "PageLoadError",
                "BrowserLoadError", "OnLoadError", "LoadingError" 
            };
            
            foreach (var eventName in errorEventNames)
            {
                TrySubscribe(_webView.InnerView, webViewType, eventName, nameof(OnBrowserLoadError));
            }
            
            TrySubscribe(_webView.InnerView, webViewType, "UnhandledException", nameof(OnBrowserUnhandledException));
            TrySubscribe(_webView.InnerView, webViewType, "JavascriptUncaughtException", nameof(OnJavascriptUncaughtException));
            TrySubscribe(_webView.InnerView, webViewType, "ConsoleMessage", nameof(OnConsoleMessage));
        }

        /// <summary>
        /// Searches inside WebView for an AvaloniaCefBrowser instance and subscribes to LoadError, UnhandledException, JavascriptUncaughtException, ConsoleMessage
        /// </summary>
        private void AttachAvaloniaCefBrowserEvents()
        {
            // Many CEF wrappers have a "Browser" property/field inside. Adjust the name if needed.
            var webViewType = _webView.InnerView.GetType();
            Debug.WriteLine($"[WebViewErrorHandler] WebView type: {webViewType.FullName}");
            
            // Print all properties and fields for diagnostics
            Debug.WriteLine("[WebViewErrorHandler] Available properties:");
            foreach (var prop in webViewType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Debug.WriteLine($"[WebViewErrorHandler]   Property: {prop.Name} ({prop.PropertyType.Name})");
            }
            Debug.WriteLine("[WebViewErrorHandler] Available fields:");
            foreach (var field in webViewType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Debug.WriteLine($"[WebViewErrorHandler]   Field: {field.Name} ({field.FieldType.Name})");
            }

            object? browser = null;
            
            // Try different property/field names
            string[] browserNames = { "Browser", "_browser", "browser", "chromiumBrowser", "_chromiumBrowser", "InternalBrowser", "_internalBrowser", "CefBrowser", "_cefBrowser" };
            
            foreach (var name in browserNames)
            {
                var browserProp = webViewType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (browserProp != null)
                {
                    browser = browserProp.GetValue(_webView.InnerView);
                    if (browser != null)
                    {
                        Debug.WriteLine($"[WebViewErrorHandler] Found browser via property '{name}', type: {browser.GetType().FullName}");
                        break;
                    }
                }
                
                var browserField = webViewType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (browserField != null)
                {
                    browser = browserField.GetValue(_webView.InnerView);
                    if (browser != null)
                    {
                        Debug.WriteLine($"[WebViewErrorHandler] Found browser via field '{name}', type: {browser.GetType().FullName}");
                        break;
                    }
                }
            }
            
            // If not found by name, search by type (contains CefBrowser or AvaloniaCefBrowser in type name)
            if (browser == null)
            {
                Debug.WriteLine("[WebViewErrorHandler] Searching by type pattern...");
                foreach (var field in webViewType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var val = field.GetValue(_webView.InnerView);
                    if (val != null)
                    {
                        var typeName = val.GetType().FullName ?? "";
                        if (typeName.Contains("CefBrowser") || typeName.Contains("Chromium") || typeName.Contains("Browser"))
                        {
                            browser = val;
                            Debug.WriteLine($"[WebViewErrorHandler] Found browser by type pattern in field '{field.Name}', type: {typeName}");
                            break;
                        }
                    }
                }
                
                if (browser == null)
                {
                    foreach (var prop in webViewType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        try
                        {
                            var val = prop.GetValue(_webView.InnerView);
                            if (val != null)
                            {
                                var typeName = val.GetType().FullName ?? "";
                                if (typeName.Contains("CefBrowser") || typeName.Contains("Chromium") || typeName.Contains("Browser"))
                                {
                                    browser = val;
                                    Debug.WriteLine($"[WebViewErrorHandler] Found browser by type pattern in property '{prop.Name}', type: {typeName}");
                                    break;
                                }
                            }
                        }
                        catch { /* ignore errors when reading properties */ }
                    }
                }
            }

            if (browser == null)
            {
                Debug.WriteLine("[WebViewErrorHandler] AvaloniaCefBrowser not found inside WebView (property/field 'Browser' missing or null)");
                Debug.WriteLine("[WebViewErrorHandler] Will rely on PropertyChanged fallback for error detection");
                return;
            }

            // Subscribe as directly as possible, like in your example, but via reflection to avoid pulling EventArgs types into Core.
            var browserType = browser.GetType();
            Debug.WriteLine($"[WebViewErrorHandler] Browser type: {browserType.FullName}");
            
            // Print available events
            Debug.WriteLine("[WebViewErrorHandler] Available events on browser:");
            foreach (var evt in browserType.GetEvents(BindingFlags.Instance | BindingFlags.Public))
            {
                Debug.WriteLine($"[WebViewErrorHandler]   Event: {evt.Name}");
            }

            TrySubscribe(browser, browserType, "LoadError", nameof(OnBrowserLoadError));
            TrySubscribe(browser, browserType, "UnhandledException", nameof(OnBrowserUnhandledException));
            TrySubscribe(browser, browserType, "JavascriptUncaughtException", nameof(OnJavascriptUncaughtException));
            TrySubscribe(browser, browserType, "ConsoleMessage", nameof(OnConsoleMessage));
        }

        private void TrySubscribe(object browser, Type browserType, string eventName, string handlerName)
        {
            try
            {
                var evt = browserType.GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (evt == null)
                {
                    Debug.WriteLine($"[WebViewErrorHandler] Event '{eventName}' not found on AvaloniaCefBrowser");
                    return;
                }

                var handlerMethod = typeof(WebViewErrorHandler).GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (handlerMethod == null)
                {
                    Debug.WriteLine($"[WebViewErrorHandler] Handler method '{handlerName}' not found on WebViewErrorHandler");
                    return;
                }

                if (evt.EventHandlerType == null)
                {
                    Debug.WriteLine($"[WebViewErrorHandler] Event '{eventName}' has null EventHandlerType");
                    return;
                }

                var del = Delegate.CreateDelegate(evt.EventHandlerType, this, handlerMethod);
                evt.AddEventHandler(browser, del);
                Debug.WriteLine($"[WebViewErrorHandler] Subscribed to AvaloniaCefBrowser.{eventName} → {handlerName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] Failed to subscribe '{eventName}': {ex}");
            }
        }

        // === Event handlers ===

        // LoadError: navigation error (DNS, timeout, HTTP, etc.)
        private void OnBrowserLoadError(object? sender, EventArgs e)
        {
            try
            {
                var argsType = e.GetType();
                var errorTextProp = argsType.GetProperty("ErrorText");
                var errorCodeProp = argsType.GetProperty("ErrorCode");
                var failedUrlProp = argsType.GetProperty("FailedUrl");

                var errorText = errorTextProp?.GetValue(e) as string;
                var failedUrl = failedUrlProp?.GetValue(e) as string;
                
                // Get error code - can be int or enum
                int errorCode = 0;
                var errorCodeValue = errorCodeProp?.GetValue(e);
                if (errorCodeValue != null)
                {
                    if (errorCodeValue is int ec)
                    {
                        errorCode = ec;
                    }
                    else if (errorCodeValue.GetType().IsEnum)
                    {
                        // Convert enum to int
                        errorCode = Convert.ToInt32(errorCodeValue);
                    }
                    else
                    {
                        // Try to convert as number
                        int.TryParse(errorCodeValue.ToString(), out errorCode);
                    }
                }

                // Code 0 means successful load - don't show error
                if (errorCode == 0)
                {
                    Debug.WriteLine($"[WebViewErrorHandler] Load completed successfully (code 0), ignoring");
                    return;
                }

                _hasExplicitErrorForCurrentNav = true;

                var url = !string.IsNullOrWhiteSpace(failedUrl)
                    ? failedUrl
                    : _pendingUrl ?? _lastValidUrl ?? _webView.Address ?? "unknown";

                Debug.WriteLine($"[WebViewErrorHandler] Load Error: {errorText} (Code: {errorCode}) for URL: {url}");

                ReportError(errorCode, url, errorText);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnBrowserLoadError failed: {ex}");
            }
        }

        // UnhandledException: unhandled .NET exceptions in the browser
        private void OnBrowserUnhandledException(object? sender, EventArgs e)
        {
            try
            {
                _hasExplicitErrorForCurrentNav = true;

                var argsType = e.GetType();
                var exProp = argsType.GetProperty("Exception");
                var ex = exProp?.GetValue(e) as Exception;
                if (ex == null) return;

                var errorMessage = $"Unhandled Exception: {ex.Message}";
                var url = _webView.Address ?? _pendingUrl ?? _lastValidUrl ?? "unknown";

                Debug.WriteLine($"[WebViewErrorHandler] {errorMessage} at {url}");

                // Use a generic error code (ERR_FAILED)
                ReportError(-2, url, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnBrowserUnhandledException failed: {ex}");
            }
        }

        // JavascriptUncaughtException: unhandled JS errors
        private void OnJavascriptUncaughtException(object? sender, EventArgs e)
        {
            try
            {
                var argsType = e.GetType();
                var msgProp = argsType.GetProperty("Message");
                var message = msgProp?.GetValue(e) as string;
                if (string.IsNullOrWhiteSpace(message)) return;

                var url = _webView.Address ?? _pendingUrl ?? _lastValidUrl ?? "unknown";
                var errorMessage = $"JavaScript Error: {message}";

                Debug.WriteLine($"[WebViewErrorHandler] {errorMessage} at {url}");

                // By default only log, without showing a separate page.
                // If you want a UI page for JS errors, uncomment:
                // ReportError(-9999, url, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnJavascriptUncaughtException failed: {ex}");
            }
        }

        // ConsoleMessage: console messages, filter errors only
        private void OnConsoleMessage(object? sender, EventArgs e)
        {
            try
            {
                if (_hasExplicitErrorForCurrentNav)
                    return;

                var argsType = e.GetType();
                var msgProp = argsType.GetProperty("Message");
                var levelProp = argsType.GetProperty("Level");
                var sourceProp = argsType.GetProperty("Source");
                var lineProp = argsType.GetProperty("Line");

                var message = msgProp?.GetValue(e) as string;
                if (string.IsNullOrWhiteSpace(message)) return;

                var level = levelProp?.GetValue(e)?.ToString();
                var source = sourceProp?.GetValue(e)?.ToString();
                var lineObj = lineProp?.GetValue(e);
                var line = lineObj?.ToString();

                // Use only Console errors
                if (!string.Equals(level, "Error", StringComparison.OrdinalIgnoreCase))
                    return;

                var url = _webView.Address ?? _pendingUrl ?? _lastValidUrl ?? "unknown";
                var errorMessage = $"Console Error: {message} at {source}:{line}";

                Debug.WriteLine($"[WebViewErrorHandler] {errorMessage}");

                var code = GuessErrorCodeFromTitle(message ?? string.Empty);
                ReportError(code, url, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnConsoleMessage failed: {ex}");
            }
        }

        private void OnWebViewPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[WebViewErrorHandler] PropertyChanged: {e.Property?.Name}; Old={e.OldValue}; New={e.NewValue}");

                if (e.Property?.Name == "Address")
                {
                    var newAddress = e.NewValue as string;
                    var oldAddress = e.OldValue as string;
                    
                    Debug.WriteLine($"[WebViewErrorHandler] Address changed: {oldAddress} -> {newAddress}");
                    
                    // Save last valid URL (as backup in case WebView events didn't fire)
                    if (!string.IsNullOrEmpty(oldAddress) && 
                        !oldAddress.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase) &&
                        !oldAddress.Contains("net::ERR_", StringComparison.OrdinalIgnoreCase))
                    {
                        _lastValidUrl = oldAddress;
                    }
                    
                    // Reset flag on new navigation (so fallback works)
                    if (!string.IsNullOrEmpty(newAddress) && 
                        !newAddress.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase) &&
                        newAddress != oldAddress)
                    {
                        // If this is a new navigation to a regular URL - reset the flag
                        _hasExplicitErrorForCurrentNav = false;
                        Debug.WriteLine($"[WebViewErrorHandler] Reset explicit error flag for new navigation");
                    }
                    
                    // Detect error via chrome-error:// URL as fallback
                    if (!string.IsNullOrEmpty(newAddress))
                    {
                        if (newAddress.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.WriteLine($"[WebViewErrorHandler] Chrome error page detected (fallback): {newAddress}");
                            ParseChromeErrorUrl(newAddress);
                        }
                    }
                }
                else if (e.Property?.Name == "Title")
                {
                    var newTitle = e.NewValue as string;
                    
                    if (string.IsNullOrWhiteSpace(newTitle))
                        return;

                    // If Title explicitly contains ERR_ — consider it an error immediately
                    if (newTitle.Contains("ERR_", StringComparison.OrdinalIgnoreCase))
                    {
                        var guessedCode = GuessErrorCodeFromTitle(newTitle);
                        var failedUrl = _webView.Address;
                        if (string.IsNullOrWhiteSpace(failedUrl) || failedUrl!.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase))
                        {
                            failedUrl = _pendingUrl ?? _lastValidUrl ?? "unknown";
                        }
                        Debug.WriteLine($"[WebViewErrorHandler] Error detected from explicit ERR_ in title (fallback): {newTitle}, code={guessedCode}, url={failedUrl}");
                        _hasExplicitErrorForCurrentNav = true; // Set to prevent duplicates
                        ReportError(guessedCode, failedUrl!, newTitle);
                        return;
                    }
                    
                    // Chromium shows specific titles for errors — use as last line of defense
                    if (DetectErrorFromTitle(newTitle))
                    {
                        Debug.WriteLine($"[WebViewErrorHandler] Error detected from title (fallback): {newTitle}");
                        _hasExplicitErrorForCurrentNav = true; // Set to prevent duplicates
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnWebViewPropertyChanged failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Parses chrome-error:// URL and extracts the error code
        /// </summary>
        private void ParseChromeErrorUrl(string chromeErrorUrl)
        {
            try
            {
                // Format: chrome-error://chromewebdata/?e=&errorCode=-105&httpStatusCode=&s=&c=0&r=-1&u=https://example.com/
                // or other variations
                
                int errorCode = -1;
                string? failedUrl = _lastValidUrl ?? _pendingUrl;
                
                var uri = new Uri(chromeErrorUrl);
                var query = uri.Query.TrimStart('?');
                var parts = query.Split('&');
                
                foreach (var part in parts)
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length == 2)
                    {
                        var key = kv[0].ToLowerInvariant();
                        var value = Uri.UnescapeDataString(kv[1]);
                        
                        if (key == "errorcode" || key == "e")
                        {
                            int.TryParse(value, out errorCode);
                        }
                        else if (key == "u" || key == "url" || key == "failedurl")
                        {
                            if (!string.IsNullOrEmpty(value))
                            {
                                failedUrl = value;
                            }
                        }
                        else if (key == "httpstatuscode" && !string.IsNullOrEmpty(value))
                        {
                            if (int.TryParse(value, out var httpCode) && httpCode >= 400)
                            {
                                ReportHttpError(httpCode, failedUrl ?? chromeErrorUrl);
                                return;
                            }
                        }
                    }
                }
                
                if (errorCode != 0)
                {
                    ReportError(errorCode, failedUrl ?? chromeErrorUrl, null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] ParseChromeErrorUrl failed: {ex.Message}");
                // Fallback - report unknown error
                ReportError(-1, _lastValidUrl ?? chromeErrorUrl, "Unknown navigation error");
            }
        }
        
        /// <summary>
        /// Detects error from page Title
        /// </summary>
        private bool DetectErrorFromTitle(string title)
        {
            // Chromium titles for errors (English + possible localized variants)
            var errorTitles = new[]
            {
                // EN
                "This site can't be reached",
                "This site can’t be reached",
                "This site can't provide a secure connection",
                "Your connection is not private",
                "This page isn't working",
                "404 Not Found",
                "500 Internal Server Error",
                "502 Bad Gateway",
                "503 Service Unavailable",
                // UA/RU (generalized phrases, not strictly tied to specific CEF text)
                "сайт не доступний",
                "сайт недоступний",
                "не вдалося отримати доступ",
                "не удається отримати доступ",
                "соединение не является приватным",
                "подключение не является приватным",
                "ошибка dns",
                "помилка dns",
                "страница недоступна",
                "сторінку не знайдено",
                "page not available"
            };
            
            foreach (var errorTitle in errorTitles)
            {
                if (title.Contains(errorTitle, StringComparison.OrdinalIgnoreCase))
                {
                    var errorCode = GuessErrorCodeFromTitle(title);
                    var failedUrl = _webView.Address;
                    if (string.IsNullOrWhiteSpace(failedUrl) || failedUrl!.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase))
                    {
                        failedUrl = _pendingUrl ?? _lastValidUrl ?? "unknown";
                    }
                    Debug.WriteLine($"[WebViewErrorHandler] DetectErrorFromTitle hit: pattern='{errorTitle}', code={errorCode}, url={failedUrl}");
                    ReportError(errorCode, failedUrl!, title);
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Attempts to determine the error code from Title
        /// </summary>
        private int GuessErrorCodeFromTitle(string title)
        {
            if (title.Contains("ERR_NAME_NOT_RESOLVED", StringComparison.OrdinalIgnoreCase))
                return -105;
            if (title.Contains("ERR_CONNECTION_REFUSED", StringComparison.OrdinalIgnoreCase))
                return -102;
            if (title.Contains("ERR_CONNECTION_TIMED_OUT", StringComparison.OrdinalIgnoreCase))
                return -118;
            if (title.Contains("ERR_INTERNET_DISCONNECTED", StringComparison.OrdinalIgnoreCase))
                return -106;
            if (title.Contains("ERR_SSL", StringComparison.OrdinalIgnoreCase))
                return -107;
            if (title.Contains("ERR_CERT", StringComparison.OrdinalIgnoreCase))
                return -207;
            if (title.Contains("can't be reached", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("can’t be reached", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("не удається отримати доступ", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("не вдалося отримати доступ", StringComparison.OrdinalIgnoreCase))
                return -105;
            if (title.Contains("not private", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("не является приватным", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("не є приватним", StringComparison.OrdinalIgnoreCase))
                return -207;
            if (title.Contains("404", StringComparison.OrdinalIgnoreCase))
                return 404;
            if (title.Contains("500", StringComparison.OrdinalIgnoreCase))
                return 500;
            if (title.Contains("502", StringComparison.OrdinalIgnoreCase))
                return 502;
            if (title.Contains("503", StringComparison.OrdinalIgnoreCase))
                return 503;
            
            return -1; // Unknown error
        }
        
        /// <summary>
        /// Sets the URL we are trying to load (for error tracking)
        /// </summary>
        public void SetPendingNavigation(string url)
        {
            _pendingUrl = url;
            Debug.WriteLine($"[WebViewErrorHandler] Pending navigation set: {url}");
        }
        
        /// <summary>
        /// Asynchronous URL availability check before navigation.
        /// Returns true if URL is accessible, false if error.
        /// </summary>
        public async Task<(bool IsSuccess, int ErrorCode, string? ErrorMessage)> PreCheckUrlAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                return (false, -1, "Empty URL");
                
            // Don't check internal URLs
            if (url.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("chrome://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return (true, 0, null);
            }
            
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0");
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    return (true, 0, null);
                }
                else
                {
                    var statusCode = (int)response.StatusCode;
                    return (false, statusCode, response.ReasonPhrase);
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] PreCheckUrl failed: {ex.Message}");
                
                // Map HTTP exception to CefErrorCode
                var errorCode = MapHttpExceptionToErrorCode(ex);
                return (false, errorCode, ex.Message);
            }
            catch (TaskCanceledException)
            {
                // Timeout
                return (false, -118, "Connection timed out");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] PreCheckUrl error: {ex.Message}");
                return (false, -1, ex.Message);
            }
        }
        
        /// <summary>
        /// Maps HttpRequestException to CefGlue error code
        /// </summary>
        private int MapHttpExceptionToErrorCode(HttpRequestException ex)
        {
            // Check StatusCode if available
            if (ex.StatusCode.HasValue)
            {
                var statusCode = (int)ex.StatusCode.Value;
                if (statusCode >= 400)
                {
                    return statusCode; // HTTP error
                }
            }
            
            // Collect all error messages (including inner exceptions)
            var allMessages = ex.Message.ToLowerInvariant();
            var innerEx = ex.InnerException;
            while (innerEx != null)
            {
                allMessages += " " + innerEx.Message.ToLowerInvariant();
                innerEx = innerEx.InnerException;
            }
            
            // DNS errors - different formats for different OS
            if (allMessages.Contains("name or service not known") || 
                allMessages.Contains("no such host") ||
                allMessages.Contains("getaddrinfo") ||
                allMessages.Contains("dns") ||
                allMessages.Contains("host not found") ||
                allMessages.Contains("known host") ||
                allMessages.Contains("nodename nor servname") ||
                allMessages.Contains("name resolution") ||
                allMessages.Contains("cannot resolve") ||
                allMessages.Contains("the requested name is valid") || // Windows DNS error
                allMessages.Contains("resource temporarily unavailable")) // Sometimes DNS
                return -105; // ERR_NAME_NOT_RESOLVED
                
            if (allMessages.Contains("connection refused") ||
                allMessages.Contains("actively refused") ||
                allMessages.Contains("target machine actively refused"))
                return -102; // ERR_CONNECTION_REFUSED
                
            if (allMessages.Contains("connection reset") ||
                allMessages.Contains("reset by peer") ||
                allMessages.Contains("existing connection was forcibly closed"))
                return -101; // ERR_CONNECTION_RESET
                
            if (allMessages.Contains("timed out") || allMessages.Contains("timeout"))
                return -118; // ERR_CONNECTION_TIMED_OUT
                
            if (allMessages.Contains("ssl") || allMessages.Contains("certificate") || allMessages.Contains("tls") ||
                allMessages.Contains("secure channel"))
                return -107; // ERR_SSL_PROTOCOL_ERROR
                
            if (allMessages.Contains("network is unreachable") || allMessages.Contains("no route") ||
                allMessages.Contains("network unreachable") || allMessages.Contains("host unreachable"))
                return -106; // ERR_INTERNET_DISCONNECTED
                
            return -105; // Default to DNS error for unknown network issues
        }
        
        /// <summary>
        /// Public method for manually reporting an error.
        /// Can be called from TabWorker when an error is detected.
        /// </summary>
        public void ReportError(int errorCode, string failedUrl, string? errorText)
        {
            try
            {
                // Avoid duplicating identical errors
                if (_lastErrorUrl == failedUrl && 
                    _lastErrorCode == errorCode &&
                    (DateTime.UtcNow - _lastErrorTime).TotalSeconds < 2)
                {
                    Debug.WriteLine("[WebViewErrorHandler] Duplicate error ignored");
                    return;
                }
                
                _lastErrorUrl = failedUrl;
                _lastErrorCode = errorCode;
                _lastErrorTime = DateTime.UtcNow;
                
                // Check if error page should be shown
                if (!BrowserErrorService.ShouldShowErrorPage(errorCode))
                {
                    Debug.WriteLine($"[WebViewErrorHandler] Error {errorCode} ignored (not user-visible)");
                    return;
                }
                
                // Get localized error
                var error = BrowserErrorService.GetLocalizedError(errorCode, failedUrl, errorText);
                
                Debug.WriteLine($"[WebViewErrorHandler] Error reported: {error.Title} ({error.ErrorName})");
                
                // Notify subscribers on UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    Debug.WriteLine($"[WebViewErrorHandler] Invoking ErrorOccurred event for: {error.Title}");
                    ErrorOccurred?.Invoke(this, new BrowserErrorEventArgs(error));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] ReportError failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reports an HTTP error
        /// </summary>
        public void ReportHttpError(int httpStatusCode, string failedUrl)
        {
            try
            {
                if (httpStatusCode < 400) return; // Not an error
                
                // Avoid duplicating
                if (_lastErrorUrl == failedUrl && 
                    _lastErrorCode == httpStatusCode &&
                    (DateTime.UtcNow - _lastErrorTime).TotalSeconds < 2)
                {
                    return;
                }
                
                _lastErrorUrl = failedUrl;
                _lastErrorCode = httpStatusCode;
                _lastErrorTime = DateTime.UtcNow;
                
                var error = BrowserErrorService.GetHttpError(httpStatusCode, failedUrl);
                
                Debug.WriteLine($"[WebViewErrorHandler] HTTP error reported: {error.Title} ({httpStatusCode})");
                
                Dispatcher.UIThread.Post(() =>
                {
                    ErrorOccurred?.Invoke(this, new BrowserErrorEventArgs(error));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] ReportHttpError failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                if (_isAttached)
                {
                    _webView.PropertyChanged -= OnWebViewPropertyChanged;
                    _isAttached = false;
                }
                Debug.WriteLine("[WebViewErrorHandler] Disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] Dispose failed: {ex.Message}");
            }
        }
        
        // === AvaloniaCefBrowser handlers ===
        private void OnCefLoadError(object? sender, EventArgs e)
        {
            try
            {
                var argsType = e.GetType();
                // LoadErrorEventArgs contract from your snippet:
                // ErrorText, ErrorCode, FailedUrl
                var errorTextProp = argsType.GetProperty("ErrorText");
                var errorCodeProp = argsType.GetProperty("ErrorCode");
                var failedUrlProp = argsType.GetProperty("FailedUrl");

                var errorText = errorTextProp?.GetValue(e) as string;
                var failedUrl = failedUrlProp?.GetValue(e) as string;
                
                // Get error code - can be int or enum
                int errorCode = 0;
                var errorCodeValue = errorCodeProp?.GetValue(e);
                if (errorCodeValue != null)
                {
                    if (errorCodeValue is int ec)
                    {
                        errorCode = ec;
                    }
                    else if (errorCodeValue.GetType().IsEnum)
                    {
                        // Convert enum to int
                        errorCode = Convert.ToInt32(errorCodeValue);
                    }
                    else
                    {
                        // Try to convert as number
                        int.TryParse(errorCodeValue.ToString(), out errorCode);
                    }
                }

                // Code 0 means successful load - don't show error
                if (errorCode == 0)
                {
                    Debug.WriteLine($"[WebViewErrorHandler] AvaloniaCefBrowser.LoadError: code 0 (success), ignoring");
                    return;
                }

                var url = !string.IsNullOrEmpty(failedUrl)
                    ? failedUrl
                    : _pendingUrl ?? _lastValidUrl ?? "unknown";

                Debug.WriteLine($"[WebViewErrorHandler] AvaloniaCefBrowser.LoadError: code={errorCode}, url={url}, text={errorText}");
                ReportError(errorCode, url, errorText);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnCefLoadError failed: {ex.Message}");
            }
        }

        private void OnCefUnhandledException(object? sender, EventArgs e)
        {
            try
            {
                var argsType = e.GetType();
                var exProp = argsType.GetProperty("Exception");
                var ex = exProp?.GetValue(e) as Exception;
                if (ex == null) return;

                var msg = ex.Message;
                var url = _webView.Address ?? _pendingUrl ?? _lastValidUrl ?? "unknown";

                Debug.WriteLine($"[WebViewErrorHandler] AvaloniaCefBrowser.UnhandledException: {msg}, url={url}");
                // Use generic error code ERR_FAILED (-2) for unhandled exception
                ReportError(-2, url, msg);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnCefUnhandledException failed: {ex.Message}");
            }
        }

        private void OnCefJavascriptUncaughtException(object? sender, EventArgs e)
        {
            try
            {
                var argsType = e.GetType();
                var msgProp = argsType.GetProperty("Message");
                var message = msgProp?.GetValue(e) as string;
                if (string.IsNullOrWhiteSpace(message)) return;

                var url = _webView.Address ?? _pendingUrl ?? _lastValidUrl ?? "unknown";
                Debug.WriteLine($"[WebViewErrorHandler] AvaloniaCefBrowser.JavascriptUncaughtException: {message}, url={url}");

                // JS errors don't always mean navigation failure, so we may not show user error page, but log.
                // If you want to show a UI error, you can choose a separate conditional code, e.g. -9999.
                // Here we limit to logging, without ReportError.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnCefJavascriptUncaughtException failed: {ex.Message}");
            }
        }

        private void OnCefConsoleMessage(object? sender, EventArgs e)
        {
            try
            {
                var argsType = e.GetType();
                var msgProp = argsType.GetProperty("Message");
                var levelProp = argsType.GetProperty("Level");

                var message = msgProp?.GetValue(e) as string;
                if (string.IsNullOrWhiteSpace(message)) return;

                var level = levelProp?.GetValue(e);
                var levelStr = level?.ToString() ?? string.Empty;

                // Use only console errors
                if (!levelStr.Contains("Error", StringComparison.OrdinalIgnoreCase))
                    return;

                if (_hasExplicitErrorForCurrentNav)
                    return; // already have a normal navigation error

                var code = GuessErrorCodeFromTitle(message);
                var url = _webView.Address ?? _pendingUrl ?? _lastValidUrl ?? "unknown";

                Debug.WriteLine($"[WebViewErrorHandler] AvaloniaCefBrowser.ConsoleMessage error: {message}, code={code}, url={url}");
                ReportError(code, url, message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnCefConsoleMessage failed: {ex.Message}");
            }
        }

        // When handling successful navigation completion (e.g., in LoadingStateChanged or after PreCheckUrl),
        // it makes sense to reset the flag so that a new navigation can generate errors again:
        private void ResetExplicitErrorFlagOnSuccessfulNavigation(string? finalUrl)
        {
            if (!string.IsNullOrEmpty(finalUrl) &&
                !finalUrl.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase) &&
                !finalUrl.Contains("net::ERR_", StringComparison.OrdinalIgnoreCase))
            {
                _lastValidUrl = finalUrl;
                _hasExplicitErrorForCurrentNav = false;
            }
        }
    }
    
    /// <summary>
    /// Browser error event arguments
    /// </summary>
    public class BrowserErrorEventArgs : EventArgs
    {
        public BrowserError Error { get; }
        
        /// <summary>
        /// Whether the error was handled (displayed)
        /// </summary>
        public bool Handled { get; set; }
        
        /// <summary>
        /// Error page for display
        /// </summary>
        public BrowserErrorPage? ErrorPage { get; private set; }

        public BrowserErrorEventArgs(BrowserError error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }
        
        /// <summary>
        /// Creates an error page for display
        /// </summary>
        public BrowserErrorPage CreateErrorPage()
        {
            ErrorPage = new BrowserErrorPage(Error);
            return ErrorPage;
        }
    }
}

