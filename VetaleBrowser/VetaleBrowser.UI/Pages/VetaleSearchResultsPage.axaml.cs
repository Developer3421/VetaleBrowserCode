using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media; // use Avalonia.Media for IImage
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Search.Services;
using VetaleBrowser.VetaleBrowser.UI.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VetaleSearchResultsPage : UserControl
{
    private string? _currentQuery;
    private Guid _currentSessionId;
    
    public event EventHandler<string>? NavigateRequested;
    public event EventHandler<SearchResultNavigationEventArgs>? SearchResultNavigateRequested;
    
    public Guid CurrentSessionId => _currentSessionId;
    public string? CurrentQuery => _currentQuery;
    
    private TextBlock? _searchStats;
    private StackPanel? _resultsPanel;
    private TextBlock? _queryHeading;
    private TextBox? _searchInput;
    private ComboBox? _searchEngineSelector;
    private Button? _searchButton;
    private Popup? _suggestionsPopup;
    private ItemsControl? _suggestionsListBox;

    // Embed Gemini chat panel
    private GeminiChatPanel? _geminiChat;

    private readonly ISuggestionsService _suggestionsService;
    private readonly IUnifiedSearchService _unifiedSearchService;
    private readonly IFaviconService _faviconService = new FaviconService();
    private CancellationTokenSource? _suggestionsCts;
    private CancellationTokenSource? _searchCts;

    private readonly ObservableCollection<string> _relatedQueries = new();

    public VetaleSearchResultsPage()
    {
        _suggestionsService = new GoogleSuggestionsService();
        _unifiedSearchService = new UnifiedSearchService();
        
        InitializeComponent();
        InitializeControls();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeControls()
    {
        _searchStats = this.FindControl<TextBlock>("SearchStats");
        _resultsPanel = this.FindControl<StackPanel>("ResultsPanel");
        _queryHeading = this.FindControl<TextBlock>("QueryHeading");
        _searchInput = this.FindControl<TextBox>("SearchInput");
        _searchEngineSelector = this.FindControl<ComboBox>("SearchEngineSelector");
        _searchButton = this.FindControl<Button>("SearchButton");
        _suggestionsPopup = this.FindControl<Popup>("SuggestionsPopup");
        _suggestionsListBox = this.FindControl<ItemsControl>("SuggestionsListBox");

        // Gemini chat control
        _geminiChat = this.FindControl<GeminiChatPanel>("GeminiChat");

        if (_searchInput != null)
        {
            _searchInput.Focus();
        }

        if (_searchButton != null)
        {
            _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput?.Text);
        }
    }

    /// <summary>
    /// Встановити пошуковий запит та завантажити результати
    /// </summary>
    public void SetSearchQuery(string query)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SetSearchQuery called with: '{query}'");
        _currentQuery = query;

        if (_searchInput != null)
        {
            _searchInput.Text = query;
        }

        if (_queryHeading != null)
        {
            _queryHeading.Text = string.IsNullOrWhiteSpace(query)
                ? "Локальний пошук Vetale"
                : $"Результати для: \"{query}\"";
        }

        LoadSearchResults(query);
        
        // Trigger Gemini chat with the query (acts like Copilot)
        _ = _geminiChat?.AskAsync(query);
    }

    /// <summary>
    /// Оновити статистику пошуку
    /// </summary>
    public void UpdateSearchStats(int totalResults, double searchTime)
    {
        if (_searchStats != null)
        {
            _searchStats.Text = $"Приблизно {totalResults:N0} результатів ({searchTime:F2} секунди)";
        }
    }

    /// <summary>
    /// Завантажити результати пошуку
    /// </summary>
    private async void LoadSearchResults(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Loading unified results for: {query}");

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            _searchStats!.Text = "Шукаємо в Wikipedia, WebArchive, CommonCrawl…";
            _resultsPanel!.Children.Clear();

            var start = DateTime.UtcNow;
            var page = await _unifiedSearchService.SearchAsync(query, pageNumber: 1, pageSize: 16, ct);
            var elapsed = (DateTime.UtcNow - start).TotalSeconds;

            if (ct.IsCancellationRequested)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                RenderUnifiedResults(page);
                UpdateSearchStats(page.TotalResults, elapsed);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] LoadSearchResults error: {ex}");
            if (_searchStats != null)
            {
                _searchStats.Text = "Сталась помилка під час пошуку. Спробуйте ще раз.";
            }
        }
    }

    private void RenderUnifiedResults(UnifiedSearchPage page)
    {
        _resultsPanel?.Children.Clear();

        // Зберігаємо SessionId для відстеження навігації
        _currentSessionId = page.SearchSessionId;

        if (page.Results == null || page.Results.Length == 0)
        {
            if (_searchStats != null)
            {
                _searchStats.Text = "Нічого не знайдено";
            }
            return;
        }

        foreach (var result in page.Results)
        {
            var searchResult = new SearchResult
            {
                Url = result.Url,
                DisplayUrl = result.DisplayUrl,
                Title = result.Title,
                Description = result.Snippet,
                Date = result.Timestamp,
                SourceType = result.Source.ToString() // зберігаємо тип джерела
            };
            // створюємо картку і отримуємо посилання на Image для фавікона
            var img = AddSearchResult(searchResult);
            // асинхронно підвантажуємо фавікон і оновлюємо лише цей контейнер
            _ = LoadFaviconAsync(searchResult, img);
        }

        // Після малювання результатів завантажуємо пов'язані запити (Google Suggestions)
        _ = LoadRelatedQueriesAsync(_currentQuery ?? string.Empty);
    }

    private async Task LoadRelatedQueriesAsync(string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ToggleRelated(false);
                return;
            }

            var suggestions = await _suggestionsService.GetSuggestionsAsync(query, 8);
            _relatedQueries.Clear();
            foreach (var s in suggestions)
            {
                if (!string.IsNullOrWhiteSpace(s.Text))
                    _relatedQueries.Add(s.Text);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var relatedSection = this.FindControl<StackPanel>("RelatedSection");
                var list = this.FindControl<ItemsControl>("RelatedQueriesList");
                if (relatedSection != null && list != null)
                {
                    list.ItemsSource = _relatedQueries;
                    relatedSection.IsVisible = _relatedQueries.Count > 0;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] LoadRelatedQueriesAsync error: {ex.Message}");
            ToggleRelated(false);
        }
    }

    private void ToggleRelated(bool visible)
    {
        var relatedSection = this.FindControl<StackPanel>("RelatedSection");
        if (relatedSection != null)
            relatedSection.IsVisible = visible;
    }

    private async Task LoadFaviconAsync(SearchResult result, Image? imageControl)
    {
        try
        {
            if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var uri))
                return;

            var image = await _faviconService.GetFaviconAsync(uri);
            result.Favicon = image;
            if (imageControl != null && image != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    imageControl.Source = image;
                });
            }
        }
        catch
        {
            // ігноруємо помилки фавікона
        }
    }

    /// <summary>
    /// Додати результат пошуку програмно
    /// </summary>
    public Image? AddSearchResult(SearchResult result)
    {
        if (_resultsPanel == null)
            return null;

        var resultBorder = new Border
        {
            Classes = { "result-item" }
        };

        // верхній ряд: фавікон + URL
        var headerPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
            Spacing = 6
        };

        var faviconImage = new Image
        {
            Width = 16,
            Height = 16,
            Margin = new Avalonia.Thickness(0, 1, 0, 0)
        };
        if (result.Favicon != null)
        {
            faviconImage.Source = result.Favicon;
        }

        headerPanel.Children.Add(faviconImage);

        var breadcrumb = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal
        };
        var urlParts = result.DisplayUrl.Split(new[] { " › ", "›" }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < urlParts.Length; i++)
        {
            breadcrumb.Children.Add(new TextBlock
            {
                Classes = { "breadcrumb-text" },
                Text = urlParts[i].Trim()
            });

            if (i < urlParts.Length - 1)
            {
                breadcrumb.Children.Add(new TextBlock
                {
                    Classes = { "breadcrumb-sep" },
                    Text = "›"
                });
            }
        }
        headerPanel.Children.Add(breadcrumb);

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(headerPanel);

        // Title
        var titleBlock = new TextBlock
        {
            Classes = { "result-title" },
            Text = result.Title
        };
        titleBlock.PointerPressed += ResultTitle_Click;
        stackPanel.Children.Add(titleBlock);

        // Date (optional)
        if (result.Date.HasValue)
        {
            stackPanel.Children.Add(new TextBlock
            {
                Classes = { "result-date" },
                Text = result.Date.Value.ToString("d MMM yyyy")
            });
        }

        // Rating (optional)
        if (result.Rating.HasValue && result.ReviewCount.HasValue)
        {
            var ratingPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Avalonia.Thickness(0, 4)
            };

            int fullStars = (int)Math.Floor(result.Rating.Value);
            for (int i = 0; i < fullStars; i++)
            {
                ratingPanel.Children.Add(new TextBlock
                {
                    Classes = { "star" },
                    Text = "★"
                });
            }

            ratingPanel.Children.Add(new TextBlock
            {
                Classes = { "rating-count" },
                Text = $"{result.Rating.Value:F1} · {result.ReviewCount.Value} відгуків"
            });

            stackPanel.Children.Add(ratingPanel);
        }

        // Description
        var descBlock = new TextBlock
        {
            Classes = { "result-description" },
            Text = result.Description
        };
        stackPanel.Children.Add(descBlock);

        resultBorder.Child = stackPanel;
        resultBorder.DataContext = result;

        _resultsPanel.Children.Add(resultBorder);
        return faviconImage;
    }


    /// <summary>
    /// Очистити всі результати
    /// </summary>
    public void ClearResults()
    {
        _resultsPanel?.Children.Clear();
        if (_searchStats != null)
        {
            _searchStats.Text = "Немає результатів";
        }
    }

    public void SearchInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_searchButton != null)
        {
            _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput?.Text);
        }

        // Завантажуємо підказки з дебаунсом
        _ = LoadSuggestionsAsync();
    }

    public async Task LoadSuggestionsAsync()
    {
        // Скасовуємо попередній запит
        _suggestionsCts?.Cancel();
        _suggestionsCts = new CancellationTokenSource();
        var token = _suggestionsCts.Token;

        try
        {
            // Дебаунс 300мс
            await Task.Delay(300, token);

            var query = _searchInput?.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                // Ховаємо попап якщо запит короткий
                if (_suggestionsPopup != null)
                {
                    _suggestionsPopup.IsOpen = false;
                }
                return;
            }

            // Завантажуємо підказки
            var suggestions = await _suggestionsService.GetSuggestionsAsync(query, 8);

            if (token.IsCancellationRequested)
                return;

            // Оновлюємо UI
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_suggestionsListBox != null)
                {
                    _suggestionsListBox.ItemsSource = suggestions;
                }

                if (_suggestionsPopup != null && suggestions.Count > 0)
                {
                    _suggestionsPopup.IsOpen = true;
                }
                else if (_suggestionsPopup != null)
                {
                    _suggestionsPopup.IsOpen = false;
                }
            });
        }
        catch (TaskCanceledException)
        {
            // Нормальна ситуація при скасуванні
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] LoadSuggestionsAsync error: {ex.Message}");
        }
    }

    public void SuggestionItem_Click(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (sender is Border border && border.DataContext is SearchSuggestion suggestion)
            {
                // Встановлюємо текст у поле пошуку
                if (_searchInput != null)
                {
                    _searchInput.Text = suggestion.Text;
                }

                // Ховаємо попап
                if (_suggestionsPopup != null)
                {
                    _suggestionsPopup.IsOpen = false;
                }

                // Виконуємо пошук
                PerformSearch();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SuggestionItem_Click error: {ex.Message}");
        }
    }

    public void SearchInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
        }
    }

    public void Search_Click(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Search button clicked!");
        PerformSearch();
    }

    public void LuckySearch_Click(object? sender, RoutedEventArgs e)
    {
        PerformSearch(isLucky: true);
    }

    public void SearchEngineSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Поки що просто лог для діагностики
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Search engine changed to index: {_searchEngineSelector?.SelectedIndex}");
    }

    private void PerformSearch(bool isLucky = false)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] PerformSearch called, isLucky={isLucky}");
        
        if (_searchInput == null || string.IsNullOrWhiteSpace(_searchInput.Text))
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] PerformSearch aborted: empty query");
            return;
        }

        var query = _searchInput.Text.Trim();
        _currentQuery = query;

        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] PerformSearch query: '{query}'");
        LoadSearchResults(query);

        // опційно: lucky може одразу відкривати перший результат у майбутньому
        _ = _geminiChat?.AskAsync(query);
    }

    public void ResultTitle_Click(object? sender, PointerPressedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] ===== ResultTitle_Click CALLED =====");
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Sender type: {sender?.GetType().Name ?? "null"}");
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Event args: {e != null}");
        
        if (sender is TextBlock titleBlock)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Sender is TextBlock, text: '{titleBlock.Text}'");
            
            var border = FindParentBorder(titleBlock);
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Parent border found: {border != null}");
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Border.DataContext type: {border?.DataContext?.GetType().Name ?? "null"}");
            
            if (border?.DataContext is SearchResult result)
            {
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] *** FOUND SearchResult! ***");
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] URL: '{result.Url}'");
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Title: '{result.Title}'");
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SourceType: '{result.SourceType}'");
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SessionId: {_currentSessionId}");
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Query: '{_currentQuery}'");
                
                // Викликаємо нову подію з додатковою інформацією
                var args = new SearchResultNavigationEventArgs
                {
                    Url = result.Url,
                    SessionId = _currentSessionId,
                    Query = _currentQuery ?? string.Empty,
                    SourceType = result.SourceType
                };
                
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Invoking SearchResultNavigateRequested event...");
                SearchResultNavigateRequested?.Invoke(this, args);
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SearchResultNavigateRequested invoked");
                
                // Залишаємо стару подію для сумісності
                NavigateRequested?.Invoke(this, result.Url);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Border.DataContext is NOT SearchResult, trying breadcrumb fallback");
                var stackPanel = titleBlock.Parent as StackPanel;
                if (stackPanel != null)
                {
                    var breadcrumbPanel = FindBreadcrumbPanel(stackPanel);
                    if (breadcrumbPanel != null)
                    {
                        var urlText = ExtractUrlFromBreadcrumb(breadcrumbPanel);
                        if (!string.IsNullOrEmpty(urlText))
                        {
                            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Opening static result: {urlText}");
                            NavigateRequested?.Invoke(this, urlText);
                        }
                    }
                }
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] ERROR: Sender is NOT TextBlock!");
        }
        
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] ===== ResultTitle_Click END =====");
    }

    private Border? FindParentBorder(Control control)
    {
        var parent = control.Parent;
        while (parent != null)
        {
            if (parent is Border border)
                return border;
            parent = parent.Parent;
        }
        return null;
    }

    private StackPanel? FindBreadcrumbPanel(StackPanel parentPanel)
    {
        foreach (var child in parentPanel.Children)
        {
            if (child is StackPanel sp && sp.Orientation == Avalonia.Layout.Orientation.Horizontal)
            {
                foreach (var grandChild in sp.Children)
                {
                    if (grandChild is TextBlock tb && tb.Classes.Contains("breadcrumb-text"))
                    {
                        return sp;
                    }
                }
            }
        }
        return null;
    }

    private string? ExtractUrlFromBreadcrumb(StackPanel breadcrumbPanel)
    {
        var urlParts = new List<string>();
        foreach (var child in breadcrumbPanel.Children)
        {
            if (child is TextBlock tb && tb.Classes.Contains("breadcrumb-text"))
            {
                urlParts.Add(tb.Text ?? "");
            }
        }

        if (urlParts.Count > 0)
        {
            var domain = urlParts[0];
            if (!domain.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                domain = "https://" + domain;
            }
            return domain;
        }

        return null;
    }

    public void RelatedQuery_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? query = null;
            if (sender is Button btn)
            {
                query = btn.Content as string ?? btn.DataContext?.ToString();
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                if (_searchInput != null)
                    _searchInput.Text = query;
                PerformSearch();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] RelatedQuery_Click error: {ex.Message}");
        }
    }
}

/// <summary>
/// Модель результату пошуку
/// </summary>
public class SearchResult
{
    public string Url { get; set; } = string.Empty;
    public string DisplayUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public double? Rating { get; set; }
    public int? ReviewCount { get; set; }
    public IImage? Favicon { get; set; }
    public string SourceType { get; set; } = string.Empty; // Wikipedia/WebArchive/YouTube
}

/// <summary>
/// Аргументи події навігації з результату пошуку
/// </summary>
public class SearchResultNavigationEventArgs : EventArgs
{
    public string Url { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string Query { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
}
