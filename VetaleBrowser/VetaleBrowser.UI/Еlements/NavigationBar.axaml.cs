using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.UI.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Еlements;

public class NavigationBar : TemplatedControl
{
    public static readonly StyledProperty<string> UrlProperty =
        AvaloniaProperty.Register<NavigationBar, string>(nameof(Url), "");

    public static readonly StyledProperty<bool> CanGoBackProperty =
        AvaloniaProperty.Register<NavigationBar, bool>(nameof(CanGoBack));

    public static readonly StyledProperty<bool> CanGoForwardProperty =
        AvaloniaProperty.Register<NavigationBar, bool>(nameof(CanGoForward));

    public static readonly StyledProperty<bool> IsSecureProperty =
        AvaloniaProperty.Register<NavigationBar, bool>(nameof(IsSecure));

    private Button? _backButton;
    private Button? _forwardButton;
    private Button? _reloadButton;
    private Button? _homeButton;
    private Button? _bookmarkButton;
    private Button? _toolsButton;
    private Button? _settingsButton;
    private TextBox? _addressBar;
    private WebViewManager? _webViewManager;
    private ISettingsService? _settingsService;

    // Подія навігації для vetale://
    public event EventHandler<string>? NavigateRequested;

    public string Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public bool CanGoBack
    {
        get => GetValue(CanGoBackProperty);
        set => SetValue(CanGoBackProperty, value);
    }

    public bool CanGoForward
    {
        get => GetValue(CanGoForwardProperty);
        set => SetValue(CanGoForwardProperty, value);
    }

    public bool IsSecure
    {
        get => GetValue(IsSecureProperty);
        set => SetValue(IsSecureProperty, value);
    }

    // Events for functionality that should be handled externally (like bookmarks, settings)
    public event EventHandler? BookmarkRequested;
    public event EventHandler? ToolsRequested;
    public event EventHandler? SettingsRequested;

    /// <summary>
    /// Initialize navigation bar with WebViewManager
    /// </summary>
    public void Initialize(WebViewManager webViewManager)
    {
        _webViewManager = webViewManager ?? throw new ArgumentNullException(nameof(webViewManager));
        System.Diagnostics.Trace.WriteLine("NavigationBar: Initialized with WebViewManager");
    }

    /// <summary>
    /// Set settings service for search engine configuration
    /// </summary>
    public void SetSettingsService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        System.Diagnostics.Trace.WriteLine("NavigationBar: Settings service set");
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Unsubscribe from old buttons
        if (_backButton != null)
            _backButton.Click -= OnBackButtonClick;
        if (_forwardButton != null)
            _forwardButton.Click -= OnForwardButtonClick;
        if (_reloadButton != null)
            _reloadButton.Click -= OnReloadButtonClick;
        if (_homeButton != null)
            _homeButton.Click -= OnHomeButtonClick;
        if (_bookmarkButton != null)
            _bookmarkButton.Click -= OnBookmarkButtonClick;
        if (_toolsButton != null)
            _toolsButton.Click -= OnToolsButtonClick;
        if (_settingsButton != null)
            _settingsButton.Click -= OnSettingsButtonClick;
        if (_addressBar != null)
            _addressBar.KeyDown -= OnAddressBarKeyDown;

        // Get new buttons
        _backButton = e.NameScope.Find<Button>("PART_BackButton");
        _forwardButton = e.NameScope.Find<Button>("PART_ForwardButton");
        _reloadButton = e.NameScope.Find<Button>("PART_ReloadButton");
        _homeButton = e.NameScope.Find<Button>("PART_HomeButton");
        _bookmarkButton = e.NameScope.Find<Button>("PART_BookmarkButton");
        _toolsButton = e.NameScope.Find<Button>("PART_ToolsButton");
        _settingsButton = e.NameScope.Find<Button>("PART_SettingsButton");
        _addressBar = e.NameScope.Find<TextBox>("PART_AddressBar");

        // Subscribe to new buttons
        if (_backButton != null)
            _backButton.Click += OnBackButtonClick;
        if (_forwardButton != null)
            _forwardButton.Click += OnForwardButtonClick;
        if (_reloadButton != null)
            _reloadButton.Click += OnReloadButtonClick;
        if (_homeButton != null)
            _homeButton.Click += OnHomeButtonClick;
        if (_bookmarkButton != null)
            _bookmarkButton.Click += OnBookmarkButtonClick;
        if (_toolsButton != null)
            _toolsButton.Click += OnToolsButtonClick;
        if (_settingsButton != null)
            _settingsButton.Click += OnSettingsButtonClick;
        if (_addressBar != null)
            _addressBar.KeyDown += OnAddressBarKeyDown;

