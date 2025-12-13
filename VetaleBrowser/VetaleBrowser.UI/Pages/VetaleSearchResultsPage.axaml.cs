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
using VetaleBrowser.VetaleBrowser.UI.Elements; // NavigationBar
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Search.Services;
using VetaleBrowser.VetaleBrowser.UI.Services;
using VetaleBrowser.VetaleBrowser.UI.Windows;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.VoiceRecognition.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public enum SearchMode
{
    Sites,
    Images,
    Videos
}

public partial class VetaleSearchResultsPage : UserControl
{
    private string? _currentQuery;
    private Guid _currentSessionId;
    private SearchMode _currentMode = SearchMode.Sites;
    
    public event EventHandler<string>? NavigateRequested;
    public event EventHandler<SearchResultNavigationEventArgs>? SearchResultNavigateRequested;
    
    public Guid CurrentSessionId => _currentSessionId;
    public string? CurrentQuery => _currentQuery;
    
    private TextBlock? _searchStats;
    private StackPanel? _resultsPanel;
    private StackPanel? _imageResultsHost;
    private StackPanel? _videoResultsHost;
    private Controls.ImageSearchResultsView? _imageResultsView;
    private Controls.VideoSearchResultsView? _videoResultsView;
    private TextBlock? _queryHeading;
    private TextBox? _searchInput;
    private ComboBox? _searchEngineSelector;
    private Button? _searchButton;
    private Button? _voiceButton;
    private Popup? _suggestionsPopup;
    private ItemsControl? _suggestionsListBox;
    
    // Панель пагінації
    private StackPanel? _paginationPanel;

    // Embed Gemini chat panel
    private GeminiChatPanel? _geminiChat;

    private readonly ISuggestionsService _suggestionsService;
    private readonly IUnifiedSearchService _unifiedSearchService;
    private readonly IFaviconService _faviconService = new FaviconService();
    private IVoiceRecognitionService? _voiceRecognitionService;
    private CancellationTokenSource? _suggestionsCts;
    private CancellationTokenSource? _searchCts;

    private readonly ObservableCollection<string> _relatedQueries = new();
    
    // Стан пагінації
    private int _currentPage = 1;
    private bool _hasNextPage = false;
    private bool _hasPreviousPage = false;
    private const int ResultsPerPage = 50;
    
    // Прапорці для контролю показу вікон API ключів
    // true = треба показати вікно, false = вже показали
    private static bool _shouldShowGeminiApiKeyPrompt = true;
    private static bool _shouldShowImageSearchApiKeyPrompt = true;
    private static bool _shouldShowVideoSearchApiKeyPrompt = true;

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
        _imageResultsHost = this.FindControl<StackPanel>("ImageResultsHost");
        _videoResultsHost = this.FindControl<StackPanel>("VideoResultsHost");
        _imageResultsView = this.FindControl<Controls.ImageSearchResultsView>("ImageResultsView");
        _videoResultsView = this.FindControl<Controls.VideoSearchResultsView>("VideoResultsView");
        _queryHeading = this.FindControl<TextBlock>("QueryHeading");
        _searchInput = this.FindControl<TextBox>("SearchInput");
        _searchEngineSelector = this.FindControl<ComboBox>("SearchEngineSelector");
        _searchButton = this.FindControl<Button>("SearchButton");
        _voiceButton = this.FindControl<Button>("VoiceButton");
        _suggestionsPopup = this.FindControl<Popup>("SuggestionsPopup");
        _suggestionsListBox = this.FindControl<ItemsControl>("SuggestionsListBox");

        // Gemini chat control
        _geminiChat = this.FindControl<GeminiChatPanel>("GeminiChat");
        
        // Підключаємо навігацію з GeminiChat
        if (_geminiChat != null)
        {
            _geminiChat.NavigateRequested += (s, url) =>
            {
                if (!string.IsNullOrWhiteSpace(url))
                    NavigateRequested?.Invoke(this, url);
            };
        }

