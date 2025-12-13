using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Search.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Controls;

public partial class VideoSearchResultsView : UserControl
{
    private readonly ObservableCollection<VideoSearchResult> _items = new();
    private ItemsControl? _itemsHost;
    private ScrollViewer? _scrollViewer;
    private Border? _loadingIndicator;
    private Border? _noResultsMessage;
    private bool _isLoading;
    private int _currentPage;
    private bool _hasNextPage = true;
    private string _query = string.Empty;
    private string? _nextPageToken;
    private CancellationTokenSource? _loadCts;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Максимум результатів на сторінку
    /// </summary>
    public const int MaxResultsPerPage = 50;

    public event EventHandler<string>? VideoOpenRequested;

    public VideoSearchResultsView()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
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
            
            // Підключаємо обробник кліку для контейнерів
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
        // Знаходимо VideoSearchResult з DataContext
        if (e.Source is Control control)
        {
            var current = control;
            while (current != null)
            {
                if (current.DataContext is VideoSearchResult result)
                {
                    if (!string.IsNullOrWhiteSpace(result.VideoUrl))
                    {
                        VideoOpenRequested?.Invoke(this, result.VideoUrl);
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
        _nextPageToken = null;
        
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
        // Додаємо таймаут 15 секунд щоб уникнути зависання
        _loadCts.CancelAfter(TimeSpan.FromSeconds(15));
        var ct = _loadCts.Token;

        try
        {
            var apiKey = ImageSearchServiceFactory.GetYouTubeApiKey();
            
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Немає API ключа - показуємо повідомлення і завершуємо
                System.Diagnostics.Debug.WriteLine("[VideoSearch] No YouTube API key configured - skipping search");
                _hasNextPage = false;
                
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_noResultsMessage != null)
                    {
                        _noResultsMessage.IsVisible = true;
                    }
                    if (_loadingIndicator != null) _loadingIndicator.IsVisible = false;
                });
                
                _isLoading = false;
                return;
            }

            var results = await SearchYouTubeAsync(_query, apiKey, ct);
            
            if (ct.IsCancellationRequested) return;

            foreach (var video in results)
            {
                _items.Add(video);
            }
            
            _currentPage++;
            
            if (_items.Count == 0 && _noResultsMessage != null)
            {
                _noResultsMessage.IsVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[VideoSearch] Search was cancelled or timed out");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VideoSearch] Error: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
            if (_loadingIndicator != null) _loadingIndicator.IsVisible = false;
        }
    }

    private async Task<System.Collections.Generic.List<VideoSearchResult>> SearchYouTubeAsync(
        string query, string apiKey, CancellationToken ct)
    {
        var results = new System.Collections.Generic.List<VideoSearchResult>();
        
        try
        {
            var url = $"https://www.googleapis.com/youtube/v3/search?" +
                      $"part=snippet&type=video&maxResults={MaxResultsPerPage}" +
                      $"&q={Uri.EscapeDataString(query)}&key={apiKey}";
            
            if (!string.IsNullOrWhiteSpace(_nextPageToken))
            {
                url += $"&pageToken={_nextPageToken}";
            }

            System.Diagnostics.Debug.WriteLine($"[VideoSearch] Searching: {query}");
            
            var response = await _httpClient.GetAsync(url, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"[VideoSearch] API Error: {response.StatusCode} - {error}");
                _hasNextPage = false;
                return results;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Наступна сторінка
            if (root.TryGetProperty("nextPageToken", out var nextToken))
            {
                _nextPageToken = nextToken.GetString();
                _hasNextPage = true;
            }
            else
            {
                _hasNextPage = false;
            }

            if (root.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    try
                    {
                        var id = item.GetProperty("id").GetProperty("videoId").GetString() ?? "";
                        var snippet = item.GetProperty("snippet");
                        
                        var video = new VideoSearchResult
                        {
                            Id = id,
                            VideoId = id,
                            Provider = "YouTube",
                            Title = snippet.GetProperty("title").GetString() ?? "Без назви",
                            Description = snippet.TryGetProperty("description", out var desc) 
                                ? desc.GetString() ?? "" : "",
                            ChannelTitle = snippet.TryGetProperty("channelTitle", out var channel) 
                                ? channel.GetString() ?? "" : "",
                            ChannelId = snippet.TryGetProperty("channelId", out var chId) 
                                ? chId.GetString() ?? "" : "",
                            VideoUrl = $"https://www.youtube.com/watch?v={id}",
                            ChannelUrl = snippet.TryGetProperty("channelId", out var chUrl) 
                                ? $"https://www.youtube.com/channel/{chUrl.GetString()}" : "",
                            Duration = "—" // Потрібен додатковий запит до videos API
                        };

                        // Thumbnail
                        if (snippet.TryGetProperty("thumbnails", out var thumbs))
                        {
                            if (thumbs.TryGetProperty("high", out var high))
                                video.ThumbnailUrl = high.GetProperty("url").GetString() ?? "";
                            else if (thumbs.TryGetProperty("medium", out var med))
                                video.ThumbnailUrl = med.GetProperty("url").GetString() ?? "";
                            else if (thumbs.TryGetProperty("default", out var def))
                                video.ThumbnailUrl = def.GetProperty("url").GetString() ?? "";
                        }

                        // Дата публікації
                        if (snippet.TryGetProperty("publishedAt", out var pubDate))
                        {
                            if (DateTime.TryParse(pubDate.GetString(), out var date))
                            {
                                video.PublishedAt = date;
                            }
                        }

                        results.Add(video);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoSearch] Error parsing item: {ex.Message}");
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[VideoSearch] Found {results.Count} videos");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VideoSearch] Exception: {ex.Message}");
        }

        return results;
    }

    private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer == null) return;

        var offset = _scrollViewer.Offset.Y;
        var extent = _scrollViewer.Extent.Height;
        var viewport = _scrollViewer.Viewport.Height;

        // Завантажуємо більше коли майже в кінці
        if (extent - (offset + viewport) < 300)
        {
            _ = LoadNextPageAsync();
        }
    }
}