        UpdateButtonStates();
    }

    private void OnBackButtonClick(object? sender, RoutedEventArgs e)
    {
        _webViewManager?.GoBack();
        System.Diagnostics.Trace.WriteLine("NavigationBar: Back button clicked");
    }

    private void OnForwardButtonClick(object? sender, RoutedEventArgs e)
    {
        _webViewManager?.GoForward();
        System.Diagnostics.Trace.WriteLine("NavigationBar: Forward button clicked");
    }

    private void OnReloadButtonClick(object? sender, RoutedEventArgs e)
    {
        _webViewManager?.Reload();
        System.Diagnostics.Trace.WriteLine("NavigationBar: Reload button clicked");
    }

    private async void OnHomeButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_webViewManager != null)
        {
            var home = await GetSearchHomePageAsync();
            if (InternalUrlHandler.IsInternalUrl(home))
            {
                NavigateRequested?.Invoke(this, home);
                return;
            }
            await _webViewManager.NavigateAsync(home);
            System.Diagnostics.Trace.WriteLine("NavigationBar: Home button clicked");
        }
    }

    private void OnBookmarkButtonClick(object? sender, RoutedEventArgs e)
    {
        BookmarkRequested?.Invoke(this, EventArgs.Empty);
        System.Diagnostics.Trace.WriteLine("NavigationBar: Bookmark button clicked");
    }

    private void OnToolsButtonClick(object? sender, RoutedEventArgs e)
    {
        ToolsRequested?.Invoke(this, EventArgs.Empty);
        System.Diagnostics.Trace.WriteLine("NavigationBar: Tools button clicked");
    }

    private void OnSettingsButtonClick(object? sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
        System.Diagnostics.Trace.WriteLine("NavigationBar: Settings button clicked");
    }

    private async void OnAddressBarKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && _addressBar != null && _webViewManager != null)
        {
            var url = _addressBar.Text ?? "";
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (!url.Contains("://"))
            {
                if (url.Contains(".") && !url.Contains(" "))
                {
                    url = "https://" + url;
                }
                else
                {
                    var searchUrl = await GetSearchEngineUrlAsync();
                    url = string.Format(searchUrl, Uri.EscapeDataString(url));
                }
            }

            if (InternalUrlHandler.IsInternalUrl(url))
            {
                NavigateRequested?.Invoke(this, url);
                System.Diagnostics.Trace.WriteLine($"NavigationBar: Internal navigate to {url}");
                return;
            }

            await _webViewManager.NavigateAsync(url);
            System.Diagnostics.Trace.WriteLine($"NavigationBar: Navigate to {url}");
        }
    }

    /// <summary>
    /// Get the search engine URL from settings or use default
    /// </summary>
    private async System.Threading.Tasks.Task<string> GetSearchEngineUrlAsync()
    {
        if (_settingsService != null)
        {
            try
            {
                var searchUrl = await _settingsService.GetSearchEngineUrlAsync();
                System.Diagnostics.Trace.WriteLine($"NavigationBar: Using search engine: {searchUrl}");
                return searchUrl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"NavigationBar: Error getting search engine: {ex}");
            }
        }
        
        // Fallback to Google
        return "https://www.google.com/search?q={0}";
    }

    private async System.Threading.Tasks.Task<string> GetSearchHomePageAsync()
    {
        try
        {
            var template = await GetSearchEngineUrlAsync();
            string basePart = template;
            var qIdx = template.IndexOf('?');
            if (qIdx >= 0)
                basePart = template.Substring(0, qIdx);
            if (Uri.TryCreate(basePart, UriKind.Absolute, out var uri))
            {
                return $"{uri.Scheme}://{uri.Host}/";
            }
        }
        catch { }
        return "https://www.google.com/";
    }

    private void UpdateButtonStates()
    {
        if (_backButton != null)
            _backButton.IsEnabled = CanGoBack;
        if (_forwardButton != null)
            _forwardButton.IsEnabled = CanGoForward;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CanGoBackProperty || change.Property == CanGoForwardProperty)
        {
            UpdateButtonStates();
        }
        else if (change.Property == UrlProperty && _addressBar != null)
        {
            _addressBar.Text = Url;
        }
    }
}
