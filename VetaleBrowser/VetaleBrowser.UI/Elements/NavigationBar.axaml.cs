using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.UI.Services;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Search.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Elements;

/// <summary>
/// Ideal navigation bar:
/// - Single responsibility: renders state + raises intents (no direct engine calls for Back/Forward).
/// - Navigation happens ONLY on Enter or suggestion click. No auto-navigate on typing.
/// - Button state driven by SetState() from MainWindow (single source of truth: TabWorker.History).
/// </summary>
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

    public static readonly StyledProperty<SecurityStatus> SecurityStatusProperty =
        AvaloniaProperty.Register<NavigationBar, SecurityStatus>(nameof(SecurityStatus), SecurityStatus.Unknown);

    private Button? _backButton;
    private Button? _forwardButton;
    private Button? _reloadButton;
    private Button? _homeButton;
    private Button? _bookmarkButton;
    private Button? _toolsButton;
    private Button? _settingsButton;
    private TextBox? _addressBar;
    private Popup? _suggestionsPopup;
    private ItemsControl? _suggestionsList;
    private Border? _securityIcon;
    private Avalonia.Controls.Shapes.Path? _securityPath;
    private TextBlock? _securityText;

    private readonly ObservableCollection<SearchSuggestion> _suggestions = new();
    private ISettingsService? _settingsService;
    private ISuggestionsService? _suggestionsService;
    private ISecurityCheckService? _securityCheckService;
    private CancellationTokenSource? _suggestionsCts;
    private CancellationTokenSource? _securityCheckCts;

    // While we programmatically set address text, ignore TextChanged.
    private bool _syncingAddressText;

    /// <summary>Address submitted via Enter or suggestion.</summary>
    public event EventHandler<string>? NavigateRequested;
    /// <summary>Back / Forward intents — handled by MainWindow via TabWorker.</summary>
    public event EventHandler? BackRequested;
    public event EventHandler? ForwardRequested;
    public event EventHandler? ReloadRequested;
    public event EventHandler? HomeRequested;
    public event EventHandler? BookmarkRequested;
    public event EventHandler? ToolsRequested;
    public event EventHandler? SettingsRequested;

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

    public SecurityStatus SecurityStatus
    {
        get => GetValue(SecurityStatusProperty);
        set => SetValue(SecurityStatusProperty, value);
    }

    /// <summary>
    /// Single entry point for state updates. Call from MainWindow whenever
    /// TabWorker.History or Address changes. Guaranteed UI-thread safe.
    /// </summary>
    public void SetState(string? url, bool canGoBack, bool canGoForward)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SetState(url, canGoBack, canGoForward));
            return;
        }
        if (url != null && !IsAddressFocused())
            Url = url; // triggers address sync via OnPropertyChanged
        CanGoBack = canGoBack;
        CanGoForward = canGoForward;
    }

    private bool IsAddressFocused() => _addressBar?.IsFocused == true;

    public void SetSettingsService(ISettingsService s) => _settingsService = s;
    public void SetSuggestionsService(ISuggestionsService s) => _suggestionsService = s;
    public void SetSecurityCheckService(ISecurityCheckService s) => _securityCheckService = s;

    [Obsolete("Use BindWorker instead.")]
    public void Initialize(WebViewManager webViewManager) { }

    /// <summary>
    /// Binds the bar to the active worker. The bar subscribes to
    /// HistoryChanged + NavigationChanged itself, so Back/Forward buttons
    /// always reflect the real history — no manual pushing needed.
    /// Call on every tab switch / new tab.
    /// </summary>
    public void BindWorker(TabWorker? worker)
    {
        if (_boundWorker != null)
        {
            _boundWorker.History.HistoryChanged -= OnBoundHistoryChanged;
            _boundWorker.NavigationChanged -= OnBoundNavigationChanged;
        }
        _boundWorker = worker;
        if (_boundWorker != null)
        {
            _boundWorker.History.HistoryChanged += OnBoundHistoryChanged;
            _boundWorker.NavigationChanged += OnBoundNavigationChanged;
        }
        RefreshFromBoundWorker();
    }

    [Obsolete("Use BindWorker instead.")]
    public void SetTabWorker(TabWorker? tabWorker) => BindWorker(tabWorker);

    private TabWorker? _boundWorker;

    private void OnBoundHistoryChanged(object? s, EventArgs e) => RefreshFromBoundWorker();

    private void OnBoundNavigationChanged(object? s, VetaleBrowser.Core.Scripts.Models.NavigationEntry e)
        => RefreshFromBoundWorker();

    private void RefreshFromBoundWorker()
    {
        if (_boundWorker == null) return;
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshFromBoundWorker);
            return;
        }
        CanGoBack = _boundWorker.CanGoBack;
        CanGoForward = _boundWorker.CanGoForward;
        if (!IsAddressFocused() && _boundWorker.Address != null)
            Url = _boundWorker.Address;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unsubscribe();
        _backButton = e.NameScope.Find<Button>("PART_BackButton");
        _forwardButton = e.NameScope.Find<Button>("PART_ForwardButton");
        _reloadButton = e.NameScope.Find<Button>("PART_ReloadButton");
        _homeButton = e.NameScope.Find<Button>("PART_HomeButton");
        _bookmarkButton = e.NameScope.Find<Button>("PART_BookmarkButton");
        _toolsButton = e.NameScope.Find<Button>("PART_ToolsButton");
        _settingsButton = e.NameScope.Find<Button>("PART_SettingsButton");
        _addressBar = e.NameScope.Find<TextBox>("PART_AddressBar");
        _suggestionsPopup = e.NameScope.Find<Popup>("PART_SuggestionsPopup");
        _suggestionsList = e.NameScope.Find<ItemsControl>("PART_SuggestionsList");
        _securityIcon = e.NameScope.Find<Border>("PART_SecurityIcon");
        _securityPath = e.NameScope.Find<Avalonia.Controls.Shapes.Path>("PART_SecurityPath");
        _securityText = e.NameScope.Find<TextBlock>("PART_SecurityText");

        if (_suggestionsList != null)
        {
            _suggestionsList.ItemsSource = _suggestions;
            _suggestionsList.AddHandler(InputElement.PointerPressedEvent, OnSuggestionsPointerPressed, handledEventsToo: false);
        }
        if (_backButton != null) { _backButton.Click += OnBackClick; }
        if (_forwardButton != null) { _forwardButton.Click += OnForwardClick; }
        if (_reloadButton != null) _reloadButton.Click += OnReloadClick;
        if (_homeButton != null) _homeButton.Click += OnHomeClick;
        if (_bookmarkButton != null) _bookmarkButton.Click += OnBookmarkClick;
        if (_toolsButton != null) _toolsButton.Click += OnToolsClick;
        if (_settingsButton != null) _settingsButton.Click += OnSettingsClick;
        if (_addressBar != null)
        {
            _addressBar.KeyDown += OnAddressBarKeyDown;
            _addressBar.TextChanged += OnAddressBarTextChanged;
        }
        UpdateButtonStates();
        SyncAddressText();
        RefreshFromBoundWorker();
    }

    private void Unsubscribe()
    {
        if (_backButton != null) { _backButton.Click -= OnBackClick; }
        if (_forwardButton != null) { _forwardButton.Click -= OnForwardClick; }
        if (_reloadButton != null) _reloadButton.Click -= OnReloadClick;
        if (_homeButton != null) _homeButton.Click -= OnHomeClick;
        if (_bookmarkButton != null) _bookmarkButton.Click -= OnBookmarkClick;
        if (_toolsButton != null) _toolsButton.Click -= OnToolsClick;
        if (_settingsButton != null) _settingsButton.Click -= OnSettingsClick;
        if (_addressBar != null)
        {
            _addressBar.KeyDown -= OnAddressBarKeyDown;
            _addressBar.TextChanged -= OnAddressBarTextChanged;
        }
        if (_suggestionsList != null)
            _suggestionsList.RemoveHandler(InputElement.PointerPressedEvent, OnSuggestionsPointerPressed);
    }

    private void OnReloadClick(object? s, RoutedEventArgs e) => ReloadRequested?.Invoke(this, EventArgs.Empty);
    private void OnHomeClick(object? s, RoutedEventArgs e) => HomeRequested?.Invoke(this, EventArgs.Empty);
    private void OnBookmarkClick(object? s, RoutedEventArgs e) => BookmarkRequested?.Invoke(this, EventArgs.Empty);
    private void OnToolsClick(object? s, RoutedEventArgs e) => ToolsRequested?.Invoke(this, EventArgs.Empty);
    private void OnSettingsClick(object? s, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnBackClick(object? s, RoutedEventArgs e)
    {
        if (!CanGoBack) return;
        BackRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnForwardClick(object? s, RoutedEventArgs e)
    {
        if (!CanGoForward) return;
        ForwardRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private async void OnAddressBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _addressBar == null) return;
        var raw = (_addressBar.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
        var url = await ResolveUserInputAsync(raw);
        if (url == null) return;
        NavigateRequested?.Invoke(this, url);
    }

    private void OnAddressBarTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingAddressText) return;
        _ = LoadSuggestionsDebouncedAsync();
        // NOTE: no auto-navigation here by design.
    }

    private async Task<string?> ResolveUserInputAsync(string raw)
    {
        if (InternalUrlHandler.IsInternalUrl(raw) || ChromiumInternalHandler.IsChromiumInternalUrl(raw))
            return raw;
        if (!raw.Contains("://"))
        {
            if (raw.Contains('.') && !raw.Contains(' '))
                raw = "https://" + raw;
            else
            {
                var tpl = await GetSearchEngineUrlAsync();
                return string.Format(tpl, Uri.EscapeDataString(raw));
            }
        }
        if (TryNormalizeUserUrl(raw, out var n)) return n;
        var tpl2 = await GetSearchEngineUrlAsync();
        return string.Format(tpl2, Uri.EscapeDataString(raw));
    }

    private async Task LoadSuggestionsDebouncedAsync()
    {
        if (_suggestionsService == null || _addressBar == null) return;
        _suggestionsCts?.Cancel();
        _suggestionsCts = new CancellationTokenSource();
        var token = _suggestionsCts.Token;
        try
        {
            await Task.Delay(250, token);
            var q = _addressBar.Text ?? "";
            if (token.IsCancellationRequested) return;
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2 || q.Contains("://") ||
                q.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase))
            {
                _suggestions.Clear();
                if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
                return;
            }
            var results = await _suggestionsService.GetSuggestionsAsync(q, 8);
            if (token.IsCancellationRequested) return;
            _suggestions.Clear();
            foreach (var s in results) _suggestions.Add(s);
            if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = _suggestions.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NavigationBar] suggestions: {ex.Message}"); }
    }

    private async void OnSuggestionsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (e.Source is Border b && b.DataContext is SearchSuggestion sug && _addressBar != null)
            {
                if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
                var url = await ResolveUserInputAsync(sug.Text);
                if (url != null) NavigateRequested?.Invoke(this, url);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NavigationBar] suggestion click: {ex.Message}"); }
    }

    private async Task<string> GetSearchEngineUrlAsync()
    {
        if (_settingsService != null)
        {
            try { return await _settingsService.GetSearchEngineUrlAsync(); } catch { }
        }
        return "https://www.google.com/search?q={0}";
    }

    public async Task<string> GetSearchHomePageAsync()
    {
        try
        {
            var tpl = await GetSearchEngineUrlAsync();
            var qi = tpl.IndexOf('?');
            var basePart = qi >= 0 ? tpl.Substring(0, qi) : tpl;
            if (Uri.TryCreate(basePart, UriKind.Absolute, out var uri))
                return $"{uri.Scheme}://{uri.Host}/";
        }
        catch { }
        return "https://www.google.com/";
    }

    private void UpdateButtonStates()
    {
        // PRO behaviour (Chrome/Edge): Back/Forward are truly disabled when
        // there is nowhere to go — dimmed + no hover/click. Reload/Home stay live.
        if (_backButton != null)
        {
            _backButton.IsEnabled = CanGoBack;
            _backButton.Opacity = CanGoBack ? 1.0 : 0.35;
        }
        if (_forwardButton != null)
        {
            _forwardButton.IsEnabled = CanGoForward;
            _forwardButton.Opacity = CanGoForward ? 1.0 : 0.35;
        }
    }

    private void SyncAddressText()
    {
        if (_addressBar == null || IsAddressFocused()) return;
        _syncingAddressText = true;
        try { _addressBar.Text = Url; } finally { _syncingAddressText = false; }
    }

    private async Task CheckUrlSecurityAsync(string url)
    {
        if (_securityCheckService == null) { SecurityStatus = SecurityStatus.Unknown; return; }
        _securityCheckCts?.Cancel();
        _securityCheckCts = new CancellationTokenSource();
        try
        {
            SecurityStatus = SecurityStatus.Checking;
            var r = await _securityCheckService.CheckUrlAsync(url);
            SecurityStatus = r.Status;
        }
        catch (OperationCanceledException) { }
        catch { SecurityStatus = SecurityStatus.Error; }
    }

    public Task CheckCurrentUrlSecurityAsync()
        => string.IsNullOrEmpty(Url) ? Task.CompletedTask : CheckUrlSecurityAsync(Url);

    private void UpdateSecurityIcon()
    {
        if (_securityPath == null) return;
        var (color, tip, text) = SecurityStatus switch
        {
            SecurityStatus.Safe => ("#4CAF50", "✓ Безпечний сайт", "Безпечно"),
            SecurityStatus.Dangerous => ("#F44336", "⚠️ НЕБЕЗПЕЧНИЙ САЙТ!", "НЕБЕЗПЕЧНО!"),
            SecurityStatus.Checking => ("#FFC107", "⏳ Перевірка...", "Перевірка…"),
            SecurityStatus.Error => ("#FF9800", "⚠ Помилка перевірки", "Помилка"),
            _ => ("#9E9E9E", "? Статус невідомий", "—"),
        };
        _securityPath.Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(color));
        if (_securityIcon != null) ToolTip.SetTip(_securityIcon, tip);
        if (_securityText != null) _securityText.Text = text;
    }

    public static bool TryNormalizeUserUrl(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var n = ValidateAndNormalizeUrl(input.Trim());
        if (n == null) return false;
        normalized = n;
        return true;
    }

    private static string? ValidateAndNormalizeUrl(string input)
    {
        if (input.StartsWith("chrome://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("view-source:", StringComparison.OrdinalIgnoreCase))
            return input.Trim();
        if (input.Equals("http://", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("https://", StringComparison.OrdinalIgnoreCase)) return null;
        string work = input;
        if (!work.Contains("://"))
        {
            if (work.Contains(' ') || !work.Contains('.')) return null;
            work = "https://" + work;
        }
        if (!Uri.TryCreate(work, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        if (string.IsNullOrWhiteSpace(uri.Host)) return null;
        return uri.ToString();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CanGoBackProperty || change.Property == CanGoForwardProperty)
            UpdateButtonStates();
        else if (change.Property == UrlProperty)
        {
            SyncAddressText();
            _ = CheckUrlSecurityAsync(Url);
        }
        else if (change.Property == SecurityStatusProperty)
            UpdateSecurityIcon();
    }
}
