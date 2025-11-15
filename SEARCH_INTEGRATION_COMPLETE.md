# Інтеграція пошуку: Vetale Search Home → Results

## Що реалізовано

Повна інтеграція переходу з головної сторінки Vetale Search на сторінку результатів при натисканні Enter або кнопки "Пошук".

## Схема роботи

```
VetaleSearchHomePage
    ↓ (користувач вводить "web development" і натискає Enter/кнопку)
    ↓ PerformSearch()
    ↓ генерує URL: "vetale://search/results?q=web+development"
    ↓ викликає NavigateRequested event
    ↓
MainWindow.OnInternalPageNavigateRequested()
    ↓ перевіряє що це internal URL
    ↓ викликає HandleInternalNavigation()
    ↓
InternalUrlHandler.CreatePageContent()
    ↓ розпізнає тип: VetaleSearchResults
    ↓ викликає CreateSearchResultsPage(url)
    ↓ витягує query параметр "q"
    ↓ створює VetaleSearchResultsPage
    ↓ викликає page.SetSearchQuery("web development")
    ↓
VetaleSearchResultsPage
    ✓ відображається з результатами пошуку
```

## Зміни у файлах

### 1. InternalUrlHandler.cs

#### Додано метод `CreateSearchResultsPage`:
```csharp
private static VetaleSearchResultsPage CreateSearchResultsPage(string url)
{
    var page = new VetaleSearchResultsPage();
    
    // Витягуємо query параметр з URL
    var query = GetQueryParameter(url, "q");
    if (!string.IsNullOrEmpty(query))
    {
        page.SetSearchQuery(query);
    }
    
    return page;
}
```

#### Додано метод `GetQueryParameter`:
```csharp
public static string? GetQueryParameter(string url, string parameterName)
{
    try
    {
        var queryStart = url.IndexOf('?');
        if (queryStart < 0)
            return null;
            
        var query = url.Substring(queryStart + 1);
        var pairs = query.Split('&');
        
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            if (parts.Length == 2 && parts[0] == parameterName)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }
        
        return null;
    }
    catch
    {
        return null;
    }
}
```

#### Оновлено `CreatePageContent`:
```csharp
return pageType switch
{
    InternalPageType.VetaleSearch => new VetaleSearchHomePage(),
    InternalPageType.VetaleSearchResults => CreateSearchResultsPage(url), // ← змінено
    InternalPageType.Bookmarks => new BookmarksPage(),
    // ...
};
```

### 2. MainWindow.axaml.cs

#### Додано підписку на NavigateRequested для VetaleSearchResultsPage:

**В методі `HandleInternalNavigation`:**
```csharp
// Підписуємося на події навігації від внутрішніх сторінок
if (content is VetaleSearchHomePage searchHomePage)
{
    searchHomePage.NavigateRequested += OnInternalPageNavigateRequested;
}
else if (content is VetaleSearchResultsPage resultsPage)
{
    resultsPage.NavigateRequested += OnInternalPageNavigateRequested;
}
```

**В методі `CreateInternalPageTab`:**
```csharp
// Підписуємося на події навігації від внутрішніх сторінок
if (pageContent is VetaleSearchHomePage searchHomePage)
{
    searchHomePage.NavigateRequested += OnInternalPageNavigateRequested;
}
else if (pageContent is VetaleSearchResultsPage resultsPage)
{
    resultsPage.NavigateRequested += OnInternalPageNavigateRequested;
}
```

### 3. VetaleSearchHomePage.axaml.cs

**Логіка вже була реалізована**, але для довідки:

```csharp
private void PerformSearch(bool isLucky = false)
{
    if (_searchInput == null || string.IsNullOrWhiteSpace(_searchInput.Text))
        return;

    string query = _searchInput.Text.Trim();
    int selectedEngine = _searchEngineSelector?.SelectedIndex ?? 0;

    if (selectedEngine == 0) // Vetale Search (локальний)
    {
        var resultsUrl = $"vetale://search/results?q={Uri.EscapeDataString(query)}";
        NavigateRequested?.Invoke(this, resultsUrl);
        return;
    }

    // Інші пошукові системи...
}
```

