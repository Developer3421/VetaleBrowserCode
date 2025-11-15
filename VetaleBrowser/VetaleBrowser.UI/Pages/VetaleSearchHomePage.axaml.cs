using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VetaleSearchHomePage : UserControl
{
    public event EventHandler<string>? NavigateRequested;

    private TextBox? _searchInput;
    private ComboBox? _searchEngineSelector;
    private Button? _searchButton;
    
    public VetaleSearchHomePage()
    {
        InitializeComponent();
        InitializeControls();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeControls()
    {
        _searchInput = this.FindControl<TextBox>("SearchInput");
        _searchEngineSelector = this.FindControl<ComboBox>("SearchEngineSelector");
        _searchButton = this.FindControl<Button>("SearchButton");

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
}
