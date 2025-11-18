using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Search.Services;

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
        if (sender is Border b && b.DataContext is SearchSuggestion sug && _searchInput != null)
        {
            _searchInput.Text = sug.Text;
            if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
            PerformSearch();
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

    private void SearchSettings_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: open settings window
    }

    private void PerformSearch(bool isLucky = false)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] PerformSearch called");
        
        if (_searchInput == null || string.IsNullOrWhiteSpace(_searchInput.Text))
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Search input is empty or null");
            return;
        }

        string query = _searchInput.Text.Trim();
        int selectedEngine = _searchEngineSelector?.SelectedIndex ?? 0;
        
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Query: '{query}', Engine: {selectedEngine}");

        if (selectedEngine == 0) // Vetale Search (локальний)
        {
            var resultsUrl = $"vetale://search/results?q={Uri.EscapeDataString(query)}";
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Generated URL: {resultsUrl}");
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] NavigateRequested subscribers: {NavigateRequested?.GetInvocationList().Length ?? 0}");
            
            NavigateRequested?.Invoke(this, resultsUrl);
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] NavigateRequested invoked");
            return;
        }

        string[] searchUrls = new[]
        {
            "",
            $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
            $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}",
            $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}",
            $"https://yandex.com/search/?text={Uri.EscapeDataString(query)}"
        };
        
        if (selectedEngine < searchUrls.Length && !string.IsNullOrEmpty(searchUrls[selectedEngine]))
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Web search URL: {searchUrls[selectedEngine]}");
            NavigateRequested?.Invoke(this, searchUrls[selectedEngine]);
        }
    }

    public void SetQuery(string query)
    {
        if (_searchInput != null)
        {
            _searchInput.Text = query;
            // оновити стан кнопки пошуку
            if (_searchButton != null)
            {
                _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput.Text);
            }
        }
    }
}
