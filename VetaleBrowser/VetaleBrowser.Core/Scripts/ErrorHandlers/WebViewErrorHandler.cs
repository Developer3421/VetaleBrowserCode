using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using WebViewControl;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using Avalonia.Threading;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers
{
    /// <summary>
    /// Обробник помилок WebView для перехоплення та локалізації помилок завантаження
    /// Використовує моніторинг Title та Address для детекції chrome-error сторінок
    /// </summary>
    public class WebViewErrorHandler : IDisposable
    {
        private readonly WebView _webView;
        private bool _isAttached;
        private string? _pendingUrl;
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private string? _lastErrorUrl;
        private int _lastErrorCode;
        private DateTime _lastErrorTime;
        private string? _lastValidUrl;
        
        // Позначає, що для поточної навігації вже отримали явну помилку з події (LoadError тощо)
        // і не треба дублювати її через fallback по Title/Address/ConsoleMessage
        private bool _hasExplicitErrorForCurrentNav;

        /// <summary>
        /// Подія виникнення помилки з локалізованим контентом
        /// </summary>
        public event EventHandler<BrowserErrorEventArgs>? ErrorOccurred;

        public WebViewErrorHandler(WebView webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        }

        /// <summary>
        /// Приєднує обробники помилок до WebView
        /// </summary>
        public void Attach()
        {
            if (_isAttached) return;

            try
            {
                // Спочатку намагаємось підписатись на події самого WebView
                AttachWebViewEvents();
                
                // Потім шукаємо внутрішній AvaloniaCefBrowser
                AttachAvaloniaCefBrowserEvents();
                
                // залишаємо PropertyChanged як fallback (оновлення _lastValidUrl + chrome-error/title-хак як останню лінію оборони)
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
        /// Спробувати підписатись на події безпосередньо на WebView
        /// </summary>
        private void AttachWebViewEvents()
        {
            var webViewType = _webView.GetType();
            Debug.WriteLine($"[WebViewErrorHandler] Attaching to WebView events...");
            
            // Виводимо всі події WebView для діагностики
            Debug.WriteLine("[WebViewErrorHandler] WebView events:");
            foreach (var evt in webViewType.GetEvents(BindingFlags.Instance | BindingFlags.Public))
            {
                Debug.WriteLine($"[WebViewErrorHandler]   Event: {evt.Name}");
            }
            
            // WebView з WebViewControl-Avalonia може мати власні події помилок
            // Спробуємо різні варіанти назв
            string[] errorEventNames = { 
                "LoadError", "LoadFailed", "NavigationError", "PageLoadError",
                "BrowserLoadError", "OnLoadError", "LoadingError" 
            };
            
            foreach (var eventName in errorEventNames)
            {
                TrySubscribe(_webView, webViewType, eventName, nameof(OnBrowserLoadError));
            }
            
            TrySubscribe(_webView, webViewType, "UnhandledException", nameof(OnBrowserUnhandledException));
            TrySubscribe(_webView, webViewType, "JavascriptUncaughtException", nameof(OnJavascriptUncaughtException));
            TrySubscribe(_webView, webViewType, "ConsoleMessage", nameof(OnConsoleMessage));
        }

        /// <summary>
        /// Шукає всередині WebView екземпляр AvaloniaCefBrowser і підписується на LoadError, UnhandledException, JavascriptUncaughtException, ConsoleMessage
        /// </summary>
        private void AttachAvaloniaCefBrowserEvents()
        {
            // Багато обгорток для CEF мають всередині властивість/поле "Browser". Підлаштуй ім'я при потребі.
            var webViewType = _webView.GetType();
            Debug.WriteLine($"[WebViewErrorHandler] WebView type: {webViewType.FullName}");
            
            // Виводимо всі властивості та поля для діагностики
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
            
            // Спробуємо різні назви властивостей/полів
            string[] browserNames = { "Browser", "_browser", "browser", "chromiumBrowser", "_chromiumBrowser", "InternalBrowser", "_internalBrowser", "CefBrowser", "_cefBrowser" };
            
            foreach (var name in browserNames)
            {
                var browserProp = webViewType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (browserProp != null)
                {
                    browser = browserProp.GetValue(_webView);
                    if (browser != null)
                    {
                        Debug.WriteLine($"[WebViewErrorHandler] Found browser via property '{name}', type: {browser.GetType().FullName}");
                        break;
                    }
                }
                
                var browserField = webViewType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (browserField != null)
                {
                    browser = browserField.GetValue(_webView);
                    if (browser != null)
                    {
                        Debug.WriteLine($"[WebViewErrorHandler] Found browser via field '{name}', type: {browser.GetType().FullName}");
                        break;
                    }
                }
            }
            
            // Якщо не знайшли за назвою, шукаємо за типом (містить CefBrowser або AvaloniaCefBrowser в назві типу)
            if (browser == null)
            {
                Debug.WriteLine("[WebViewErrorHandler] Searching by type pattern...");
                foreach (var field in webViewType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var val = field.GetValue(_webView);
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
                            var val = prop.GetValue(_webView);
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
                        catch { /* ігноруємо помилки при читанні властивостей */ }
                    }
                }
            }

            if (browser == null)
            {
                Debug.WriteLine("[WebViewErrorHandler] AvaloniaCefBrowser not found inside WebView (property/field 'Browser' missing or null)");
                Debug.WriteLine("[WebViewErrorHandler] Will rely on PropertyChanged fallback for error detection");
                return;
            }

            // Підписуємось максимально прямо, як у твоєму прикладі, але через reflection, щоб не тягнути типи EventArgs у Core.
            var browserType = browser.GetType();
            Debug.WriteLine($"[WebViewErrorHandler] Browser type: {browserType.FullName}");
            
            // Виводимо доступні події
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

        // === Хендлери, максимально близькі до твого прикладу ===

        // LoadError: навігаційна помилка (DNS, timeout, HTTP і т.д.)
        private void OnBrowserLoadError(object? sender, EventArgs e)
        {
            try
            {
                _hasExplicitErrorForCurrentNav = true;

                var argsType = e.GetType();
                var errorTextProp = argsType.GetProperty("ErrorText");
                var errorCodeProp = argsType.GetProperty("ErrorCode");
                var failedUrlProp = argsType.GetProperty("FailedUrl");

                var errorText = errorTextProp?.GetValue(e) as string;
                var failedUrl = failedUrlProp?.GetValue(e) as string;
                int errorCode = 0;
                if (errorCodeProp?.GetValue(e) is int ec)
                    errorCode = ec;

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

        // UnhandledException: необроблені .NET винятки в браузері
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

                // Використовуємо умовний загальний код помилки (ERR_FAILED)
                ReportError(-2, url, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnBrowserUnhandledException failed: {ex}");
            }
        }

        // JavascriptUncaughtException: необроблені JS-помилки
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

                // За замовчуванням тільки лог, без показу окремої сторінки.
                // Якщо хочеш UI-сторінку для JS-помилок, розкоментуй:
                // ReportError(-9999, url, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnJavascriptUncaughtException failed: {ex}");
            }
        }

        // ConsoleMessage: повідомлення консолі, фільтруємо лише помилки
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

                // Використовуємо тільки Console errors
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
                    
                    // Зберігаємо останній валідний URL (як резерв на випадок, якщо події WebView не спрацювали)
                    if (!string.IsNullOrEmpty(oldAddress) && 
                        !oldAddress.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase) &&
                        !oldAddress.Contains("net::ERR_", StringComparison.OrdinalIgnoreCase))
                    {
                        _lastValidUrl = oldAddress;
                    }
                    
                    // Скидаємо прапорець при новій навігації (щоб fallback працював)
                    if (!string.IsNullOrEmpty(newAddress) && 
                        !newAddress.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase) &&
                        newAddress != oldAddress)
                    {
                        // Якщо це нова навігація на звичайний URL - скидаємо прапорець
                        _hasExplicitErrorForCurrentNav = false;
                        Debug.WriteLine($"[WebViewErrorHandler] Reset explicit error flag for new navigation");
                    }
                    
                    // Детектуємо помилку через chrome-error:// URL як fallback
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

                    // Якщо в Title явно присутній ERR_ — одразу вважаємо це помилкою
                    if (newTitle.Contains("ERR_", StringComparison.OrdinalIgnoreCase))
                    {
                        var guessedCode = GuessErrorCodeFromTitle(newTitle);
                        var failedUrl = _webView.Address;
                        if (string.IsNullOrWhiteSpace(failedUrl) || failedUrl!.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase))
                        {
                            failedUrl = _pendingUrl ?? _lastValidUrl ?? "unknown";
                        }
                        Debug.WriteLine($"[WebViewErrorHandler] Error detected from explicit ERR_ in title (fallback): {newTitle}, code={guessedCode}, url={failedUrl}");
                        _hasExplicitErrorForCurrentNav = true; // Встановлюємо щоб не дублювати
                        ReportError(guessedCode, failedUrl!, newTitle);
                        return;
                    }
                    
                    // Chromium показує специфічні title для помилок — використовуємо як останню лінію оборони
                    if (DetectErrorFromTitle(newTitle))
                    {
                        Debug.WriteLine($"[WebViewErrorHandler] Error detected from title (fallback): {newTitle}");
                        _hasExplicitErrorForCurrentNav = true; // Встановлюємо щоб не дублювати
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewErrorHandler] OnWebViewPropertyChanged failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Парсить chrome-error:// URL та витягує код помилки
        /// </summary>
        private void ParseChromeErrorUrl(string chromeErrorUrl)
        {
            try
            {
                // Формат: chrome-error://chromewebdata/?e=&errorCode=-105&httpStatusCode=&s=&c=0&r=-1&u=https://example.com/
                // або інші варіації
                
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
                // Fallback - повідомляємо про невідому помилку
                ReportError(-1, _lastValidUrl ?? chromeErrorUrl, "Unknown navigation error");
            }
        }
        
        /// <summary>
        /// Детектує помилку через Title сторінки
        /// </summary>
        private bool DetectErrorFromTitle(string title)
        {
            // Chromium titles для помилок (англійські + можливі локалізовані варіанти)
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
                // UA/RU (узагальнені формулювання, без суворої прив'язки до конкретного тексту CEF)
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
        /// Намагається визначити код помилки з Title
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
        /// Встановлює URL який ми намагаємося завантажити (для трекінгу помилок)
        /// </summary>
        public void SetPendingNavigation(string url)
        {
            _pendingUrl = url;
            Debug.WriteLine($"[WebViewErrorHandler] Pending navigation set: {url}");
        }
        
        /// <summary>
        /// Асинхронна перевірка доступності URL перед навігацією
        /// Повертає true якщо URL доступний, false якщо помилка
        /// </summary>
        public async Task<(bool IsSuccess, int ErrorCode, string? ErrorMessage)> PreCheckUrlAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                return (false, -1, "Empty URL");
                
            // Не перевіряємо внутрішні URL
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
                
                // Мапуємо HTTP exception на CefErrorCode
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
        /// Мапує HttpRequestException на код помилки CefGlue
        /// </summary>
        private int MapHttpExceptionToErrorCode(HttpRequestException ex)
        {
            // Перевіряємо StatusCode якщо доступний
            if (ex.StatusCode.HasValue)
            {
                var statusCode = (int)ex.StatusCode.Value;
                if (statusCode >= 400)
                {
                    return statusCode; // HTTP помилка
                }
            }
            
            // Збираємо всі повідомлення про помилки (включаючи inner exceptions)
            var allMessages = ex.Message.ToLowerInvariant();
            var innerEx = ex.InnerException;
            while (innerEx != null)
            {
                allMessages += " " + innerEx.Message.ToLowerInvariant();
                innerEx = innerEx.InnerException;
            }
            
            // DNS помилки - різні формати для різних ОС
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
        /// Публічний метод для ручного повідомлення про помилку
        /// Може бути викликаний з TabWorker при виявленні помилки
        /// </summary>
        public void ReportError(int errorCode, string failedUrl, string? errorText)
        {
            try
            {
                // Уникаємо дублювання однакових помилок
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
                
                // Перевіряємо, чи потрібно показувати сторінку помилки
                if (!BrowserErrorService.ShouldShowErrorPage(errorCode))
                {
                    Debug.WriteLine($"[WebViewErrorHandler] Error {errorCode} ignored (not user-visible)");
                    return;
                }
                
                // Отримуємо локалізовану помилку
                var error = BrowserErrorService.GetLocalizedError(errorCode, failedUrl, errorText);
                
                Debug.WriteLine($"[WebViewErrorHandler] Error reported: {error.Title} ({error.ErrorName})");
                
                // Сповіщаємо підписників в UI потоці
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
        /// Повідомляє про HTTP помилку
        /// </summary>
        public void ReportHttpError(int httpStatusCode, string failedUrl)
        {
            try
            {
                if (httpStatusCode < 400) return; // Не помилка
                
                // Уникаємо дублювання
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
                int errorCode = 0;
                if (errorCodeProp?.GetValue(e) is int ec)
                    errorCode = ec;

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
                // Використовуємо загальний код ERR_FAILED (-2) для необробленого винятку
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

                // JS помилки не завжди означають фейл навігації, тому можна не показувати юзер-сторінку, але логувати.
                // Якщо хочеш показувати UI-помилку, можна обрати окремий умовний код, наприклад -9999.
                // Тут обмежимося логуванням, без ReportError.
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

                // Використовуємо лише помилки з консолі
                if (!levelStr.Contains("Error", StringComparison.OrdinalIgnoreCase))
                    return;

                if (_hasExplicitErrorForCurrentNav)
                    return; // уже є нормальна навігаційна помилка

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

        // Де ти обробляєш успішне завершення навігації (наприклад, у LoadingStateChanged або після PreCheckUrl),
        // має сенс скидати прапорець, щоб нова навігація могла знову генерувати помилки:
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
    /// Аргументи події помилки браузера
    /// </summary>
    public class BrowserErrorEventArgs : EventArgs
    {
        public BrowserError Error { get; }
        
        /// <summary>
        /// Чи було оброблено (показано) помилку
        /// </summary>
        public bool Handled { get; set; }
        
        /// <summary>
        /// Сторінка помилки для відображення
        /// </summary>
        public BrowserErrorPage? ErrorPage { get; private set; }

        public BrowserErrorEventArgs(BrowserError error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }
        
        /// <summary>
        /// Створює сторінку помилки для відображення
        /// </summary>
        public BrowserErrorPage CreateErrorPage()
        {
            ErrorPage = new BrowserErrorPage(Error);
            return ErrorPage;
        }
    }
}

