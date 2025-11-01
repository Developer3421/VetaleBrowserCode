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
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;
using System.Linq;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.UI.Windows; // added for SettingsWindow and ToolsWindow

namespace VetaleBrowser;

public partial class MainWindow : Window
{
    private readonly WindowManager _windowManager;
    private readonly IFaviconService _faviconService = new FaviconService();

    private readonly TabsManager _tabs = new();

    private StackPanel? _tabsHost;
    private Button? _addTabButton;

    // Keep track of which worker's WebView we're listening to
    private TabWorker? _subscribedWorker;

    // Polling support for robust favicon and title updates
    private readonly DispatcherTimer _faviconPollTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private string? _lastFaviconUrl;
    private string? _lastPageTitle;

    public MainWindow()
    {
        InitializeComponent();
        _windowManager = new WindowManager(this);

        // Initialize after the window is loaded
        this.Loaded += OnWindowLoaded;
        this.Closed += OnWindowClosed;

        _tabs.TabActivated += OnTabActivated;
        _tabs.TabClosed += OnTabClosed;
        // Removed TabCreated subscription to avoid duplicate Tab controls
        // _tabs.TabCreated += OnTabCreated;

        // Timer to check for URL/title changes periodically (covers redirects and edge cases)
        _faviconPollTimer.Tick += async (_, _) =>
        {
            try
            {
                var active = _tabs.Active;
                if (active != null)
                {
                    var url = active.Manager.GetCurrentUrl();
                    if (!string.IsNullOrWhiteSpace(url) && !string.Equals(url, _lastFaviconUrl, StringComparison.Ordinal))
                    {
                        _lastFaviconUrl = url;
                        await UpdateFaviconAsync(url);
                        await UpdateTabTitleAsync(null, url);
                    }

                    var currentTitle = TryGetWebViewTitle(active.WebView);
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
        _tabsHost = this.FindControl<StackPanel>("TabsHost");
        _addTabButton = this.FindControl<Button>("PART_AddTabButton");
        if (_addTabButton != null)
            _addTabButton.Click += (_, __) => CreateNewTab("https://www.google.com");
    }

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("MainWindow: OnWindowLoaded called");

        try
        {
            // Ensure there's at least one tab
            if (_tabs.Active == null)
            {
                CreateNewTab("https://www.google.com");
            }

            // Initialize NavigationBar with the active tab's manager
            var navigationBar = this.FindControl<NavigationBar>("NavigationBar");
            if (navigationBar != null && _tabs.Active != null)
            {
                navigationBar.Initialize(_tabs.Active.Manager);

                // Subscribe only to events that need external handling
                navigationBar.BookmarkRequested += OnBookmarkRequested;
                navigationBar.ToolsRequested += OnToolsRequested;
                navigationBar.SettingsRequested += OnSettingsRequested;

                System.Diagnostics.Debug.WriteLine("MainWindow: NavigationBar initialized");
            }

            // Start favicon/title polling
            _faviconPollTimer.Start();

            // Navigate default handled per tab creation
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Error initializing: {ex}");
            ShowErrorInWebViewContainer($"Помилка ініціалізації: {ex.Message}");
        }
    }

    private void ShowErrorInWebViewContainer(string message)
    {
        var container = this.FindControl<Grid>("WebViewContainer");
        if (container != null)
        {
            container.Children.Clear();
            container.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = Avalonia.Media.Brushes.Red,
                FontSize = 14,
                Margin = new Thickness(20)
            });
        }
    }

    private void CreateNewTab(string? initialUrl = null)
    {
        var worker = _tabs.Create(initialUrl);
        AddTabControlForWorker(worker);
        ActivateWorker(worker);
    }

    private void AddTabControlForWorker(TabWorker worker)
    {
        if (_tabsHost == null) return;

        var tab = new Tab
        {
            Title = "New Tab",
            IsActive = worker.IsActive,
            IsCloseButtonVisible = true,
            Width = 200
        };

        tab.Clicked += (_, __) => ActivateWorker(worker);
        tab.CloseRequested += (_, __) =>
        {
            // Remove the specific Tab control that was clicked, then close its worker
            if (_tabsHost != null)
            {
                _tabsHost.Children.Remove(tab);
            }
            _tabs.Close(worker);
        };

        _tabsHost.Children.Add(tab);
    }

    private int GetChildIndexForWorker(TabWorker worker)
    {
        // Children[0] is the AddTab button; tabs start from index 1
        var workerIndex = _tabs.Workers.ToList().IndexOf(worker);
        return workerIndex < 0 ? -1 : workerIndex + 1;
    }

    private void ActivateWorker(TabWorker worker)
    {
        _tabs.Activate(worker);

        // Update UI: swap WebView into container, update tab headers, rebind NavigationBar
        var container = this.FindControl<Grid>("WebViewContainer");
        if (container != null)
        {
            container.Children.Clear();
            container.Children.Add(worker.WebView);
        }

        // Update tabs active state and titles
        if (_tabsHost != null)
        {
            for (int widx = 0; widx < _tabs.Workers.Count; widx++)
            {
                var childIdx = widx + 1; // account for add button
                if (childIdx >= 0 && childIdx < _tabsHost.Children.Count && _tabsHost.Children[childIdx] is Tab t)
                {
                    var w = _tabs.Workers[widx];
                    t.IsActive = w == worker;
                    var interim = ComputeTitle(w.Title, w.Address);
                    t.Title = interim;
                }
            }
        }

        // Bind navigation bar to active manager
        var navigationBar = this.FindControl<NavigationBar>("NavigationBar");
        if (navigationBar != null)
        {
            navigationBar.Initialize(worker.Manager);
            navigationBar.Url = worker.Address ?? string.Empty;
            navigationBar.CanGoBack = worker.WebView.CanGoBack;
            navigationBar.CanGoForward = worker.WebView.CanGoForward;
        }

        WireActiveWebViewPropertyChanged(worker);

        _lastFaviconUrl = worker.Address;
        _lastPageTitle = worker.Title;
    }

