using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

/// <summary>
/// Panel with Perplexity AI via WebView
/// AI search with sources - works without registration (limited)
/// </summary>
public partial class DuckDuckGoAiChatPanel : UserControl, IDisposable
{
    private Grid? _webViewContainer;
    private Border? _loadingOverlay;
    private TextBlock? _statusText;
    private IBrowserView? _webView;
    private bool _isDisposed;
    private bool _isLoaded;
    private bool _isWebViewReady;
    private string? _pendingMessage; // Message to send after loading
    private string? _lastQuery; // Last sent query (restored after WebView recreation)
    private int _addressSeq; // Guards against overlapping delayed handlers on rapid navigations
    
    // Perplexity AI URL - query can be passed via the q parameter
    private const string PerplexityAiUrl = "https://www.perplexity.ai";

    /// <summary>
    /// Event for navigation to a URL in the browser
    /// </summary>
    public event EventHandler<string>? NavigateRequested;

    public DuckDuckGoAiChatPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _webViewContainer = this.FindControl<Grid>("WebViewContainer");
        _loadingOverlay = this.FindControl<Border>("LoadingOverlay");
        _statusText = this.FindControl<TextBlock>("StatusText");

        if (!_isLoaded)
        {
            _isLoaded = true;
            InitializeWebView();
        }

