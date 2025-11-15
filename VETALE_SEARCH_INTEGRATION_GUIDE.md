# Vetale Search - Приклад інтеграції

## 📋 Покрокова інструкція інтеграції

### Крок 1: Ініціалізація сервісу пошуку

У головному класі вашого додатку (наприклад, `App.axaml.cs` або `MainWindow.cs`):

```csharp
using VetaleBrowser.VetaleBrowser.Database.Services;

public class App : Application
{
    private static SearchIndexService? _searchIndexService;
    
    public static SearchIndexService SearchIndexService
    {
        get
        {
            if (_searchIndexService == null)
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VetaleBrowser"
                );
                
                var dbPath = Path.Combine(appDataPath, "vetale_search.db");
                _searchIndexService = new SearchIndexService(dbPath);
            }
            return _searchIndexService;
        }
    }
}
```

### Крок 2: Індексація при відвідуванні сторінки

Коли користувач відвідує сторінку в WebView:

```csharp
// У вашому WebView обробнику
private async void OnPageLoaded(object sender, EventArgs e)
{
    try
    {
        var url = webView.CoreWebView2.Source;
        var title = await webView.CoreWebView2.ExecuteScriptAsync(
            "document.title"
        );
        
        // Витягнути текстовий контент
        var content = await webView.CoreWebView2.ExecuteScriptAsync(
            "document.body.innerText"
        );
        
        // Витягнути meta description
        var description = await webView.CoreWebView2.ExecuteScriptAsync(
            "document.querySelector('meta[name=\"description\"]')?.content || ''"
        );
        
        // Витягнути meta keywords
        var keywords = await webView.CoreWebView2.ExecuteScriptAsync(
            "document.querySelector('meta[name=\"keywords\"]')?.content || ''"
        );
        
        // Індексувати сторінку
        await App.SearchIndexService.IndexPageAsync(
            url,
            CleanJsonString(title),
            CleanJsonString(content),
            CleanJsonString(description),
            CleanJsonString(keywords)
        );
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error indexing page: {ex.Message}");
    }
}

private string CleanJsonString(string jsonString)
{
    // Видалити лапки та екрановані символи з JSON результату
    return jsonString?.Trim('"').Replace("\\n", "\n").Replace("\\\"", "\"") ?? "";
}
```

### Крок 3: Оновлення VetaleSearchHomePage

Додайте посилання на сервіс:

```csharp
public partial class VetaleSearchHomePage : UserControl
{
    private readonly ISearchIndexService _searchIndexService;
    
    public VetaleSearchHomePage()
    {
        _searchIndexService = App.SearchIndexService;
        InitializeComponent();
        InitializeControls();
    }
    
    private void PerformSearch(bool isLucky = false)
    {
        if (_searchInput == null || string.IsNullOrWhiteSpace(_searchInput.Text))
            return;

        string query = _searchInput.Text.Trim();
        int selectedEngine = _searchEngineSelector?.SelectedIndex ?? 0;

        if (selectedEngine == 0) // Vetale Search (локальний)
        {
            // Перехід на сторінку результатів
            var resultsPage = new VetaleSearchResultsPage(_searchIndexService);
            resultsPage.SetSearchQuery(query);
            
            // TODO: Ваш код навігації
            // Наприклад:
            // MainWindow.NavigateToPage(resultsPage);
            // або
            // (Parent as ContentControl).Content = resultsPage;
        }
        else
        {
            // Зовнішня пошукова система
            string[] searchUrls = new[]
            {
                "",
                $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
                $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}",
                $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}",
                $"https://yandex.com/search/?text={Uri.EscapeDataString(query)}"
            };
            
            if (selectedEngine < searchUrls.Length)
            {
                // TODO: Відкрити URL у WebView
                // webView.CoreWebView2.Navigate(searchUrls[selectedEngine]);
            }
        }
    }
}
```

### Крок 4: Оновлення VetaleSearchResultsPage

Додайте логіку пошуку:

