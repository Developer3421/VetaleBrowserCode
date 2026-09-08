using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Search.Services;
using Avalonia;
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.UI.Theme;
using VetaleBrowser.VetaleBrowser.UI.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VetaleSearchHomePage : UserControl
{
    public event EventHandler<string>? NavigateRequested;

    private TextBox? _searchInput;
    private ComboBox? _searchEngineSelector;
    private Button? _searchButton;
    private Popup? _suggestionsPopup;
    private ItemsControl? _suggestionsList;
    private readonly System.Collections.ObjectModel.ObservableCollection<SearchSuggestion> _suggestions = new();
    private ISuggestionsService? _suggestionsService;
    private System.Threading.CancellationTokenSource? _suggestionsCts;

    public VetaleSearchHomePage()
    {
        InitializeComponent();
        InitializeControls();

        ApplyTheme();
        VetaleSearchThemeManager.ThemeChanged += OnThemeChanged;
        Unloaded += OnUnloaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void SetSuggestionsService(ISuggestionsService service)
    {
        _suggestionsService = service;
    }

    private void InitializeControls()
    {
        _searchInput = this.FindControl<TextBox>("SearchInput");
        _searchEngineSelector = this.FindControl<ComboBox>("SearchEngineSelector");
        _searchButton = this.FindControl<Button>("SearchButton");
        _suggestionsPopup = this.FindControl<Popup>("SuggestionsPopup");
        _suggestionsList = this.FindControl<ItemsControl>("SuggestionsList");
        if (_suggestionsList != null)
        {
            _suggestionsList.ItemsSource = _suggestions;
            _suggestionsList.AddHandler(InputElement.PointerPressedEvent, OnSuggestionPointerPressed, handledEventsToo: false);
        }

        // Focus search input when page loads
        if (_searchInput != null)
        {
            _searchInput.Focus();
        }

        if (_searchButton != null)
        {
            // Defensive: rebind handler to prevent duplicates + survive visual tree/style reload edge cases
            _searchButton.Click -= Search_Click;
            _searchButton.Click += Search_Click;
            _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput?.Text);
        }
    }

    private void SearchInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_searchButton != null)
        {
            _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput?.Text);
        }
        _ = LoadSuggestionsAsync();
    }

    private async System.Threading.Tasks.Task LoadSuggestionsAsync()
    {
        if (_suggestionsService == null || _searchInput == null)
        {
            if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
            return;
        }
        _suggestionsCts?.Cancel();
        _suggestionsCts = new System.Threading.CancellationTokenSource();
        var token = _suggestionsCts.Token;
        try
        {
            await System.Threading.Tasks.Task.Delay(300, token); // debounce
            var query = _searchInput.Text ?? string.Empty;
            if (token.IsCancellationRequested) return;
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
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
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Suggestions error: {ex.Message}");
            _suggestions.Clear();
            if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
        }
    }

    private void OnSuggestionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            // Find the element that was clicked
            if (e.Source is Control clickedControl)
            {
                // Search for SearchSuggestion in DataContext of current or parent elements
                var current = clickedControl;
                SearchSuggestion? suggestion = null;
                
                while (current != null)
                {
                    if (current.DataContext is SearchSuggestion sug)
                    {
                        suggestion = sug;
                        break;
                    }
                    current = current.Parent as Control;
                }
                
                if (suggestion != null && _searchInput != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Suggestion clicked: {suggestion.Text}");
                    
                    // Insert suggestion text into the search field
                    _searchInput.Text = suggestion.Text;
                    
                    // Place cursor at the end of the text
                    _searchInput.CaretIndex = suggestion.Text.Length;
                    
                    // Close the popup
                    if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
                    
                    // Focus the search field
                    _searchInput.Focus();
                    
                    // Do NOT perform search automatically - allow user to edit
                    // If automatic search is needed, uncomment:
                    // PerformSearch();
                    
                    e.Handled = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] OnSuggestionPointerPressed error: {ex.Message}");
        }
    }

    private void SearchInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
        }
    }

    private void Search_Click(object? sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private void LuckySearch_Click(object? sender, RoutedEventArgs e)
    {
        // "I'm feeling lucky" functionality
        PerformSearch(isLucky: true);
    }

    private void SearchEngineSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Handle search engine change
        // You can store the selected engine for later use
    }

    private async void SearchSettings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                var settingsWindow = new Windows.VetaleSearchSettingsWindow();
                await settingsWindow.ShowDialog(parentWindow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] SearchSettings_Click ERROR: {ex.Message}");
        }
    }

    private async void Bookmarks_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                var bookmarksWindow = new Windows.BookmarksWindow();
                await bookmarksWindow.ShowDialog(parentWindow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Bookmarks_Click ERROR: {ex.Message}");
        }
    }

    private async void History_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                var historyWindow = new Windows.HistoryWindow();
                await historyWindow.ShowDialog(parentWindow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] History_Click ERROR: {ex.Message}");
        }
    }

    private void GeoSearch_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var raw = _searchInput?.Text ?? string.Empty;
            var query = raw.Trim();
            var url = string.IsNullOrWhiteSpace(query)
                ? "https://www.openstreetmap.org/"
                : $"https://www.openstreetmap.org/search?query={Uri.EscapeDataString(query)}";

            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] GeoSearch_Click: query='{query}', url='{url}'");
            NavigateRequested?.Invoke(this, url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] GeoSearch_Click ERROR: {ex.Message}");
        }
    }

    private void PerformSearch(bool isLucky = false)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] PerformSearch called (isLucky={isLucky})");
        
        if (_searchInput == null)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] ✗ Search input is NULL");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Search input text: '{_searchInput.Text}'");
        
        if (string.IsNullOrWhiteSpace(_searchInput.Text))
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] ✗ Search input is empty or whitespace");
            return;
        }

        string query = _searchInput.Text.Trim();
        int selectedEngine = _searchEngineSelector?.SelectedIndex ?? 0;
        
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Query: '{query}', Engine: {selectedEngine}");

        if (selectedEngine == 0) // Vetale Search (local)
        {
            var resultsUrl = $"vetale://search/results?q={Uri.EscapeDataString(query)}";

            // Primary path: event (wired by MainWindow/InternalUrlHandler in most cases)
            if (NavigateRequested != null)
            {
                NavigateRequested.Invoke(this, resultsUrl);
                return;
            }

            // Fallback: drive navigation through the global callback (set by MainWindow).
            var cb = InternalUrlHandler.NavigationRequestCallback;
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] NavigateRequested is NULL; NavigationRequestCallback={(cb != null ? "OK" : "NULL")}, url={resultsUrl}");

            if (cb != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try { cb(resultsUrl); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] NavigationRequestCallback invoke failed: {ex.Message}"); }
                });
            }
            return;
        }

        // Пункти dropdown (VetaleSearchHomePage.axaml): 0=Local, 1=Google, 2=Bing, 3=DuckDuckGo, 4=Maps(OSM).
        // Явний switch замість масиву за індексом — минулий масив мав зайвий Yandex і останній пункт вів не туди.
        var encoded = Uri.EscapeDataString(query);
        string? webUrl = selectedEngine switch
        {
            1 => $"https://www.google.com/search?q={encoded}",
            2 => $"https://www.bing.com/search?q={encoded}",
            3 => $"https://duckduckgo.com/?q={encoded}",
            4 => $"https://www.openstreetmap.org/search?query={encoded}",
            _ => null,
        };

        if (!string.IsNullOrEmpty(webUrl))
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Web search URL: {webUrl}");
            NavigateRequested?.Invoke(this, webUrl);
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] ✓ NavigateRequested invoked for web search");
        }
    }

    public void SetQuery(string query)
    {
        if (_searchInput != null)
        {
            _searchInput.Text = query;
            // update search button state
            if (_searchButton != null)
            {
                _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput.Text);
            }
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // Only re-apply theme. Do not touch navigation delegates here.
        ApplyTheme();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        VetaleSearchThemeManager.ThemeChanged -= OnThemeChanged;
        Unloaded -= OnUnloaded;
    }

    private async void ApplyTheme()
    {
        try
        {
            if (Application.Current != null)
                await VetaleSearchThemeManager.ApplyToResourceHostAsync(Application.Current);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] ApplyTheme error: {ex.Message}");
        }
    }
}