        if (_searchInput != null)
        {
            _searchInput.Focus();
        }

        if (_searchButton != null)
        {
            _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput?.Text);
        }

        // Підключаємо навігацію з ImageResultsView
        if (_imageResultsView != null)
        {
            _imageResultsView.SourcePageOpenRequested += (s, url) =>
            {
                if (!string.IsNullOrWhiteSpace(url))
                    NavigateRequested?.Invoke(this, url);
            };
        }
        
        // Підключаємо навігацію з VideoResultsView
        if (_videoResultsView != null)
        {
            _videoResultsView.VideoOpenRequested += (s, url) =>
            {
                if (!string.IsNullOrWhiteSpace(url))
                    NavigateRequested?.Invoke(this, url);
            };
        }
    }

    /// <summary>
    /// Встановити пошуковий запит та завантажити результати
    /// </summary>
    public void SetSearchQuery(string query, string? mode = null)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SetSearchQuery called with: '{query}', mode='{mode}'");
        _currentQuery = query;

        if (!string.IsNullOrWhiteSpace(mode) && mode.Equals("images", StringComparison.OrdinalIgnoreCase))
        {
            _currentMode = SearchMode.Images;
        }
        else
        {
            _currentMode = SearchMode.Sites;
        }

        UpdateModeVisuals();

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
    /// Оновити статистику пошуку (для сумісності)
    /// </summary>
    public void UpdateSearchStats(int totalResults, double searchTime)
    {
        UpdateSearchStats(totalResults, searchTime, _currentPage);
    }

    /// <summary>
    /// Завантажити результати пошуку
    /// </summary>
    private async void LoadSearchResults(string query, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Loading unified results for: {query}, page: {page}");

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            _currentPage = page;
            
            // Локалізовані повідомлення
            var searchingText = GetLocalizedString("Search.Results.SearchingIn") 
                ?? "Шукаємо в Wikipedia, WebArchive, MetaSearx…";
            var loadingPageTemplate = GetLocalizedString("Search.Results.LoadingPage") 
                ?? "Завантажуємо сторінку {0}…";
            
            _searchStats!.Text = page == 1 
                ? searchingText 
                : string.Format(loadingPageTemplate, page);
            _resultsPanel!.Children.Clear();

            var start = DateTime.UtcNow;
            var searchPage = await _unifiedSearchService.SearchAsync(query, pageNumber: page, pageSize: ResultsPerPage, ct);
            var elapsed = (DateTime.UtcNow - start).TotalSeconds;

            if (ct.IsCancellationRequested)
                return;

            _hasNextPage = searchPage.HasNextPage;
            _hasPreviousPage = searchPage.HasPreviousPage;

            Dispatcher.UIThread.Post(() =>
            {
                RenderUnifiedResults(searchPage);
                UpdateSearchStats(searchPage.TotalResults, elapsed, page);
                RenderPaginationPanel();
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
                var errorText = GetLocalizedString("Search.Results.Error") 
                    ?? "Сталась помилка під час пошуку. Спробуйте ще раз.";
                _searchStats.Text = errorText;
            }
        }
    }
    
    /// <summary>
    /// Оновлює статистику пошуку
    /// </summary>
    private void UpdateSearchStats(int totalResults, double elapsed, int page = 1)
    {
        if (_searchStats != null)
        {
            if (page > 1)
            {
                // Спробуємо отримати локалізований рядок
                var template = GetLocalizedString("Search.Results.StatsWithPage") 
                    ?? "Знайдено {0} результатів за {1} сек. (сторінка {2})";
                _searchStats.Text = string.Format(template, totalResults, elapsed.ToString("F2"), page);
            }
            else
            {
                var template = GetLocalizedString("Search.Results.Stats") 
                    ?? "Знайдено {0} результатів за {1} сек.";
                _searchStats.Text = string.Format(template, totalResults, elapsed.ToString("F2"));
            }
        }
    }
    
    /// <summary>
    /// Отримує локалізований рядок з ресурсів
    /// </summary>
    private string? GetLocalizedString(string key)
    {
        try
        {
            if (Avalonia.Application.Current != null && 
                Avalonia.Application.Current.TryFindResource(key, out var resource) &&
                resource is string str)
            {
                return str;
            }
        }
        catch { }
        return null;
    }
    
    /// <summary>
    /// Рендерить панель пагінації
    /// </summary>
    private void RenderPaginationPanel()
    {
        // Видаляємо стару панель пагінації якщо є
        if (_paginationPanel != null && _resultsPanel != null)
        {
            _resultsPanel.Children.Remove(_paginationPanel);
        }
        
        // Створюємо нову панель пагінації
        _paginationPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 20, 0, 20),
            Spacing = 8
        };
        
        // Локалізовані тексти
        var prevText = GetLocalizedString("Search.Pagination.Previous") ?? "← Попередня";
        var nextText = GetLocalizedString("Search.Pagination.Next") ?? "Наступна →";
        
        // Кнопка "Попередня"
        if (_hasPreviousPage)
        {
            var prevButton = CreatePaginationButton(prevText, _currentPage - 1);
            _paginationPanel.Children.Add(prevButton);
        }
        
        // Номери сторінок (показуємо 5 сторінок навколо поточної)
        var startPage = Math.Max(1, _currentPage - 2);
        var endPage = startPage + 4;
        
        // Перша сторінка якщо не в діапазоні
        if (startPage > 1)
        {
            _paginationPanel.Children.Add(CreatePaginationButton("1", 1));
            if (startPage > 2)
            {
                _paginationPanel.Children.Add(new TextBlock 
                { 
                    Text = "...", 
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Foreground = Avalonia.Media.Brushes.Gray,
                    Margin = new Avalonia.Thickness(4, 0)
                });
            }
        }
        
        // Сторінки в діапазоні
        for (int i = startPage; i <= endPage; i++)
        {
            var pageButton = CreatePaginationButton(i.ToString(), i, i == _currentPage);
            _paginationPanel.Children.Add(pageButton);
            
            // Якщо це остання відома сторінка і немає наступної - виходимо
            if (i == _currentPage && !_hasNextPage)
                break;
        }
        
        // Кнопка "Наступна"
        if (_hasNextPage)
        {
            var nextButton = CreatePaginationButton(nextText, _currentPage + 1);
            _paginationPanel.Children.Add(nextButton);
        }
        
        // Додаємо панель в кінець результатів
        _resultsPanel?.Children.Add(_paginationPanel);
    }
    
    /// <summary>
    /// Створює кнопку пагінації
    /// </summary>
    private Button CreatePaginationButton(string text, int targetPage, bool isCurrentPage = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 40,
            Padding = new Avalonia.Thickness(12, 8),
            Background = isCurrentPage 
                ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FF6B00"))
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0F0F0")),
            Foreground = isCurrentPage 
                ? Avalonia.Media.Brushes.White 
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333")),
            BorderThickness = new Avalonia.Thickness(1),
            BorderBrush = isCurrentPage 
                ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FF6B00"))
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC")),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            CornerRadius = new Avalonia.CornerRadius(6)
        };
        
        if (!isCurrentPage)
        {
            button.Click += (s, e) => 
            {
                if (!string.IsNullOrWhiteSpace(_currentQuery))
                {
                    LoadSearchResults(_currentQuery, targetPage);
                    
                    // Скролимо вгору
                    var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
                    scrollViewer?.ScrollToHome();
                }
            };
        }
        
        return button;
    }

    private void RenderUnifiedResults(UnifiedSearchPage page)
    {
        _resultsPanel?.Children.Clear();

        // Зберігаємо SessionId для відстеження навігації
        _currentSessionId = page.SearchSessionId;

        // 1) Якщо початковий запит схожий на прямий URL — додаємо його першим елементом
        var query = _currentQuery ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(query) && NavigationBar.TryNormalizeUserUrl(query, out var directUrl))
        {
            try
            {
                string displayUrl = BuildDisplayUrl(directUrl);
                string domain = ExtractDomain(directUrl);
                var direct = new SearchResult
                {
                    Url = directUrl,
                    DisplayUrl = displayUrl,
                    Title = $"🌐 Перейти на {domain}",
                    Description = $"Відкрити сайт: {directUrl}",
                    Date = null,
                    SourceType = "DirectUrl"
                };
                var img = AddSearchResult(direct);
                _ = LoadFaviconAsync(direct, img);
            }
            catch { }
        }

        if (page.Results == null || page.Results.Length == 0)
        {
            if (_searchStats != null)
            {
                _searchStats.Text = GetLocalizedString("Search.Results.NoResults") ?? "Нічого не знайдено";
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
            var img = AddSearchResult(searchResult);
            _ = LoadFaviconAsync(searchResult, img);
        }

        // Після малювання результатів завантажуємо пов'язані запити (Google Suggestions)
        _ = LoadRelatedQueriesAsync(_currentQuery ?? string.Empty);
    }

    private static string BuildDisplayUrl(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
            var host = uri.Host;
            var path = uri.AbsolutePath?.Trim('/') ?? string.Empty;
            if (string.IsNullOrEmpty(path)) return host;
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // Формат: host › part1 › part2 (не більше 2 частин)
            var sb = new System.Text.StringBuilder(host);
            for (int i = 0; i < Math.Min(parts.Length, 2); i++)
            {
                sb.Append(" › ").Append(parts[i]);
            }
            return sb.ToString();
        }
        catch { return url; }
    }

    private static string ExtractDomain(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
            return uri.Host;
        }
        catch { return url; }
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

            Dispatcher.UIThread.Post(() =>
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
                Dispatcher.UIThread.Post(() =>
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

        var resultBorder = new Border();
        
        // Застосувати спеціальний клас для прямих URL
        if (result.SourceType == "DirectUrl")
        {
            resultBorder.Classes.Add("result-item-direct");
        }
        else
        {
            resultBorder.Classes.Add("result-item");
        }

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
        
        // ВАЖЛИВО: додаємо обробник кліку на сам Border, щоб клік точно спрацював
        resultBorder.PointerPressed += ResultBorder_Click;

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
            Dispatcher.UIThread.Post(() =>
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
            // Знаходимо елемент на який клікнули
            if (e.Source is Control clickedControl)
            {
                // Шукаємо SearchSuggestion в DataContext поточного або батьківських елементів
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
                
                if (suggestion != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Suggestion clicked: {suggestion.Text}");
                    
                    // Вставляємо текст підказки в поле пошуку
                    if (_searchInput != null)
                    {
                        _searchInput.Text = suggestion.Text;
                        _searchInput.CaretIndex = suggestion.Text.Length;
                        _searchInput.Focus();
                    }

                    // Ховаємо попап
                    if (_suggestionsPopup != null)
                    {
                        _suggestionsPopup.IsOpen = false;
                    }

                    // НЕ виконуємо пошук автоматично - даємо користувачу редагувати
                    // Якщо потрібно автоматично шукати, розкоментуйте:
                    // PerformSearch();
                    
                    e.Handled = true;
                }
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
        _currentPage = 1; // Скидаємо на першу сторінку
        LoadSearchResults(query, 1);

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
                
                // ГОЛОВНЕ: викликаємо NavigateRequested першою (вона точно працює)
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Invoking NavigateRequested with URL: '{result.Url}'");
                NavigateRequested?.Invoke(this, result.Url);
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] NavigateRequested invoked");
                
                // Також викликаємо нову подію для історії
                var args = new SearchResultNavigationEventArgs
                {
                    Url = result.Url,
                    SessionId = _currentSessionId,
                    Query = _currentQuery ?? string.Empty,
                    SourceType = result.SourceType
                };
                
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Invoking SearchResultNavigateRequested for history...");
                SearchResultNavigateRequested?.Invoke(this, args);
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SearchResultNavigateRequested invoked");
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

    public void ResultBorder_Click(object? sender, PointerPressedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] ===== ResultBorder_Click CALLED =====");
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Sender type: {sender?.GetType().Name ?? "null"}");
        
        Border? border = sender as Border;
        
        if (border != null && border.DataContext is SearchResult result)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] *** FOUND SearchResult from Border! ***");
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] URL: '{result.Url}'");
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Title: '{result.Title}'");
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SourceType: '{result.SourceType}'");
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SessionId: {_currentSessionId}");
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Query: '{_currentQuery}'");
            
            // Викликаємо NavigateRequested
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Invoking NavigateRequested with URL: '{result.Url}'");
            NavigateRequested?.Invoke(this, result.Url);
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] NavigateRequested invoked");
            
            // Також викликаємо подію для історії
            var args = new SearchResultNavigationEventArgs
            {
                Url = result.Url,
                SessionId = _currentSessionId,
                Query = _currentQuery ?? string.Empty,
                SourceType = result.SourceType
            };
            
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Invoking SearchResultNavigateRequested for history...");
            SearchResultNavigateRequested?.Invoke(this, args);
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SearchResultNavigateRequested invoked");
            
            // Позначаємо подію як оброблену, щоб не спливала далі
            e.Handled = true;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] ERROR: Border.DataContext is NOT SearchResult!");
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] DataContext type: {border?.DataContext?.GetType().Name ?? "null"}");
        }
        
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] ===== ResultBorder_Click END =====");
    }

    public void SetVoiceRecognitionService(IVoiceRecognitionService service)
    {
        _voiceRecognitionService = service;
        
        // Підписуємося на події
        if (_voiceRecognitionService != null)
        {
            _voiceRecognitionService.TextRecognized += OnVoiceTextRecognized;
            _voiceRecognitionService.StateChanged += OnVoiceStateChanged;
            _voiceRecognitionService.ErrorOccurred += OnVoiceError;
        }
    }

    // Обробники подій голосового розпізнавання
    private async void VoiceButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_voiceRecognitionService == null)
        {
            System.Diagnostics.Debug.WriteLine("[VetaleSearchResultsPage] Voice recognition service not initialized");
            return;
        }

        try
        {
            if (_voiceRecognitionService.CurrentState == VoiceRecognitionState.Listening)
            {
                // Якщо вже слухаємо, зупиняємо
                _voiceRecognitionService.StopListening();
            }
            else
            {
                // Перевіряємо доступність
                if (!_voiceRecognitionService.IsAvailable())
                {
                    System.Diagnostics.Debug.WriteLine("[VetaleSearchResultsPage] Voice recognition not available");
                    OnVoiceError(this, "Розпізнавання голосу недоступне на цьому пристрої");
                    return;
                }

                // Починаємо слухати
                await _voiceRecognitionService.StartListeningAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] VoiceButton_Click error: {ex.Message}");
            OnVoiceError(this, $"Помилка: {ex.Message}");
        }
    }

    private void OnVoiceTextRecognized(object? sender, string text)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Voice text recognized: {text}");
        
        // Оновлюємо UI в UI-потоці
        Dispatcher.UIThread.Post(() =>
        {
            if (_searchInput != null)
            {
                _searchInput.Text = text;
                if (_searchButton != null)
                {
                    _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(text);
                }
            }
            
            // Автоматично зупиняємо після розпізнавання
            _voiceRecognitionService?.StopListening();
            
            // Автоматично виконуємо пошук
            PerformSearch();
        });
    }

    private void OnVoiceStateChanged(object? sender, VoiceRecognitionState state)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Voice state changed: {state}");
        
        // Оновлюємо UI в UI-потоці
        Dispatcher.UIThread.Post(() =>
        {
            if (_voiceButton != null)
            {
                // Змінюємо вигляд кнопки в залежності від стану
                var iconText = state switch
                {
                    VoiceRecognitionState.Listening => "⏹️", // Зупинити
                    VoiceRecognitionState.Processing => "⏳", // Обробка
                    _ => "🎤" // Мікрофон
                };
                
                // Створюємо новий TextBlock
                _voiceButton.Content = new TextBlock 
                { 
                    Text = iconText,
                    FontSize = 18
                };
                
                _voiceButton.IsEnabled = state != VoiceRecognitionState.Processing;
            }
        });
    }

    private void OnVoiceError(object? sender, string error)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Voice error: {error}");
        
        // TODO: Показати користувачу повідомлення про помилку
    }

    public void GeoSearch_Click(object? sender, RoutedEventArgs e)
    {
        if (_searchInput == null || string.IsNullOrWhiteSpace(_searchInput.Text)) return;
        var query = _searchInput.Text.Trim();
        var mapsUrl = $"https://www.openstreetmap.org/search?query={Uri.EscapeDataString(query)}";
        NavigateRequested?.Invoke(this, mapsUrl);
    }

    public void SetMode(SearchMode mode)
    {
        _currentMode = mode;
        UpdateModeVisuals();
    }

    private void UpdateModeVisuals()
    {
        var sitesButton = this.FindControl<Button>("ModeSitesButton");
        var imagesButton = this.FindControl<Button>("ModeImagesButton");
        var videosButton = this.FindControl<Button>("ModeVideosButton");
        
        sitesButton?.Classes.Remove("active");
        imagesButton?.Classes.Remove("active");
        videosButton?.Classes.Remove("active");
        
        switch (_currentMode)
        {
            case SearchMode.Sites:
                sitesButton?.Classes.Add("active");
                break;
            case SearchMode.Images:
                imagesButton?.Classes.Add("active");
                break;
            case SearchMode.Videos:
                videosButton?.Classes.Add("active");
                break;
        }

        if (_resultsPanel != null)
            _resultsPanel.IsVisible = _currentMode == SearchMode.Sites;
        if (_imageResultsHost != null)
            _imageResultsHost.IsVisible = _currentMode == SearchMode.Images;
        if (_videoResultsHost != null)
            _videoResultsHost.IsVisible = _currentMode == SearchMode.Videos;
    }

    private async void ModeSites_Click(object? sender, RoutedEventArgs e)
    {
        // Показуємо вікно Gemini API при першому пошуку сайтів (якщо ще не показували)
        if (_shouldShowGeminiApiKeyPrompt)
        {
            _shouldShowGeminiApiKeyPrompt = false; // Більше не показувати
            await ShowGeminiApiKeyPromptAsync();
        }
        
        SetMode(SearchMode.Sites);
        if (_currentQuery != null)
        {
            LoadSearchResults(_currentQuery);
        }
    }

    private async void ModeImages_Click(object? sender, RoutedEventArgs e)
    {
        // Показуємо вікно API ключів для пошуку зображень при першому використанні
        if (_shouldShowImageSearchApiKeyPrompt)
        {
            _shouldShowImageSearchApiKeyPrompt = false; // Більше не показувати
            await ShowImageSearchApiKeyPromptAsync();
        }
        
        SetMode(SearchMode.Images);
        if (_currentQuery != null && _imageResultsView != null)
        {
            _imageResultsView.SetQuery(_currentQuery);
        }
    }
    
    /// <summary>
    /// Показує вікно для налаштування Gemini API ключа
    /// </summary>
    private async Task ShowGeminiApiKeyPromptAsync()
    {
        try
        {
            // Перевіряємо чи вже є ключ в БД
            var apiKeysService = DatabaseServicesFactory.TryGetApiKeysService();
            if (apiKeysService != null)
            {
                var existingKey = await apiKeysService.GetGeminiApiKeyAsync();
                if (!string.IsNullOrWhiteSpace(existingKey))
                {
                    System.Diagnostics.Debug.WriteLine("[VetaleSearch] Gemini API key already configured");
                    return;
                }
            }
            
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            
            // Передаємо callback для навігації у браузері Vetale
            var result = await ApiKeyConfigWindow.ShowGeminiConfigAsync(parentWindow, url =>
            {
                NavigateRequested?.Invoke(this, url);
            });
            
            if (result.Saved && !string.IsNullOrWhiteSpace(result.ApiKey))
            {
                // Оновлюємо сервіс Gemini
                GeminiAiSummaryService.SetCustomApiKey(result.ApiKey);
                System.Diagnostics.Debug.WriteLine("[VetaleSearch] Gemini API key configured");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearch] Error showing Gemini API key prompt: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Показує вікна для налаштування API ключів пошуку зображень (Pexels, Unsplash)
    /// </summary>
    private async Task ShowImageSearchApiKeyPromptAsync()
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            
            // Callback для навігації у браузері Vetale
            Action<string> navigateCallback = url => NavigateRequested?.Invoke(this, url);
            
            // Показуємо вікно для Pexels
            var pexelsResult = await ApiKeyConfigWindow.ShowPexelsConfigAsync(parentWindow, navigateCallback);
            if (pexelsResult.Saved)
            {
                System.Diagnostics.Debug.WriteLine("[VetaleSearch] Pexels API key configured");
            }
            
            // Показуємо вікно для Unsplash
            var unsplashResult = await ApiKeyConfigWindow.ShowUnsplashConfigAsync(parentWindow, navigateCallback);
            if (unsplashResult.Saved)
            {
                System.Diagnostics.Debug.WriteLine("[VetaleSearch] Unsplash API key configured");
            }
            
            // Переініціалізуємо сервіс пошуку зображень
            if (_imageResultsView != null)
            {
                _imageResultsView.ImageSearchService = ImageSearchServiceFactory.Create();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearch] Error showing image search API key prompt: {ex.Message}");
        }
    }
    
    private async void ModeVideos_Click(object? sender, RoutedEventArgs e)
    {
        // Показуємо вікно API ключів для пошуку відео при першому використанні
        if (_shouldShowVideoSearchApiKeyPrompt)
        {
            _shouldShowVideoSearchApiKeyPrompt = false; // Більше не показувати
            await ShowVideoSearchApiKeyPromptAsync();
        }
        
        SetMode(SearchMode.Videos);
        
        // Запускаємо пошук відео
        if (_currentQuery != null && _videoResultsView != null)
        {
            _videoResultsView.SetQuery(_currentQuery);
        }
    }
    
    /// <summary>
    /// Показує вікно для налаштування YouTube API ключа
    /// </summary>
    private async Task ShowVideoSearchApiKeyPromptAsync()
    {
        try
        {
            // Перевіряємо чи вже є ключ в БД
            var apiKeysService = DatabaseServicesFactory.TryGetApiKeysService();
            if (apiKeysService != null)
            {
                var existingKey = await apiKeysService.GetYouTubeApiKeyAsync();
                if (!string.IsNullOrWhiteSpace(existingKey))
                {
                    System.Diagnostics.Debug.WriteLine("[VetaleSearch] YouTube API key already configured");
                    return;
                }
            }
            
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            
            // Передаємо callback для навігації у браузері Vetale
            var result = await ApiKeyConfigWindow.ShowYouTubeConfigAsync(parentWindow, url =>
            {
                NavigateRequested?.Invoke(this, url);
            });
            
            if (result.Saved)
            {
                System.Diagnostics.Debug.WriteLine("[VetaleSearch] YouTube API key configured");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearch] Error showing YouTube API key prompt: {ex.Message}");
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