    private void WireActiveWebViewPropertyChanged(TabWorker worker)
    {
        if (_subscribedWorker != null)
        {
            try { _subscribedWorker.WebView.PropertyChanged -= WebView_OnPropertyChanged; } catch { }
        }
        _subscribedWorker = worker;
        _subscribedWorker.WebView.PropertyChanged += WebView_OnPropertyChanged;
    }

    // React to WebView property changes (e.g., Address changes, CanGoBack/Forward, Title)
    private async void WebView_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        try
        {
            if (_tabs.Active == null) return;
            var vw = _tabs.Active.WebView;
            if (!ReferenceEquals(sender, vw)) return; // only react to active

            var prop = e.Property?.Name;
            if (prop == "Address")
            {
                var url = _tabs.Active.Manager.GetCurrentUrl();
                _lastFaviconUrl = url; // keep poll baseline in sync
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var nav = this.FindControl<NavigationBar>("NavigationBar");
                    if (nav != null)
                    {
                        nav.Url = url ?? string.Empty;
                        nav.CanGoBack = vw.CanGoBack;
                        nav.CanGoForward = vw.CanGoForward;
                    }
                });

                await UpdateFaviconAsync(url);
                await UpdateTabTitleAsync(null, url);
            }
            else if (prop == "CanGoBack" || prop == "CanGoForward")
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var nav = this.FindControl<NavigationBar>("NavigationBar");
                    if (nav != null)
                    {
                        nav.CanGoBack = vw.CanGoBack;
                        nav.CanGoForward = vw.CanGoForward;
                    }
                });
            }
            else if (prop == "Title")
            {
                string? pageTitle = null;
                try { pageTitle = vw.GetType().GetProperty("Title")?.GetValue(vw) as string; } catch { }
                _lastPageTitle = pageTitle ?? _lastPageTitle;
                await UpdateTabTitleAsync(pageTitle, vw.Address);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] WebView_OnPropertyChanged error: {ex.Message}");
        }
    }

    private void OnTabCreated(object? sender, TabWorker e)
    {
        // No-op: UI for tabs is created explicitly in CreateNewTab to avoid duplicates
    }

    private void CloseWorker(TabWorker worker)
    {
        // Remove corresponding Tab control by index mapping (legacy path)
        if (_tabsHost != null)
        {
            var childIdx = GetChildIndexForWorker(worker);
            if (childIdx >= 0 && childIdx < _tabsHost.Children.Count)
            {
                _tabsHost.Children.RemoveAt(childIdx);
            }
        }

        _tabs.Close(worker);
    }

    private void OnTabActivated(object? sender, TabWorker e)
    {
        // Sync UI states when manager reports activation (already handled by ActivateWorker)
    }

    private void OnTabClosed(object? sender, TabWorker e)
    {
        // If the closed tab was active, Activate() in TabsManager already switched to another
        if (_tabs.Active != null)
        {
            ActivateWorker(_tabs.Active);
        }
        else
        {
            // Ensure at least one tab exists
            CreateNewTab("https://www.google.com");
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
                if (_tabsHost != null && _tabs.Active != null)
                {
                    var idx = _tabs.Workers.ToList().IndexOf(_tabs.Active);
                    var childIdx = idx + 1; // account for add button
                    if (idx >= 0 && childIdx < _tabsHost.Children.Count && _tabsHost.Children[childIdx] is Tab tab)
                    {
                        tab.FaviconSource = image;
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Favicon applied: hasImage={(image != null)}");
                    }
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
                if (_tabsHost != null && _tabs.Active != null)
                {
                    var idx = _tabs.Workers.ToList().IndexOf(_tabs.Active);
                    var childIdx = idx + 1; // account for add button
                    if (idx >= 0 && childIdx < _tabsHost.Children.Count && _tabsHost.Children[childIdx] is Tab tab)
                    {
                        tab.Title = friendly;
                    }
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
        if (_subscribedWorker != null)
        {
            try { _subscribedWorker.WebView.PropertyChanged -= WebView_OnPropertyChanged; } catch { }
            _subscribedWorker = null;
        }

        _tabs.Dispose();
        if (_faviconService is IDisposable d) d.Dispose();
        _faviconPollTimer.Stop();
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
        try
        {
            if (_tabs.Active == null)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: No active tab to bookmark");
                return;
            }

            // Отримуємо поточний URL та заголовок
            var currentUrl = _tabs.Active.Manager.GetCurrentUrl() ?? string.Empty;
            var currentTitle = _tabs.Active.Title ?? TryGetWebViewTitle(_tabs.Active.WebView) ?? "Без назви";

            System.Diagnostics.Debug.WriteLine($"MainWindow: Opening bookmarks window with URL: {currentUrl}, Title: {currentTitle}");

            // Відкриваємо вікно закладок з формою додавання
            var bookmarksWindow = new BookmarksWindow(currentUrl, currentTitle);
            bookmarksWindow.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Error opening bookmarks window: {ex}");
        }
    }

    private void OnToolsRequested(object? sender, EventArgs e)
    {
        // Open tools window styled like the main window
        try
        {
            var tools = new ToolsWindow();
            tools.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to open ToolsWindow: {ex}");
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        // Open settings window styled like the main window
        try
        {
            var settings = new SettingsWindow();
            settings.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to open SettingsWindow: {ex}");
        }
    }
}
