using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using WebViewControl;

namespace VetaleBrowser.VetaleBrowser.UI.Controls
{
    /// <summary>
    /// Компонент для обробки помилок CefGlue Web View з відображенням BrowserErrorPage
    /// Інкапсулює логіку перемикання між браузером та сторінкою помилки
    /// </summary>
    public partial class ErrorHandlingBrowserComponent : UserControl
    {
        private ContentControl? _webViewContainer;
        private ContentControl? _errorPageContainer;
        private WebView? _webView;
        private WebViewErrorHandler? _errorHandler;
        private BrowserErrorPage? _currentErrorPage;
        private string? _lastUrl;
        private bool _isShowingError;
        private bool _ownsErrorHandler; // Чи ми створили ErrorHandler самі (і маємо його знищити)

        /// <summary>
        /// Подія запиту повторної спроби завантаження
        /// </summary>
        public event EventHandler? RetryRequested;

        /// <summary>
        /// Подія запиту повернення назад
        /// </summary>
        public event EventHandler? GoBackRequested;

        /// <summary>
        /// Подія запиту переходу на головну
        /// </summary>
        public event EventHandler? GoHomeRequested;

        /// <summary>
        /// Подія запиту пошуку
        /// </summary>
        public event EventHandler<string>? SearchRequested;

        /// <summary>
        /// Подія виникнення помилки (для зовнішньої обробки)
        /// </summary>
        public event EventHandler<BrowserErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Доступ до WebView
        /// </summary>
        public WebView? WebView => _webView;

        /// <summary>
        /// Чи відображається сторінка помилки
        /// </summary>
        public bool IsShowingError => _isShowingError;

        /// <summary>
        /// Поточна помилка
        /// </summary>
        public BrowserError? CurrentError => _currentErrorPage != null ? GetErrorFromPage(_currentErrorPage) : null;

        public ErrorHandlingBrowserComponent()
        {
            InitializeComponent();
            FindControls();
        }

        /// <summary>
        /// Конструктор з переданим WebView
        /// </summary>
        public ErrorHandlingBrowserComponent(WebView webView) : this()
        {
            SetWebView(webView);
        }
        
