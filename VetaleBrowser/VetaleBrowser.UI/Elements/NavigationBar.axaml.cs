using System;
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
    private WebViewManager? _webViewManager;
    private TabWorker? _tabWorker;
    private ISettingsService? _settingsService;
    private Popup? _suggestionsPopup;
    private ItemsControl? _suggestionsList;
    private readonly System.Collections.ObjectModel.ObservableCollection<SearchSuggestion> _suggestions = new();
    private ISuggestionsService? _suggestionsService;
    private System.Threading.CancellationTokenSource? _suggestionsCts;
    private ISecurityCheckService? _securityCheckService;
    private System.Threading.CancellationTokenSource? _securityCheckCts;
    private Border? _securityIcon;
    private Avalonia.Controls.Shapes.Path? _securityPath;
    private TextBlock? _securityText;
    private System.Threading.CancellationTokenSource? _autoNavigateCts; // ��+�� debounce ������������������
    private string? _lastAutoNavigatedUrl; // ����������� URL ���� ��� ������������� �+���������
    private bool _suppressTextChanged; // ��+��������� �+����������+����� ������������������ �+��� �+��������+���� ���+�����

    // ������� ������������� ��+�� vetale://
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
    
    public SecurityStatus SecurityStatus
    {
        get => GetValue(SecurityStatusProperty);
        set => SetValue(SecurityStatusProperty, value);
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
    /// Set TabWorker for navigation history support
    /// </summary>
    public void SetTabWorker(TabWorker? tabWorker)
    {
        _tabWorker = tabWorker;
        System.Diagnostics.Trace.WriteLine("NavigationBar: TabWorker set");
    }

    /// <summary>
    /// Set settings service for search engine configuration
    /// </summary>
    public void SetSettingsService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        System.Diagnostics.Trace.WriteLine("NavigationBar: Settings service set");
    }

    public void SetSuggestionsService(ISuggestionsService service)
    {
        _suggestionsService = service;
    }
    
    public void SetSecurityCheckService(ISecurityCheckService service)
    {
        _securityCheckService = service;
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
        {
            _addressBar.KeyDown -= OnAddressBarKeyDown;
            _addressBar.TextChanged -= OnAddressBarTextChanged;
        }

        // Get new buttons/controls
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
        {
            _addressBar.KeyDown += OnAddressBarKeyDown;
            _addressBar.TextChanged += OnAddressBarTextChanged;
        }

        UpdateButtonStates();
    }

    private void OnBackButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_tabWorker != null)
        {
            _tabWorker.GoBack();
            System.Diagnostics.Trace.WriteLine("NavigationBar: Back button clicked (TabWorker)");
        }
        else
        {
            _webViewManager?.GoBack();
            System.Diagnostics.Trace.WriteLine("NavigationBar: Back button clicked (WebViewManager fallback)");
        }
    }

    private void OnForwardButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_tabWorker != null)
        {
            _tabWorker.GoForward();
            System.Diagnostics.Trace.WriteLine("NavigationBar: Forward button clicked (TabWorker)");
        }
        else
        {
            _webViewManager?.GoForward();
            System.Diagnostics.Trace.WriteLine("NavigationBar: Forward button clicked (WebViewManager fallback)");
        }
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

            // �������������� ������+���� �+������� ���������������
            await CheckUrlSecurityAsync(url);

            // ����� �� TabWorker, ������������������+� ���� Navigate (��� ���+������� PreCheck)
            if (_tabWorker != null)
            {
                _tabWorker.Navigate(url);
                System.Diagnostics.Trace.WriteLine($"NavigationBar: Navigate via TabWorker to {url}");
            }
            else
            {
                // Fallback �� �+�����+��� �������������
                await _webViewManager.NavigateAsync(url);
                System.Diagnostics.Trace.WriteLine($"NavigationBar: Navigate to {url}");
            }
        }
    }

    private void OnAddressBarTextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        if (_suppressTextChanged) return; // ����+���������� ��������������� ���+� �+� ����+�� �����+�����+� Url
        _ = LoadAddressSuggestionsAsync();
        _ = AutoNavigateDebouncedAsync();
    }

    private async System.Threading.Tasks.Task LoadAddressSuggestionsAsync()
    {
        if (_suggestionsService == null || _addressBar == null)
        {
            if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
            return;
        }

        _suggestionsCts?.Cancel();
        _suggestionsCts = new System.Threading.CancellationTokenSource();
        var token = _suggestionsCts.Token;
        try
        {
            await System.Threading.Tasks.Task.Delay(250, token); // debounce
            var query = _addressBar.Text ?? string.Empty;
            if (token.IsCancellationRequested) return;

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                _suggestions.Clear();
                if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
                return;
            }
            // ����� ��������� �� �+����� URL ��� ���������������� vetale:// - ��� �+����������+� �+���������
            if (query.Contains("://") || query.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase))
            {
                _suggestions.Clear();
                if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
                return;
            }

            var results = await _suggestionsService.GetSuggestionsAsync(query, 8);
            if (token.IsCancellationRequested) return;

            _suggestions.Clear();
            foreach (var s in results) _suggestions.Add(s);

            if (_suggestionsPopup != null)
                _suggestionsPopup.IsOpen = _suggestions.Count > 0;
        }
        catch (System.OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationBar] Suggestions error: {ex.Message}");
            _suggestions.Clear();
            if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
        }
    }

    private async void OnSuggestionsPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        try
        {
            // ��������+� Border ���� DataContext SearchSuggestion
            if (e.Source is Border b && b.DataContext is SearchSuggestion sug && _addressBar != null)
            {
                _addressBar.Text = sug.Text;
                if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
                if (_webViewManager != null)
                {
                    var url = sug.Text;
                    if (!url.Contains("://"))
                    {
                        var searchTemplate = await GetSearchEngineUrlAsync();
                        url = string.Format(searchTemplate, Uri.EscapeDataString(url));
                    }
                    if (InternalUrlHandler.IsInternalUrl(url))
                        NavigateRequested?.Invoke(this, url);
                    else
                        await _webViewManager.NavigateAsync(url);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationBar] Suggestions click error: {ex.Message}");
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
    
    /// <summary>
    /// ���������������� ������+����� URL
    /// </summary>
    private async System.Threading.Tasks.Task CheckUrlSecurityAsync(string url)
    {
        if (_securityCheckService == null)
        {
            SecurityStatus = SecurityStatus.Unknown;
            return;
        }
        
        // ������������� �+��+���������� �+��������������
        _securityCheckCts?.Cancel();
        _securityCheckCts = new System.Threading.CancellationTokenSource();
        
        try
        {
            // �������������� ����������� "��������������"
            SecurityStatus = SecurityStatus.Checking;
            
            // ���������� �+��������������
            var result = await _securityCheckService.CheckUrlAsync(url);
            
            // ��������� �����������
            SecurityStatus = result.Status;
            
            // ����� ������ ���������+������� - �+��������� �+��+���������������
            if (result.Status == SecurityStatus.Dangerous)
            {
                System.Diagnostics.Debug.WriteLine($"[Security] ��ᴩ� ����������! ������������� ������: {url}");
                System.Diagnostics.Debug.WriteLine($"[Security] {result.Description}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Security] ԣ� �������+�������: {url} - {result.Description}");
            }
        }
        catch (System.OperationCanceledException)
        {
            // ��������������� �����������
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Security] ���+��+�� �+�������������: {ex.Message}");
            SecurityStatus = SecurityStatus.Error;
        }
    }
    
    /// <summary>
    /// �����+������� �+������ ��+�� �+������������� �+���������� URL (����+������������� ���������)
    /// </summary>
    public async System.Threading.Tasks.Task CheckCurrentUrlSecurityAsync()
    {
        if (!string.IsNullOrEmpty(Url))
        {
            await CheckUrlSecurityAsync(Url);
        }
    }
    
    /// <summary>
    /// ��������� �������� ������+����
    /// </summary>
    private void UpdateSecurityIcon()
    {
        if (_securityPath == null) return;

        switch (SecurityStatus)
        {
            case SecurityStatus.Safe:
                // �����+����� ����� - ������+������
                _securityPath.Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50"));
                _securityPath.Data = Avalonia.Media.Geometry.Parse("M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z");
                if (_securityIcon != null) ToolTip.SetTip(_securityIcon, GetLocalizedString("Security.Safe.Tooltip", "ԣ� �������+������� ������"));
                if (_securityText != null) _securityText.Text = GetLocalizedString("Security.Safe.Text", "����������������: ������+������");
                break;

            case SecurityStatus.Dangerous:
                // ���������� ����� �� ���+���� - ���������+������
                _securityPath.Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F44336"));
                _securityPath.Data = Avalonia.Media.Geometry.Parse("M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z M11 7h2v6h-2V7z M11 15h2v2h-2v-2z");
                if (_securityIcon != null) ToolTip.SetTip(_securityIcon, GetLocalizedString("Security.Dangerous.Tooltip", "��ᴩ� �������������������� �������� (���������)!"));
                if (_securityText != null) _securityText.Text = GetLocalizedString("Security.Dangerous.Text", "������������������!");
                break;

            case SecurityStatus.Checking:
                // �������� ����� - �+�������������
                _securityPath.Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFC107"));
                _securityPath.Data = Avalonia.Media.Geometry.Parse("M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z");
                if (_securityIcon != null) ToolTip.SetTip(_securityIcon, GetLocalizedString("Security.Checking.Tooltip", "�Ŧ �������������� ������+����..."));
                if (_securityText != null) _securityText.Text = GetLocalizedString("Security.Checking.Text", "���������������Ǫ");
                break;

            case SecurityStatus.Error:
                // ���+������������ ����� - �+��+��+��
                _securityPath.Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FF9800"));
                _securityPath.Data = Avalonia.Media.Geometry.Parse("M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z");
                if (_securityIcon != null) ToolTip.SetTip(_securityIcon, GetLocalizedString("Security.Error.Tooltip", "��� ���+��+�� �+�������������"));
                if (_securityText != null) _securityText.Text = GetLocalizedString("Security.Error.Text", "���+��+�� �+�������������");
                break;

            case SecurityStatus.Unknown:
            default:
                // �������� ����� - ���������+�
                _securityPath.Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#9E9E9E"));
                _securityPath.Data = Avalonia.Media.Geometry.Parse("M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z");
                if (_securityIcon != null) ToolTip.SetTip(_securityIcon, GetLocalizedString("Security.Unknown.Tooltip", "? ����������� ���������+��"));
                if (_securityText != null) _securityText.Text = GetLocalizedString("Security.Unknown.Text", "����������� ���������+��");
                break;
        }
    }
    
    /// <summary>
    /// ��������+���� �+����+���������� �������
    /// </summary>
    private static string GetLocalizedString(string key, string defaultValue)
    {
        try
        {
            if (Application.Current?.TryFindResource(key, out var resource) == true && resource is string str)
                return str;
        }
        catch { }
        return defaultValue;
    }

    private async System.Threading.Tasks.Task AutoNavigateDebouncedAsync()
    {
        if (_addressBar == null || _webViewManager == null) return;
        _autoNavigateCts?.Cancel();
        _autoNavigateCts = new System.Threading.CancellationTokenSource();
        var token = _autoNavigateCts.Token;
        try
        {
            await System.Threading.Tasks.Task.Delay(500, token); // debounce
            if (token.IsCancellationRequested) return;
            var raw = _addressBar.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw)) return;

            // �������������� �� ���������������� vetale://
            if (InternalUrlHandler.IsInternalUrl(raw))
            {
                if (!string.Equals(_lastAutoNavigatedUrl, raw, StringComparison.Ordinal))
                {
                    _lastAutoNavigatedUrl = raw;
                    System.Diagnostics.Debug.WriteLine($"[NavigationBar] Auto internal navigate: {raw}");
                    NavigateRequested?.Invoke(this, raw);
                }
                return;
            }

            var normalized = ValidateAndNormalizeUrl(raw);
            if (normalized == null) return; // ��� ���+������ URL, �������� ��� ������+�
            if (string.Equals(_lastAutoNavigatedUrl, normalized, StringComparison.Ordinal)) return; // ����� ����+
            _lastAutoNavigatedUrl = normalized;

            System.Diagnostics.Debug.WriteLine($"[NavigationBar] Auto navigating to: {normalized}");
            await CheckUrlSecurityAsync(normalized);
            await _webViewManager.NavigateAsync(normalized);

            // ��������� Url ��+��������������� ����� �+����������� ����� ����+�������
            _suppressTextChanged = true;
            try
            {
                Url = normalized;
            }
            finally
            {
                _suppressTextChanged = false;
            }
        }
        catch (System.OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationBar] AutoNavigate error: {ex.Message}");
        }
    }

    /// <summary>
    /// �����+������� �����+�+����: ���+����������� �����+��+����������� �������������������� ����� �� ���+������� http(s) URL.
    /// ������������ true, ������ ���� ��������� �� �+�����+�� URL (���+��� ����� �+�������+���), �� �+����������� �����+��+���������� URL.
    /// </summary>
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
        // ������������ ���������� ����+����� �+������������
        if (input.Equals("http://", StringComparison.OrdinalIgnoreCase) || input.Equals("https://", StringComparison.OrdinalIgnoreCase))
            return null;

        string work = input;
        // �������� https ������ ����+��� �������+� ��+�� ����+������ ��� ���+���
        if (!work.Contains("://"))
        {
            // ����� �+����������� �+�������+� ��� ���� ��� �+�����+�� URL
            if (work.Contains(' ')) return null;
            // �������� �+���������� ������ � ����� �����+��� (domain.tld)
            if (work.Contains('.'))
            {
                work = "https://" + work;
            }
            else
            {
                return null; // ���� �+���������� ����+���, ��� URL
            }
        }
        // �������������� ���+�����������
        if (!Uri.TryCreate(work, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        // ������� ������ ����� ��������� ���+'�� ��������
        if (string.IsNullOrWhiteSpace(uri.Host)) return null;
        return uri.ToString();
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
            // �+��� ���+����� Url �����+�����+� ��������� ����� ��+�� ��� ������������+� ������������������
            _suppressTextChanged = true;
            try { _addressBar.Text = Url; } finally { _suppressTextChanged = false; }
            _ = CheckUrlSecurityAsync(Url);
        }
        else if (change.Property == SecurityStatusProperty)
        {
            UpdateSecurityIcon();
        }
    }
}

