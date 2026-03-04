using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Search.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Controls;

public partial class ImageSearchResultsView : UserControl
{
    private readonly ObservableCollection<ImageSearchResult> _items = new();
    private ItemsControl? _itemsHost;
    private ScrollViewer? _scrollViewer;
    private Border? _loadingIndicator;
    private Border? _noResultsMessage;
    private bool _isLoading;
    private int _currentPage;
    private bool _hasNextPage = true;
    private string _query = string.Empty;
    private CancellationTokenSource? _loadCts;

    public event EventHandler<string>? SourcePageOpenRequested;

    public IImageSearchService ImageSearchService { get; set; } = ImageSearchServiceFactory.Create();

    public ImageSearchResultsView()
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
        _itemsHost = this.FindControl<ItemsControl>("ItemsHost");
        if (_itemsHost != null)
        {
            _itemsHost.ItemsSource = _items;
            
            // Connect click handler for containers
            _itemsHost.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, OnItemPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        _scrollViewer = this.FindControl<ScrollViewer>("PART_ScrollViewer");
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
        }
        
        _loadingIndicator = this.FindControl<Border>("LoadingIndicator");
        _noResultsMessage = this.FindControl<Border>("NoResultsMessage");
    }
    
    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Find ImageSearchResult from DataContext
        if (e.Source is Control control)
        {
            var current = control;
            while (current != null)
            {
                if (current.DataContext is ImageSearchResult result)
                {
                    if (!string.IsNullOrWhiteSpace(result.SourcePageUrl))
                    {
                        SourcePageOpenRequested?.Invoke(this, result.SourcePageUrl);
                        e.Handled = true;
                    }
                    return;
                }
                current = current.Parent as Control;
            }
        }
    }

    public void SetQuery(string query)
    {
        _query = query;
        _items.Clear();
        _currentPage = 0;
        _hasNextPage = true;
        
        // Hide the "no results" message
        if (_noResultsMessage != null) _noResultsMessage.IsVisible = false;
        
        _ = LoadNextPageAsync();
    }

    private async Task LoadNextPageAsync()
    {
        if (_isLoading || !_hasNextPage || string.IsNullOrWhiteSpace(_query))
            return;

        _isLoading = true;
        if (_loadingIndicator != null) _loadingIndicator.IsVisible = true;
        
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        try
        {
            var nextPage = _currentPage + 1;
            System.Diagnostics.Debug.WriteLine($"[ImageSearch] Loading page {nextPage} for query: {_query}");
            
            // Maximum 50 results per page
            var page = await ImageSearchService.SearchAsync(_query, nextPage, ImageSearchServiceFactory.MaxResultsPerPage, null, ct);
            if (ct.IsCancellationRequested)
                return;

            _currentPage = page.PageNumber;
            _hasNextPage = page.HasNextPage;

            System.Diagnostics.Debug.WriteLine($"[ImageSearch] Got {page.Results.Count} results, hasNext: {_hasNextPage}");

            foreach (var r in page.Results)
            {
                _items.Add(r);
            }
            
            // Show message if there are no results
            if (_items.Count == 0 && _noResultsMessage != null)
            {
                _noResultsMessage.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageSearch] Error: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
            if (_loadingIndicator != null) _loadingIndicator.IsVisible = false;
        }
    }

    private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer == null)
            return;

        var offset = _scrollViewer.Offset.Y;
        var extent = _scrollViewer.Extent.Height;
        var viewport = _scrollViewer.Viewport.Height;

        if (extent - (offset + viewport) < 200)
        {
            _ = LoadNextPageAsync();
        }
    }
}
