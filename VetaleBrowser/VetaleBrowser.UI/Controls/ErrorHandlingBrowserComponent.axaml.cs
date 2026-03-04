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
    /// Component for handling CefGlue Web View errors with BrowserErrorPage display
    /// Encapsulates the logic for switching between the browser and the error page
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
        private bool _ownsErrorHandler; // Whether we created the ErrorHandler ourselves (and must destroy it)

        /// <summary>
        /// Event for requesting a retry of page loading
        /// </summary>
        public event EventHandler? RetryRequested;

        /// <summary>
        /// Event for requesting navigation back
        /// </summary>
        public event EventHandler? GoBackRequested;

        /// <summary>
        /// Event for requesting navigation home
        /// </summary>
        public event EventHandler? GoHomeRequested;

        /// <summary>
        /// Event for requesting a search
        /// </summary>
        public event EventHandler<string>? SearchRequested;

        /// <summary>
        /// Event raised when an error occurs (for external handling)
        /// </summary>
        public event EventHandler<BrowserErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Access to WebView
        /// </summary>
        public WebView? WebView => _webView;

        /// <summary>
        /// Whether the error page is currently displayed
        /// </summary>
        public bool IsShowingError => _isShowingError;

        /// <summary>
        /// Current error
        /// </summary>
        public BrowserError? CurrentError => _currentErrorPage != null ? GetErrorFromPage(_currentErrorPage) : null;

        public ErrorHandlingBrowserComponent()
        {
            InitializeComponent();
            FindControls();
        }

        /// <summary>
        /// Constructor with a provided WebView
        /// </summary>
        public ErrorHandlingBrowserComponent(WebView webView) : this()
        {
            SetWebView(webView);
        }
        
        /// <summary>
        /// Constructor with a provided WebView and existing ErrorHandler
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
        /// Sets the WebView and configures error handling (creates a new ErrorHandler)
        /// </summary>
        public void SetWebView(WebView webView)
        {
            if (webView == null)
                throw new ArgumentNullException(nameof(webView));

            // Unsubscribe from the old one
            DetachErrorHandler();

            _webView = webView;

            // Set the WebView in the container
            if (_webViewContainer != null)
            {
                _webViewContainer.Content = _webView;
            }

            // Create a new error handler
            _errorHandler = new WebViewErrorHandler(_webView);
            _errorHandler.ErrorOccurred += OnErrorHandlerError;
            _errorHandler.Attach();
            _ownsErrorHandler = true;

            // Track Address changes to save the last URL
            _webView.PropertyChanged += OnWebViewPropertyChanged;

            Debug.WriteLine("[ErrorHandlingBrowserComponent] WebView set, new error handler created and attached");
        }
        
        /// <summary>
        /// Sets the WebView and uses an existing ErrorHandler (from TabWorker)
        /// </summary>
        public void SetWebView(WebView webView, WebViewErrorHandler errorHandler)
        {
            if (webView == null)
                throw new ArgumentNullException(nameof(webView));
            if (errorHandler == null)
                throw new ArgumentNullException(nameof(errorHandler));

            // Unsubscribe from the old one
            DetachErrorHandler();

            _webView = webView;

            // Set the WebView in the container
            if (_webViewContainer != null)
            {
                _webViewContainer.Content = _webView;
            }

            // Use the existing error handler
            _errorHandler = errorHandler;
            _errorHandler.ErrorOccurred += OnErrorHandlerError;
            _ownsErrorHandler = false; // We did not create it, we do not destroy it

            // Track Address changes to save the last URL
            _webView.PropertyChanged += OnWebViewPropertyChanged;

            Debug.WriteLine("[ErrorHandlingBrowserComponent] WebView set, using existing error handler");
        }
        
        /// <summary>
        /// Sets only the ErrorHandler (if WebView was already set by other means)
        /// </summary>
        public void SetErrorHandler(WebViewErrorHandler errorHandler)
        {
            if (errorHandler == null)
                throw new ArgumentNullException(nameof(errorHandler));

            // Unsubscribe from the old one
            DetachErrorHandler();

            _errorHandler = errorHandler;
            _errorHandler.ErrorOccurred += OnErrorHandlerError;
            _ownsErrorHandler = false;

            Debug.WriteLine("[ErrorHandlingBrowserComponent] Error handler set (external)");
        }
        
        /// <summary>
        /// Disconnects the current ErrorHandler
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
        /// Gets the ErrorHandler for direct access
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
        /// Error handler from WebViewErrorHandler
        /// </summary>
        private void OnErrorHandlerError(object? sender, BrowserErrorEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[ErrorHandlingBrowserComponent] Error received: {e.Error.Title} ({e.Error.ErrorName})");

                // Save the URL for retry
                if (!string.IsNullOrEmpty(e.Error.FailedUrl))
                {
                    _lastUrl = e.Error.FailedUrl;
                }

                // Notify external subscribers
                ErrorOccurred?.Invoke(this, e);

                // If the external handler did not mark as handled - show the error page
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
        /// Shows the error page
        /// </summary>
        public void ShowError(BrowserError error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            try
            {
                // Create the error page
                _currentErrorPage = new BrowserErrorPage(error);
                
                // Subscribe to error page events
                _currentErrorPage.RetryRequested += OnRetryRequested;
                _currentErrorPage.GoBackRequested += OnGoBackRequested;
                _currentErrorPage.GoHomeRequested += OnGoHomeRequested;
                _currentErrorPage.SearchRequested += OnSearchRequested;

                // Show error page, hide WebView
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
        /// Hides the error page and shows the WebView
        /// </summary>
        public void HideError()
        {
            try
            {
                // Unsubscribe from the old error page
                if (_currentErrorPage != null)
                {
                    _currentErrorPage.RetryRequested -= OnRetryRequested;
                    _currentErrorPage.GoBackRequested -= OnGoBackRequested;
                    _currentErrorPage.GoHomeRequested -= OnGoHomeRequested;
                    _currentErrorPage.SearchRequested -= OnSearchRequested;
                    _currentErrorPage = null;
                }

                // Hide error page, show WebView
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
        /// Retry loading the last URL
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
        /// Navigate to a URL
        /// </summary>
        public void Navigate(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;

            // Hide the error if it is currently shown
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
            
            // If there is no external handler - execute standard logic
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
        /// Gets the error from the page
        /// </summary>
        private BrowserError? GetErrorFromPage(BrowserErrorPage page)
        {
            return page?.Error;
        }

        /// <summary>
        /// Releases resources
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

