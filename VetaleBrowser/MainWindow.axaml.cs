using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using VetaleBrowser.VetaleBrowser.UI.Scripts;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.UI.Еlements;
using VetaleBrowser.VetaleBrowser.UI.Services;
using WebViewControl;
using Avalonia.Threading;
using Avalonia;

namespace VetaleBrowser;

public partial class MainWindow : Window
{
    private readonly WindowManager _windowManager;
    private readonly WebViewManager _webViewManager;
    private readonly IFaviconService _faviconService = new FaviconService();

    // Hold a reference to the hosted WebView control for convenience
    private WebView? _webViewControl;

    // Polling support for robust favicon and title updates
    private readonly DispatcherTimer _faviconPollTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private string? _lastFaviconUrl;
    private string? _lastPageTitle;

    public WebViewManager WebView => _webViewManager;

    public MainWindow()
    {
        InitializeComponent();
        _windowManager = new WindowManager(this);
        _webViewManager = new WebViewManager();

        // Initialize after the window is loaded
        this.Loaded += OnWindowLoaded;
        this.Closed += OnWindowClosed;

        // Timer to check for URL/title changes periodically (covers redirects and edge cases)
        _faviconPollTimer.Tick += async (_, _) =>
        {
            try
            {
                var url = _webViewManager.GetCurrentUrl();
                if (!string.IsNullOrWhiteSpace(url) && !string.Equals(url, _lastFaviconUrl, StringComparison.Ordinal))
                {
                    _lastFaviconUrl = url;
                    await UpdateFaviconAsync(url);
                    // Update the tab title based on URL while page title is not yet available
                    await UpdateTabTitleAsync(null, url);
                }

                // Additionally, check for title updates if possible
                if (_webViewControl != null)
                {
                    var currentTitle = TryGetWebViewTitle(_webViewControl);
                    if (!string.IsNullOrWhiteSpace(currentTitle) && !string.Equals(currentTitle, _lastPageTitle, StringComparison.Ordinal))
                    {
                        _lastPageTitle = currentTitle;
                        await UpdateTabTitleAsync(currentTitle, url);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Poll tick error: {ex.Message}");
            }
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("MainWindow: OnWindowLoaded called");
        
        try
        {
            // Get the container from XAML
            var container = this.FindControl<Grid>("WebViewContainer");
            
            if (container != null)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Found WebViewContainer");
                
                // Create WebView from WebViewControl package
                var webView = new WebView();
                webView.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                webView.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                
                // Clear container and add WebView
                container.Children.Clear();
                container.Children.Add(webView);

                // Cache instance for later polling
                _webViewControl = webView;
                
                System.Diagnostics.Debug.WriteLine("MainWindow: WebView added to container");
                
                // Initialize WebViewManager with the control
                _webViewManager.Initialize(webView);
                
                // Subscribe to navigation initiated via manager
                _webViewManager.Navigated += async (_, url) =>
                {
                    _lastFaviconUrl = url; // track latest
                    await UpdateFaviconAsync(url);
                    await UpdateTabTitleAsync(null, url); // provisional title from URL/domain
                };

                // Also subscribe to WebView property changes to capture in-page navigations, redirects, etc.
                webView.PropertyChanged += WebView_OnPropertyChanged;
                
                System.Diagnostics.Debug.WriteLine("MainWindow: WebViewManager initialized");
                
                // Initialize NavigationBar with WebViewManager
                var navigationBar = this.FindControl<NavigationBar>("NavigationBar");
                if (navigationBar != null)
                {
                    navigationBar.Initialize(_webViewManager);
                    
                    // Subscribe only to events that need external handling
                    navigationBar.BookmarkRequested += OnBookmarkRequested;
                    navigationBar.ToolsRequested += OnToolsRequested;
                    navigationBar.SettingsRequested += OnSettingsRequested;
                    
                    System.Diagnostics.Debug.WriteLine("MainWindow: NavigationBar initialized");
                }
                
                // Start favicon/title polling
                _faviconPollTimer.Start();
                
                // Navigate to a default page
                await _webViewManager.NavigateAsync("https://www.google.com");
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Navigation command sent");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: WebViewContainer NOT found!");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Error initializing WebView: {ex}");
            
            // Show error in UI
            var container = this.FindControl<Grid>("WebViewContainer");
            if (container != null)
            {
                container.Children.Clear();
                container.Children.Add(new TextBlock
                {
                    Text = $"Помилка ініціалізації WebView:\n{ex.Message}\n\nПереконайтесь що WebView2 Runtime встановлено.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Foreground = Avalonia.Media.Brushes.Red,
                    FontSize = 14,
                    Margin = new Thickness(20)
                });
            }
        }
    }

    // Try to read the document title from the WebView via reflection (supports various wrappers)
    private static string? TryGetWebViewTitle(WebView vw)
    {
        try
        {
            var t = vw.GetType();
            var prop = t.GetProperty("Title") ?? t.GetProperty("DocumentTitle");
            return prop?.GetValue(vw) as string;
        }
        catch
        {
            return null;
        }
    }

    // React to WebView property changes (e.g., Address changes when user clicks links)
    private async void WebView_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        try
        {
            if (sender is WebView vw && e.Property?.Name != null)
            {
                var prop = e.Property.Name;
                if (prop == "Address")
                {
                    var url = _webViewManager.GetCurrentUrl();
                    _lastFaviconUrl = url; // keep poll baseline in sync
                    // Update address bar text
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var nav = this.FindControl<NavigationBar>("NavigationBar");
                        if (nav != null)
                        {
                            nav.Url = url ?? string.Empty;
                            // Also reflect back/forward state if available
                            nav.CanGoBack = vw.CanGoBack;
                            nav.CanGoForward = vw.CanGoForward;
                        }
                    });

                    // Update favicon for new address
                    await UpdateFaviconAsync(url);

                    // Update tab text from URL while title is not ready
                    await UpdateTabTitleAsync(null, url);
                }
                else if (prop == "CanGoBack" || prop == "CanGoForward")
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var nav = this.FindControl<NavigationBar>("NavigationBar");
                        if (nav != null)
                        {
                            nav.CanGoBack = (prop == "CanGoBack") ? vw.CanGoBack : nav.CanGoBack;
                            nav.CanGoForward = (prop == "CanGoForward") ? vw.CanGoForward : nav.CanGoForward;
                        }
                    });
                }
                else if (prop == "Title")
                {
                    // Prefer the document title when available
                    string? pageTitle = null;
                    try
                    {
                        pageTitle = vw.GetType().GetProperty("Title")?.GetValue(vw) as string;
                    }
                    catch { /* ignore reflection issues */ }

                    _lastPageTitle = pageTitle ?? _lastPageTitle;
                    await UpdateTabTitleAsync(pageTitle, vw.Address);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] WebView_OnPropertyChanged error: {ex.Message}");
        }
    }

    private async Task UpdateFaviconAsync(string? address)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return;

            // Determine scale for better icon size
            var visualRoot = this.GetVisualRoot();
            double scale = 1.0;
            if (visualRoot is TopLevel top)
            {
                scale = top.RenderScaling;
            }

            var size = scale >= 1.5 ? 48 : 32;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateFavicon: url={uri} size={size} scale={scale:0.00}");

            var image = await _faviconService.GetFaviconAsync(uri, size);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var tab = this.FindControl<Tab>("ActiveTab");
                if (tab != null)
                {
                    tab.FaviconSource = image; // can be null, template keeps default bg
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Favicon applied: hasImage={(image != null)}");
                }
            });
        }
        catch (Exception ex)
        {
            // ignore favicon failures
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateFavicon error: {ex.Message}");
        }
    }

    // Compute and apply a friendly tab title from the page title or URL
    private async Task UpdateTabTitleAsync(string? pageTitle, string? url)
    {
        try
        {
            var friendly = ComputeTitle(pageTitle, url);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var tab = this.FindControl<Tab>("ActiveTab");
                if (tab != null)
                {
                    tab.Title = friendly;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateTabTitle error: {ex.Message}");
        }
    }

    private static string ComputeTitle(string? pageTitle, string? url)
    {
        // Use document title if provided
        if (!string.IsNullOrWhiteSpace(pageTitle))
        {
            return pageTitle.Trim();
        }

        // Fallback to host if we have a URL
        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(4);
            return host;
        }

        return "New Tab";
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _webViewManager.Dispose();
        if (_faviconService is IDisposable d) d.Dispose();
        _faviconPollTimer.Stop();
    }

    public async Task NavigateToAsync(string url)
    {
        await _webViewManager.NavigateAsync(url);
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        _windowManager.Minimize();
    }

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
    {
        _windowManager.ToggleMaximize();
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        _windowManager.Close();
    }

    // Подвійний клік по верхній панелі -> максимізувати/відновити
    private void TopBar_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        _windowManager.OnTopBarDoubleTapped(sender, e);
    }

    // Перетягування вікна при натисканні на верхню панель
    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _windowManager.TryBeginMoveDrag(e);
        }
    }

    // Event handlers for functionality that needs to be handled externally
    private void OnBookmarkRequested(object? sender, EventArgs e)
    {
        // TODO: Implement bookmark functionality
        System.Diagnostics.Debug.WriteLine("MainWindow: Bookmark button clicked");
    }

    private void OnToolsRequested(object? sender, EventArgs e)
    {
        // TODO: Implement tools menu
        System.Diagnostics.Debug.WriteLine("MainWindow: Tools button clicked");
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        // TODO: Implement settings window
        System.Diagnostics.Debug.WriteLine("MainWindow: Settings button clicked");
    }
}