        /// <summary>
        /// Конструктор з переданим WebView та існуючим ErrorHandler
        /// </summary>
        public ErrorHandlingBrowserComponent(WebView webView, WebViewErrorHandler errorHandler) : this()
        {
            SetWebView(webView, errorHandler);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void FindControls()
        {
            _webViewContainer = this.FindControl<ContentControl>("WebViewContainer");
            _errorPageContainer = this.FindControl<ContentControl>("ErrorPageContainer");
        }

        /// <summary>
        /// Встановлює WebView та налаштовує обробку помилок (створює новий ErrorHandler)
        /// </summary>
        public void SetWebView(WebView webView)
        {
            if (webView == null)
                throw new ArgumentNullException(nameof(webView));

            // Відписуємось від старого
            DetachErrorHandler();

            _webView = webView;

            // Встановлюємо WebView в контейнер
            if (_webViewContainer != null)
            {
                _webViewContainer.Content = _webView;
            }

            // Створюємо новий обробник помилок
            _errorHandler = new WebViewErrorHandler(_webView);
            _errorHandler.ErrorOccurred += OnErrorHandlerError;
            _errorHandler.Attach();
            _ownsErrorHandler = true;

            // Відстежуємо зміни Address для збереження останнього URL
            _webView.PropertyChanged += OnWebViewPropertyChanged;

            Debug.WriteLine("[ErrorHandlingBrowserComponent] WebView set, new error handler created and attached");
        }
        
        /// <summary>
        /// Встановлює WebView та використовує існуючий ErrorHandler (з TabWorker)
        /// </summary>
        public void SetWebView(WebView webView, WebViewErrorHandler errorHandler)
        {
            if (webView == null)
                throw new ArgumentNullException(nameof(webView));
            if (errorHandler == null)
                throw new ArgumentNullException(nameof(errorHandler));

            // Відписуємось від старого
            DetachErrorHandler();

            _webView = webView;

            // Встановлюємо WebView в контейнер
            if (_webViewContainer != null)
            {
                _webViewContainer.Content = _webView;
            }

            // Використовуємо існуючий обробник помилок
            _errorHandler = errorHandler;
            _errorHandler.ErrorOccurred += OnErrorHandlerError;
            _ownsErrorHandler = false; // Не ми його створили, не ми знищуємо

            // Відстежуємо зміни Address для збереження останнього URL
            _webView.PropertyChanged += OnWebViewPropertyChanged;

            Debug.WriteLine("[ErrorHandlingBrowserComponent] WebView set, using existing error handler");
        }
        
        /// <summary>
        /// Встановлює тільки ErrorHandler (якщо WebView вже встановлено іншим чином)
        /// </summary>
        public void SetErrorHandler(WebViewErrorHandler errorHandler)
        {
            if (errorHandler == null)
                throw new ArgumentNullException(nameof(errorHandler));

            // Відписуємось від старого
            DetachErrorHandler();

            _errorHandler = errorHandler;
            _errorHandler.ErrorOccurred += OnErrorHandlerError;
            _ownsErrorHandler = false;

            Debug.WriteLine("[ErrorHandlingBrowserComponent] Error handler set (external)");
        }
        
        /// <summary>
        /// Відключає поточний ErrorHandler
        /// </summary>
        private void DetachErrorHandler()
        {
            if (_errorHandler != null)
            {
                _errorHandler.ErrorOccurred -= OnErrorHandlerError;
                if (_ownsErrorHandler)
                {
                    _errorHandler.Dispose();
                }
                _errorHandler = null;
                _ownsErrorHandler = false;
            }
        }

        /// <summary>
        /// Отримує ErrorHandler для прямого доступу
        /// </summary>
        public WebViewErrorHandler? ErrorHandler => _errorHandler;

        private void OnWebViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property?.Name == "Address")
            {
                var newAddress = e.NewValue as string;
                if (!string.IsNullOrEmpty(newAddress) && 
                    !newAddress.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase))
                {
                    _lastUrl = newAddress;
                }
            }
        }

        /// <summary>
        /// Обробник помилок від WebViewErrorHandler
        /// </summary>
        private void OnErrorHandlerError(object? sender, BrowserErrorEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[ErrorHandlingBrowserComponent] Error received: {e.Error.Title} ({e.Error.ErrorName})");

                // Запам'ятовуємо URL для повторної спроби
                if (!string.IsNullOrEmpty(e.Error.FailedUrl))
                {
                    _lastUrl = e.Error.FailedUrl;
                }

                // Повідомляємо зовнішніх підписників
                ErrorOccurred?.Invoke(this, e);

                // Якщо зовнішній обробник не позначив як оброблену - показуємо сторінку помилки
                if (!e.Handled)
                {
                    Dispatcher.UIThread.Post(() => ShowError(e.Error));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ErrorHandlingBrowserComponent] OnErrorHandlerError failed: {ex}");
            }
        }

        /// <summary>
        /// Показує сторінку помилки
        /// </summary>
        public void ShowError(BrowserError error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            try
            {
                // Створюємо сторінку помилки
                _currentErrorPage = new BrowserErrorPage(error);
                
                // Підписуємось на події сторінки помилки
                _currentErrorPage.RetryRequested += OnRetryRequested;
                _currentErrorPage.GoBackRequested += OnGoBackRequested;
                _currentErrorPage.GoHomeRequested += OnGoHomeRequested;
                _currentErrorPage.SearchRequested += OnSearchRequested;

                // Показуємо сторінку помилки, приховуємо WebView
                if (_errorPageContainer != null && _webViewContainer != null)
                {
                    _errorPageContainer.Content = _currentErrorPage;
                    _errorPageContainer.IsVisible = true;
                    _webViewContainer.IsVisible = false;
                    _isShowingError = true;

                    Debug.WriteLine($"[ErrorHandlingBrowserComponent] Error page displayed: {error.Title}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ErrorHandlingBrowserComponent] ShowError failed: {ex}");
            }
        }

        /// <summary>
        /// Приховує сторінку помилки та показує WebView
        /// </summary>
        public void HideError()
        {
            try
            {
                // Відписуємось від старої сторінки помилки
                if (_currentErrorPage != null)
                {
                    _currentErrorPage.RetryRequested -= OnRetryRequested;
                    _currentErrorPage.GoBackRequested -= OnGoBackRequested;
                    _currentErrorPage.GoHomeRequested -= OnGoHomeRequested;
                    _currentErrorPage.SearchRequested -= OnSearchRequested;
                    _currentErrorPage = null;
                }

                // Приховуємо сторінку помилки, показуємо WebView
                if (_errorPageContainer != null && _webViewContainer != null)
                {
                    _errorPageContainer.IsVisible = false;
                    _errorPageContainer.Content = null;
                    _webViewContainer.IsVisible = true;
                    _isShowingError = false;

                    Debug.WriteLine("[ErrorHandlingBrowserComponent] Error page hidden, WebView visible");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ErrorHandlingBrowserComponent] HideError failed: {ex}");
            }
        }

        /// <summary>
        /// Повторна спроба завантаження останнього URL
        /// </summary>
        public void Retry()
        {
            HideError();
            
            if (!string.IsNullOrEmpty(_lastUrl) && _webView != null)
            {
                Debug.WriteLine($"[ErrorHandlingBrowserComponent] Retrying URL: {_lastUrl}");
                _webView.Address = _lastUrl;
            }
        }

        /// <summary>
        /// Навігація до URL
        /// </summary>
        public void Navigate(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;

            // Приховуємо помилку якщо показується
            if (_isShowingError)
            {
                HideError();
            }

            _lastUrl = url;
            
            if (_webView != null)
            {
                _webView.Address = url;
            }
        }

        private void OnRetryRequested(object? sender, EventArgs e)
        {
            RetryRequested?.Invoke(this, e);
            
            // Якщо немає зовнішнього обробника - виконуємо стандартну логіку
            if (RetryRequested == null)
            {
                Retry();
            }
        }

        private void OnGoBackRequested(object? sender, EventArgs e)
        {
            GoBackRequested?.Invoke(this, e);
        }

        private void OnGoHomeRequested(object? sender, EventArgs e)
        {
            GoHomeRequested?.Invoke(this, e);
        }

        private void OnSearchRequested(object? sender, string query)
        {
            SearchRequested?.Invoke(this, query);
        }

        /// <summary>
        /// Отримує помилку зі сторінки
        /// </summary>
        private BrowserError? GetErrorFromPage(BrowserErrorPage page)
        {
            return page?.Error;
        }

        /// <summary>
        /// Звільняє ресурси
        /// </summary>
        public void Dispose()
        {
            try
            {
                DetachErrorHandler();

                if (_webView != null)
                {
                    _webView.PropertyChanged -= OnWebViewPropertyChanged;
                    _webView = null;
                }

                if (_currentErrorPage != null)
                {
                    _currentErrorPage.RetryRequested -= OnRetryRequested;
                    _currentErrorPage.GoBackRequested -= OnGoBackRequested;
                    _currentErrorPage.GoHomeRequested -= OnGoHomeRequested;
                    _currentErrorPage.SearchRequested -= OnSearchRequested;
                    _currentErrorPage = null;
                }

                Debug.WriteLine("[ErrorHandlingBrowserComponent] Disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ErrorHandlingBrowserComponent] Dispose failed: {ex}");
            }
        }
    }
}