        Debug.WriteLine("[DuckDuckGoAiChat] Panel loaded");
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // Dispose the WebView on unload: hidden results pages stay in tab history,
        // and each preserved WebView is a live Chromium copy leaking memory.
        // The view is recreated on next load (last query is restored).
        DisposeWebView();
        _isLoaded = false;
        _isWebViewReady = false;
        Debug.WriteLine("[DuckDuckGoAiChat] Panel unloaded (WebView disposed)");
    }

    private void InitializeWebView()
    {
        if (_webViewContainer == null) return;
        if (_webView != null) return; // Already initialized

        try
        {
            ShowLoading(true);
            UpdateStatus("Initializing...");

            // Clear the container
            _webViewContainer.Children.Clear();

            // Create WebView with proper stretch properties
            _webView = new CefSharpAdapter();
            _webView.View.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            _webView.View.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            
            // Subscribe to events
            _webView.PropertyChanged += OnWebViewPropertyChanged;
            
            // Add to container
            _webViewContainer.Children.Add(_webView.View);

            // Navigate to Perplexity AI (restore last query after recreation)
            _webView.Address = BuildSearchUrl(_lastQuery);

            Debug.WriteLine($"[PerplexityAiChat] WebView created, navigating to: {_webView.Address}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PerplexityAiChat] Error initializing WebView: {ex.Message}");
            ShowError($"Loading error: {ex.Message}");
        }
    }

    private static string BuildSearchUrl(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return PerplexityAiUrl;
        return $"{PerplexityAiUrl}/search?q={Uri.EscapeDataString(query)}";
    }

    private void OnWebViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_webView == null || e.Property.Name != "Address") return;

        var address = _webView.Address;
        Debug.WriteLine($"[DuckDuckGoAiChat] Address changed: {address}");

        // Guard: only the latest navigation runs the delayed ready/pending logic.
        var seq = ++_addressSeq;

        // Inject scripts to improve UX
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(2000); // Wait for the page to load (DuckDuckGo requires more time)
            if (seq != _addressSeq || _isDisposed || _webView == null) return;
            
            // First try to accept consent
            await AcceptTermsIfNeededAsync();
            
            await InjectCustomScriptsAsync();
            ShowLoading(false);
            UpdateStatus("Ready");
            
            // Mark WebView as ready
            _isWebViewReady = true;
            
            // If there is a pending message - send it with a delay
            if (!string.IsNullOrWhiteSpace(_pendingMessage))
            {
                Debug.WriteLine($"[DuckDuckGoAiChat] Sending pending message: {_pendingMessage}");
                await Task.Delay(1000); // Longer delay for stability after accepting consent
                if (seq != _addressSeq || _isDisposed || _webView == null) return;
                var pending = _pendingMessage;
                _pendingMessage = null;
                await SendMessageInternalAsync(pending);
            }
        });
    }
    
    /// <summary>
    /// Automatically accepts Terms/Privacy if a consent dialog is shown
    /// </summary>
    private async Task AcceptTermsIfNeededAsync()
    {
        if (_webView == null) return;
        
        try
        {
            // Script for automatically accepting DuckDuckGo consent
            var js = @"
                (function() {
                    try {
                        console.log('[Vetale] Checking for consent dialog...');
                        
                        // Search for accept buttons (various options)
                        var acceptButtons = [
                            // DuckDuckGo AI Chat specific
                            document.querySelector('button[data-testid=""chat-terms-accept""]'),
                            document.querySelector('button[data-testid=""accept-terms""]'),
                            document.querySelector('button[aria-label*=""Accept""]'),
                            document.querySelector('button[aria-label*=""agree"" i]'),
                            document.querySelector('button[aria-label*=""Accept"" i]'),
                            // General selectors for consent buttons
                            document.querySelector('button.terms-accept'),
                            document.querySelector('button.accept-btn'),
                            document.querySelector('button.consent-accept'),
                            document.querySelector('[class*=""accept"" i] button'),
                            document.querySelector('[class*=""consent"" i] button'),
                            document.querySelector('[class*=""terms"" i] button'),
                            // Search by text
                            Array.from(document.querySelectorAll('button')).find(b => 
                                b.textContent && (
                                    b.textContent.toLowerCase().includes('accept') ||
                                    b.textContent.toLowerCase().includes('agree') ||
                                    b.textContent.toLowerCase().includes('i agree') ||
                                    b.textContent.toLowerCase().includes('get started') ||
                                    b.textContent.toLowerCase().includes('start chat') ||
                                    b.textContent.toLowerCase().includes('continue')
                                )
                            )
                        ];
                        
                        for (var i = 0; i < acceptButtons.length; i++) {
                            var btn = acceptButtons[i];
                            if (btn && btn.offsetParent !== null) { // Check that the button is visible
                                console.log('[Vetale] Found accept button:', btn.textContent || btn.outerHTML.substring(0, 100));
                                btn.click();
                                console.log('[Vetale] Clicked accept button!');
                                return true;
                            }
                        }
                        
                        // Also look for consent checkboxes
                        var checkboxes = document.querySelectorAll('input[type=""checkbox""]');
                        checkboxes.forEach(function(cb) {
                            if (!cb.checked) {
                                cb.click();
                                console.log('[Vetale] Clicked checkbox');
                            }
                        });
                        
                        console.log('[Vetale] No consent dialog found or already accepted');
                        return false;
                    } catch(e) {
                        console.error('[Vetale] Error accepting terms:', e);
                        return false;
                    }
                })();
            ";
            
            var result = await _webView.EvaluateScriptAsync<bool>(js);
            Debug.WriteLine($"[DuckDuckGoAiChat] AcceptTerms result: {result}");
            
            if (result)
            {
                // If consent was accepted - wait for UI to update
                await Task.Delay(1500);
                // Try again in case there is another dialog
                await _webView.EvaluateScriptAsync<bool>(js);
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuckDuckGoAiChat] Error accepting terms: {ex.Message}");
        }
    }

    /// <summary>
    /// Injects custom scripts to improve UX
    /// </summary>
    private async Task InjectCustomScriptsAsync()
    {
        if (_webView == null) return;

        try
        {
            // Script for:
            // 1. Blocking opening new windows
            // 2. Hiding unnecessary UI elements (optional)
            // 3. Handling external links
            var js = @"
                (function(){
                    if(window.__vetale_ddg_injected__) return;
                    window.__vetale_ddg_injected__ = true;
                    
                    // Block window.open
                    var originalOpen = window.open;
                    window.open = function(url) {
                        try {
                            if(url) location.href = url;
                        } catch(e) {}
                        return null;
                    };
                    
                    // Intercept clicks on links with target=_blank
                    document.addEventListener('click', function(e) {
                        try {
                            var el = e.target;
                            while(el && el.tagName !== 'A') { el = el.parentElement; }
                            if(!el) return;
                            
                            var href = el.getAttribute('href');
                            if(!href) return;
                            
                            var target = el.getAttribute('target');
                            if(target && target.toLowerCase() === '_blank') {
                                e.preventDefault();
                                e.stopPropagation();
                                // Open in the same window
                                location.href = href;
                            }
                        } catch(_) {}
                    }, true);
                    
                    // Block middle mouse button click
                    document.addEventListener('auxclick', function(e) {
                        try {
                            if(e.button === 1) {
                                var el = e.target;
                                while(el && el.tagName !== 'A') { el = el.parentElement; }
                                if(!el) return;
                                
                                var href = el.getAttribute('href');
                                if(href) {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    location.href = href;
                                }
                            }
                        } catch(_) {}
                    }, true);
                    
                    console.log('[Vetale] DuckDuckGo AI Chat scripts injected');
                })();
            ";

            await _webView.EvaluateScriptAsync<object>(js);
            Debug.WriteLine("[DuckDuckGoAiChat] Custom scripts injected successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuckDuckGoAiChat] Failed to inject scripts: {ex.Message}");
        }
    }

    /// <summary>
    /// Programmatically send a message to the chat
    /// </summary>
    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        
        Debug.WriteLine($"[PerplexityAiChat] SendMessageAsync called: {message}");
        _lastQuery = message;

        // For Perplexity it is best to use URL with the q parameter
        // This will automatically perform the search
        if (_webView != null)
        {
            var searchUrl = BuildSearchUrl(message);
            
            Debug.WriteLine($"[PerplexityAiChat] Navigating to search URL: {searchUrl}");
            _webView.Address = searchUrl;
            return;
        }
        
        // If WebView is not ready yet - queue the message
        if (!_isWebViewReady)
        {
            Debug.WriteLine($"[PerplexityAiChat] WebView not ready, queuing message");
            _pendingMessage = message;
            return;
        }
        
        await SendMessageInternalAsync(message);
    }
    
    /// <summary>
    /// Internal method for sending a message via URL navigation
    /// </summary>
    private Task SendMessageInternalAsync(string message)
    {
        if (_webView == null || string.IsNullOrWhiteSpace(message)) 
            return Task.CompletedTask;

        try
        {
            // For Perplexity it is better to use the URL directly
            var searchUrl = BuildSearchUrl(message);
            
            Debug.WriteLine($"[PerplexityAiChat] Navigating to: {searchUrl}");
            _webView.Address = searchUrl;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PerplexityAiChat] Error sending message: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (_webView != null)
        {
            try
            {
                ShowLoading(true);
                UpdateStatus("Refreshing...");
                _webView.Reload();
                Debug.WriteLine("[DuckDuckGoAiChat] Refreshing page");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DuckDuckGoAiChat] Error refreshing: {ex.Message}");
                ShowLoading(false);
            }
        }
    }

    private void OnNewChatClick(object? sender, RoutedEventArgs e)
    {
        if (_webView != null)
        {
            try
            {
                ShowLoading(true);
                UpdateStatus("New chat...");
                _isWebViewReady = false;
                // Reload the page for a new chat
                _webView.Address = PerplexityAiUrl;
                Debug.WriteLine("[PerplexityAiChat] Starting new chat");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerplexityAiChat] Error starting new chat: {ex.Message}");
                ShowLoading(false);
            }
        }
    }

    private void ShowLoading(bool show)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_loadingOverlay != null)
            {
                _loadingOverlay.IsVisible = show;
            }
        });
    }

    private void UpdateStatus(string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_statusText != null)
            {
                _statusText.Text = status;
            }
        });
    }

    private void ShowError(string message)
    {
        if (_webViewContainer == null) return;

        Dispatcher.UIThread.Post(() =>
        {
            ShowLoading(false);
            _webViewContainer.Children.Clear();
            _webViewContainer.Children.Add(new StackPanel
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "❌",
                        FontSize = 48,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = message,
                        Foreground = Avalonia.Media.Brushes.Red,
                        FontSize = 14,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    },
                    new Button
                    {
                        Content = "Retry",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Command = new RelayCommand(() => InitializeWebView())
                    }
                }
            });
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try { Loaded -= OnLoaded; } catch { }
        try { Unloaded -= OnUnloaded; } catch { }

        DisposeWebView();

        Debug.WriteLine("[DuckDuckGoAiChat] Disposed");
    }

    private void DisposeWebView()
    {
        try
        {
            if (_webView != null)
            {
                _webView.PropertyChanged -= OnWebViewPropertyChanged;
                if (_webViewContainer != null)
                    _webViewContainer.Children.Remove(_webView.View);
                _webView.Dispose();
                _webView = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuckDuckGoAiChat] Error disposing WebView: {ex.Message}");
        }
    }

    /// <summary>
    /// Simple RelayCommand for buttons
    /// </summary>
    private class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        
        public RelayCommand(Action execute) => _execute = execute;
        
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}