```csharp
public partial class VetaleSearchResultsPage : UserControl
{
    private readonly ISearchIndexService _searchIndexService;
    
    // Оновлений конструктор
    public VetaleSearchResultsPage(ISearchIndexService searchIndexService)
    {
        _searchIndexService = searchIndexService;
        InitializeComponent();
        InitializeControls();
    }
    
    private async void PerformSearch()
    {
        if (_searchInput == null || string.IsNullOrWhiteSpace(_searchInput.Text))
            return;

        string query = _searchInput.Text.Trim();
        
        if (_currentEngineIndex == 0) // Vetale Search (локальний)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Виконати пошук
                var results = await _searchIndexService.SearchAsync(query, 50);
                
                stopwatch.Stop();
                
                // Оновити статистику
                if (_searchStats != null)
                {
                    _searchStats.Text = $"Знайдено {results.Count} " +
                        $"результатів ({stopwatch.Elapsed.TotalSeconds:F2} секунди)";
                }
                
                // Відобразити результати
                DisplayResults(results);
                
                // Зберегти запит в історію
                await _searchIndexService.SaveSearchQueryAsync(
                    query, 
                    "Vetale Search", 
                    results.Count
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Search error: {ex.Message}");
            }
        }
        else
        {
            // Зовнішня пошукова система
            // TODO: Відкрити у WebView
        }
    }
    
    private void DisplayResults(List<SearchIndex> results)
    {
        if (_resultsPanel == null)
            return;
            
        _resultsPanel.Children.Clear();
        
        foreach (var result in results)
        {
            var resultBorder = new Border
            {
                Classes = { "result-item" },
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            
            var stackPanel = new StackPanel { Spacing = 4 };
            
            // URL
            try
            {
                var uri = new Uri(result.Url);
                stackPanel.Children.Add(new TextBlock 
                { 
                    Classes = { "result-url" },
                    Text = $"{uri.Host} › {uri.PathAndQuery.TrimStart('/')}"
                });
            }
            catch
            {
                stackPanel.Children.Add(new TextBlock 
                { 
                    Classes = { "result-url" },
                    Text = result.Url
                });
            }
            
            // Заголовок
            var titleBlock = new TextBlock 
            { 
                Classes = { "result-title" },
                Text = string.IsNullOrEmpty(result.Title) ? 
                    "Без заголовка" : result.Title,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            titleBlock.PointerPressed += (s, e) => 
            {
                // TODO: Відкрити URL у браузері
                Debug.WriteLine($"Opening: {result.Url}");
            };
            stackPanel.Children.Add(titleBlock);
            
            // Опис
            var description = result.Description;
            if (string.IsNullOrEmpty(description))
            {
                description = result.Content.Length > 200 
                    ? result.Content.Substring(0, 200) + "..." 
                    : result.Content;
            }
            
            stackPanel.Children.Add(new TextBlock 
            { 
                Classes = { "result-description" },
                Text = description
            });
            
            resultBorder.Child = stackPanel;
            _resultsPanel.Children.Add(resultBorder);
        }
        
        // Якщо немає результатів
        if (results.Count == 0)
        {
            var noResults = new TextBlock
            {
                Text = "Нічого не знайдено. Спробуйте інший запит.",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.Parse("#5F6368")),
                Margin = new Thickness(0, 40, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _resultsPanel.Children.Add(noResults);
        }
    }
}
```

### Крок 5: Налаштування пошукової системи

У `SearchEngineSettingsPage.axaml.cs`:

```csharp
private void OnOpenVetaleSearch(object? sender, RoutedEventArgs e)
{
    // Варіант 1: Навігація у поточному вікні
    var homePage = new VetaleSearchHomePage();
    // TODO: Ваш код навігації
    
    // Варіант 2: Відкрити у новому вікні
    /*
    var window = new Window
    {
        Title = "Vetale Search",
        Width = 1200,
        Height = 800,
        Content = new VetaleSearchHomePage()
    };
    window.Show();
    */
    
    // Варіант 3: Відкрити як внутрішню сторінку браузера
    /*
    var url = "vetale://search"; // Внутрішній протокол
    webView.Navigate(url);
    */
}
```

### Крок 6: Автодоповнення (опціонально)

Додайте автодоповнення до пошукового поля:

```csharp
private async void SearchInput_TextChanged(object? sender, TextChangedEventArgs e)
{
    if (_searchInput == null || string.IsNullOrWhiteSpace(_searchInput.Text))
        return;
        
    var suggestions = await _searchIndexService.GetAutocompleteSuggestionsAsync(
        _searchInput.Text, 
        10
    );
    
    // TODO: Показати dropdown зі suggestions
    // Можна ви��ористати AutoCompleteBox або створити власний popup
}
```

## 🗂️ Структура проекту

```
VetaleBrowser/
├── VetaleBrowser.UI/
│   └── Pages/
│       ├── VetaleSearchHomePage.axaml         ✅ Створено
│       ├── VetaleSearchHomePage.axaml.cs      ✅ Створено
│       ├── VetaleSearchResultsPage.axaml      ✅ Створено
│       ├── VetaleSearchResultsPage.axaml.cs   ✅ Створено
│       └── SearchEngineSettingsPage.axaml     ✅ Оновлено
│
├── VetaleBrowser.Database/
│   ├── Models/
│   │   └── DatabaseModels.cs                  ✅ Оновлено
│   └── Services/
│       ├── ISearchIndexService.cs             ✅ Створено
│       └── SearchIndexService.cs              ✅ Створено
│
└── AppData/
    └── vetale_search.db                        🔄 Створюється автоматично
```

## ✅ Чеклист інтеграції

- [x] Створено UI сторінки (Home + Results)
- [x] Додано моделі БД (SearchIndex, SearchQuery, SearchKeyword)
- [x] Створено сервіс пошуку (SearchIndexService)
- [x] Додано кнопку в налаштування
- [ ] Ініціалізувати SearchIndexService при старті
- [ ] Додати індексацію при завантаженні сторінок
- [ ] Реалізувати навігацію між сторінками
- [ ] Підключити сервіс до UI
- [ ] Протестувати локальний пошук
- [ ] Додати обробку помилок
- [ ] (Опціонально) Додати автодоповнення
- [ ] (Опціонально) Додати історію пошуків

## 🎯 Готово до використання!

Всі необхідні файли створені. Залишилось тільки підключити сервіс до UI та реалізувати навігацію відповідно до архітектури вашого браузера.

