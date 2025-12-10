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
    private bool _isLoading;
    private int _currentPage = 0;
    private bool _hasNextPage = true;
    private string _query = string.Empty;
    private CancellationTokenSource? _loadCts;

    public event EventHandler<string>? SourcePageOpenRequested;

    public IImageSearchService ImageSearchService { get; set; } = ImageSearchServiceFactory.Create(); // Виклик фабрики

    // КОМЕНТАР:
    // Якщо потрібно тимчасово протестувати з конкретним ключем без env:
    // ImageSearchService = new UnifiedImageSearchService("PEXELS_KEY", "UNSPLASH_KEY");
    // або тільки один:
    // ImageSearchService = new UnifiedImageSearchService("PEXELS_KEY", null);

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
        }

        _scrollViewer = this.FindControl<ScrollViewer>("PART_ScrollViewer");
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
        }
    }

    public void SetQuery(string query)
    {
        _query = query;
        _items.Clear();
        _currentPage = 0;
        _hasNextPage = true;
        _ = LoadNextPageAsync();
    }

    private async Task LoadNextPageAsync()
    {
        if (_isLoading || !_hasNextPage || string.IsNullOrWhiteSpace(_query))
            return;

        _isLoading = true;
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        try
        {
            var nextPage = _currentPage + 1;
            var page = await ImageSearchService.SearchAsync(_query, nextPage, 50, null, ct);
            if (ct.IsCancellationRequested)
                return;

            _currentPage = page.PageNumber;
            _hasNextPage = page.HasNextPage;

            foreach (var r in page.Results)
            {
                _items.Add(r);
            }
        }
        finally
        {
            _isLoading = false;
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

    private void ImageCard_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ImageSearchResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.SourcePageUrl))
            {
                SourcePageOpenRequested?.Invoke(this, result.SourcePageUrl);
            }
        }
    }
}