## Як це працює

### Крок 1: Користувач вводить запит
На сторінці **VetaleSearchHomePage**:
- Користувач вводить текст, наприклад: "web development"
- Натискає **Enter** або кнопку **"Пошук"**

### Крок 2: Генерація внутрішнього URL
`VetaleSearchHomePage.PerformSearch()`:
- Перевіряє який движок обрано
- Якщо **Vetale Search** (index 0):
  - Генерує: `vetale://search/results?q=web+development`
  - Викликає подію: `NavigateRequested`

### Крок 3: MainWindow обробляє навігацію
`MainWindow.OnInternalPageNavigateRequested()`:
- Отримує URL: `vetale://search/results?q=web+development`
- Перевіряє: `IsInternalUrl()` → true
- Викликає: `HandleInternalNavigation(url)`

### Крок 4: Створення сторінки результатів
`InternalUrlHandler.CreatePageContent()`:
- Розпізнає тип: `InternalPageType.VetaleSearchResults`
- Викликає: `CreateSearchResultsPage(url)`
  - Витягує: `q = "web development"`
  - Створює: `new VetaleSearchResultsPage()`
  - Викликає: `page.SetSearchQuery("web development")`

### Крок 5: Відображення результатів
`VetaleSearchResultsPage`:
- Отримує запит через `SetSearchQuery()`
- Відображає статичні результати (або виконує реальний пошук)
- Оновлює статистику: "Приблизно X результатів (Y секунди)"

## Тестування

### Сценарій 1: Пошук з головної сторінки
1. Відкрити браузер
2. Встановити **Vetale Search** як пошукову систему (Settings → Search Engine)
3. На головній сторінці ввести: "test query"
4. Натиснути **Enter** або кнопку "Пошук"
5. **Очікуваний результат:**
   - Поточна вкладка оновлюється на `VetaleSearchResultsPage`
   - URL: `vetale://search/results?q=test+query`
   - Відображаються результати пошуку
   - Статистика: "Приблизно 1 234 567 результатів (0.45 секунди)"

### Сценарій 2: Пошук з порожнім полем
1. На головній сторінці залишити поле пустим
2. Натиснути "Пошук"
3. **Очікуваний результат:**
   - Нічого не відбувається (валідація в `PerformSearch`)

### Сценарій 3: Перехід на веб-пошукові системи
1. На головній сторінці вибрати **Google** у селекторі
2. Ввести запит: "test"
3. Натиснути "Пошук"
4. **Очікуваний результат:**
   - Перехід на: `https://www.google.com/search?q=test`

## Логи для відстеження

При Debug режимі в Output Window з'являються логи:

```
[MainWindow] Internal page requested navigation to: vetale://search/results?q=web+development
[MainWindow] Active tab found: Address=vetale://search
[MainWindow] Created new VetaleSearchResultsPage instance
[MainWindow] Subscribed to NavigateRequested event
[VetaleSearchResultsPage] Loading results for: web development
```

## Наступні кроки

### 1. Інтеграція з реальним пошуковим движком
```csharp
// У VetaleSearchResultsPage.LoadSearchResults():
var results = await VetaleSearchEngine.SearchAsync(query);

ClearResults();
UpdateSearchStats(results.TotalCount, results.SearchTime);

foreach (var item in results.Items)
{
    AddSearchResult(item);
}
```

### 2. Підтримка фільтрів
Додати параметри до URL:
- `vetale://search/results?q=test&type=images` - пошук зображень
- `vetale://search/results?q=test&date=week` - результати за тиждень

### 3. Історія пошуків
Зберігати всі пошукові запити в БД для автодоповнення.

### 4. Автодоповнення
При введенні тексту показувати підказки з попередніх пошуків.

## Підсумок

✅ **Повний цикл пошуку реалізовано:**
- Введення запиту на головній сторінці
- Генерація внутрішнього URL з query параметром
- Навігація на сторінку результатів
- Автоматичне витягування і відображення запиту
- Підтримка кількох пошукових систем (Vetale Search, Google, Bing, DuckDuckGo, Yandex)

Тепер можна тестувати повний flow пошуку! 🎯

