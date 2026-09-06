using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.UI.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public class HistoryItemViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public HistoryItem Item { get; set; }
    private Avalonia.Media.IImage? _faviconImage;
    public Avalonia.Media.IImage? FaviconImage
    {
        get => _faviconImage;
        set
        {
            if (!ReferenceEquals(_faviconImage, value))
            {
                _faviconImage = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FaviconImage)));
            }
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    
    public int Id => Item.Id;
    public string Url => Item.Url;
    public string Title => Item.Title;
    public DateTime VisitedAt => Item.VisitedAt;
    public int VisitCount => Item.VisitCount;

    public HistoryItemViewModel(HistoryItem item)
    {
        Item = item;
    }
}

public partial class HistoryPage : UserControl
{
    private ItemsControl? _historyItemsControl;
    private TextBox? _searchBox;
    private StackPanel? _filterPanel;
    private Border? _emptyState;
    private IHistoryDatabaseService? _historyService;
    private readonly IFaviconService _faviconService;
    private string _currentFilter = "All";

    public HistoryPage()
    {
        InitializeComponent();
        _faviconService = new FaviconService();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _historyItemsControl = this.FindControl<ItemsControl>("PART_HistoryItemsControl");
        _searchBox = this.FindControl<TextBox>("PART_SearchBox");
        _filterPanel = this.FindControl<StackPanel>("PART_FilterPanel");
        _emptyState = this.FindControl<Border>("PART_EmptyState");
        
        // Set the first filter as active
        UpdateFilterButtonStyles();
        LoadHistory();
    }

    public void SetHistoryService(IHistoryDatabaseService historyService)
    {
        _historyService = historyService;
        LoadHistory();
    }

    private async void LoadHistory()
    {
        if (_historyItemsControl == null || _historyService == null)
        {
            System.Diagnostics.Debug.WriteLine("[HistoryPage] Cannot load history - controls or service not ready");
            return;
        }

        try
        {
            List<HistoryItem> historyItems;
            
            // Get history by filter
            if (!string.IsNullOrWhiteSpace(_searchBox?.Text))
            {
                historyItems = _historyService.SearchHistory(_searchBox.Text);
            }
            else
            {
                var (startDate, endDate) = GetDateRangeForFilter(_currentFilter);
                historyItems = _historyService.GetHistory(startDate, endDate);
            }

            System.Diagnostics.Debug.WriteLine($"[HistoryPage] Loaded {historyItems.Count} history items");

            // Check for empty result
            if (historyItems.Count == 0)
            {
                _historyItemsControl.ItemsSource = new List<HistoryItemViewModel>();
                if (_emptyState != null) _emptyState.IsVisible = true;
                return;
            }

            // Hide empty state when data is present
            if (_emptyState != null) _emptyState.IsVisible = false;

            // Show items immediately, load favicons progressively in background
            var viewModels = new List<HistoryItemViewModel>();
            foreach (var item in historyItems)
            {
                viewModels.Add(new HistoryItemViewModel(item));
            }

            _historyItemsControl.ItemsSource = viewModels;
            _ = LoadFaviconsAsync(viewModels);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryPage] Error loading history: {ex.Message}");
            Console.WriteLine($"Error loading history: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads favicons in background without blocking the list display
    /// </summary>
    private async System.Threading.Tasks.Task LoadFaviconsAsync(List<HistoryItemViewModel> viewModels)
    {
        foreach (var vm in viewModels)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(vm.Url) && Uri.TryCreate(vm.Url, UriKind.Absolute, out var uri))
                {
                    var favicon = await _faviconService.GetFaviconAsync(uri, 20);
                    if (favicon != null)
                    {
                        var captured = vm;
                        var capturedIcon = favicon;
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => captured.FaviconImage = capturedIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HistoryPage] Error loading favicon for {vm.Url}: {ex.Message}");
            }
        }
    }

    private async System.Threading.Tasks.Task LoadFaviconsForHistoryAsync(List<HistoryItem> items)
    {
        foreach (var item in items)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(item.Url))
                {
                    var uri = new Uri(item.Url);
                    var favicon = await _faviconService.GetFaviconAsync(uri, 18);
                    if (favicon != null)
                    {
                        // Store favicon in FaviconData for later display
                        // But it is better to use binding to Source
                        // For now just log it
                        System.Diagnostics.Debug.WriteLine($"[HistoryPage] Loaded favicon for {item.Url}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HistoryPage] Error loading favicon for {item.Url}: {ex.Message}");
            }
        }
    }

    private (DateTime? startDate, DateTime? endDate) GetDateRangeForFilter(string filter)
    {
        var now = DateTime.UtcNow;
        
        return filter switch
        {
            "1Day" => (now.AddDays(-1), now),
            "7Days" => (now.AddDays(-7), now),
            "1Month" => (now.AddMonths(-1), now),
            "6Months" => (now.AddMonths(-6), now),
            "1Year" => (now.AddYears(-1), now),
            "OlderThanYear" => (null, now.AddYears(-1)),
            _ => (null, null) // "All"
        };
    }

    private void FilterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filter)
        {
            _currentFilter = filter;
            UpdateFilterButtonStyles();
            LoadHistory();
        }
    }

    private void UpdateFilterButtonStyles()
    {
        if (_filterPanel == null)
            return;

        foreach (var child in _filterPanel.Children)
        {
            if (child is Button btn)
            {
                var isActive = btn.Tag?.ToString() == _currentFilter;
                // Use modern style like Chrome
                btn.Background = isActive 
                    ? Brush.Parse("#1A73E8")  // Google Blue
                    : Brush.Parse("#F0F0F0");
                btn.Foreground = isActive 
                    ? Brushes.White 
                    : Brush.Parse("#333333");
            }
        }
    }

    private void SearchBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        LoadHistory();
    }

    private void OpenHistory_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is HistoryItemViewModel viewModel)
        {
            try
            {
                var historyItem = viewModel.Item;
                
                // Find the main window and navigate to URL
                var appLifetime = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                if (appLifetime != null)
                {
                    foreach (var window in appLifetime.Windows)
                    {
                        if (window is MainWindow mainWindow)
                        {
                            mainWindow.NavigateUrlInActiveTab(historyItem.Url);
                            mainWindow.Activate();
                            GetParentWindow()?.Close();
                            
                            System.Diagnostics.Debug.WriteLine($"[HistoryPage] Navigating to: {historyItem.Url}");
                            return;
                        }
                    }
                }
                
                Console.WriteLine($"[HistoryPage] Could not find MainWindow to navigate to: {historyItem.Url}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HistoryPage] Error opening history: {ex.Message}");
            }
        }
    }

    private void DeleteHistory_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is HistoryItemViewModel viewModel && _historyService != null)
        {
            try
            {
                _historyService.DeleteHistoryItem(viewModel.Item.Id);
                LoadHistory();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting history item: {ex.Message}");
            }
        }
    }

    private void ClearAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_historyService == null)
            return;

        try
        {
            // Show confirmation dialog (simplified version)
            _historyService.ClearHistory();
            LoadHistory();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing history: {ex.Message}");
        }
    }

    private Window GetParentWindow()
    {
        var parent = this.Parent;
        while (parent != null)
        {
            if (parent is Window window)
                return window;
            parent = parent.Parent;
        }
        throw new InvalidOperationException("Could not find parent window");
    }
}

