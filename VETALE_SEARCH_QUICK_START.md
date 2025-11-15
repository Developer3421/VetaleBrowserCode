# Vetale Search - Швидкий старт

## 📁 Створені файли

### UI Сторінки
```
VetaleBrowser.UI/Pages/
├── VetaleSearchHomePage.axaml          // Головна сторінка (розмітка)
├── VetaleSearchHomePage.axaml.cs       // Головна сторінка (код)
├── VetaleSearchResultsPage.axaml       // Сторінка результатів (розмітка)
├── VetaleSearchResultsPage.axaml.cs    // Сторінка результатів (код)
└── SearchEngineSettingsPage.axaml      // ОНОВЛЕНО: додано кнопку Vetale Search
```

### Моделі бази даних
```
VetaleBrowser.Database/Models/
└── DatabaseModels.cs                    // ОНОВЛЕНО: додано моделі для пошуку
    ├── SearchIndex                      // Індекс відвіданих сторінок
    ├── SearchQuery                      // Історія пошукових запитів
    └── SearchKeyword                    // Ключові слова для пошуку
```

### Сервіси
```
VetaleBrowser.Database/Services/
├── ISearchIndexService.cs               // Інтерфейс сервісу пошуку
└── SearchIndexService.cs                // Реалізація локального пошуку
```

## 🎨 Дизайн

### Головна сторінка
- ✅ Оранжево-фіолетовий градієнтний фон
- ✅ Великий логотип "Vetale Search"
- ✅ Пошукове поле з округлими краями
- ✅ Вибір пошукової системи (5 варіантів)
- ✅ 2 кнопки: "Пошук" та "Мені пощастить"
- ✅ Швидкі посилання внизу

### Сторінка результатів
- ✅ Компактний хедер з градієнтом
- ✅ Пошукове поле в хедері
- ✅ Вкладки: Усі, Зображення, Відео, Новини, Карти, Інструменти
- ✅ Кнопка вибору пошукової системи
- ✅ Статистика пошуку
- ✅ Картки результатів з hover ефектами
- ✅ Пагінація (10 сторінок)

## 🚀 Як використовувати

### Відкрити головну сторінку
```csharp
var searchHomePage = new VetaleSearchHomePage();
// Встановіть сторінку в ваш ContentControl або Window
```

### Відкрити результати з запитом
```csharp
var resultsPage = new VetaleSearchResultsPage();
resultsPage.SetSearchQuery("ваш пошуковий запит");
```

## 🔧 Що потрібно реалізувати (Backend)

### 1. Ініціалізація сервісу пошуку
```csharp
// При старті програми
var searchIndexService = new SearchIndexService(
    Path.Combine(AppDataPath, "vetale_search.db")
);
```

### 2. Індексація сторінок при відвідуванні
```csharp
// Коли користувач відвідує сторінку в WebView
await searchIndexService.IndexPageAsync(
    url: currentUrl,
    title: pageTitle,
    content: pageTextContent,  // Витягнути текст зі сторінки
    description: metaDescription,
    keywords: metaKeywords
);
```

### 3. Локальний пошук
```csharp
// У VetaleSearchResultsPage.cs -> PerformSearch()
private async void PerformSearch()
{
    string query = _searchInput.Text.Trim();
    
    if (_currentEngineIndex == 0) // Vetale Search
    {
        var results = await _searchIndexService.SearchAsync(query, 50);
        DisplayResults(results);
        
        // Зберегти запит в історію
        await _searchIndexService.SaveSearchQueryAsync(
            query, "Vetale Search", results.Count
        );
    }
}
```

### 4. Відображення результатів
```csharp
private void DisplayResults(List<SearchIndex> results)
{
    _resultsPanel.Children.Clear();
    
    foreach (var result in results)
    {
        var resultBorder = new Border { Classes = { "result-item" } };
        var stackPanel = new StackPanel { Spacing = 4 };
        
        stackPanel.Children.Add(new TextBlock 
        { 
            Classes = { "result-url" },
            Text = new Uri(result.Url).Host
        });
        
        stackPanel.Children.Add(new TextBlock 
        { 
            Classes = { "result-title" },
            Text = result.Title,
            Cursor = new Cursor(StandardCursorType.Hand)
        });
        
        stackPanel.Children.Add(new TextBlock 
        { 
            Classes = { "result-description" },
            Text = result.Description ?? result.Content.Substring(0, 
                Math.Min(200, result.Content.Length)) + "..."
        });
        
        resultBorder.Child = stackPanel;
        _resultsPanel.Children.Add(resultBorder);
    }
}
```

### 5. Навігація з налаштувань
```csharp
// У SearchEngineSettingsPage.cs -> OnOpenVetaleSearch()
private void OnOpenVetaleSearch(object? sender, RoutedEventArgs e)
{
    // Відкрити Vetale Search в новій вкладці
    var homePage = new VetaleSearchHomePage();
    // Ваш код навігації або відкриття вкладки
}
```

## 💾 База даних

### Структура таблиць

**SearchIndex** - Індекс відвіданих сторінок
- `Id` - Унікальний ідентифікатор
- `Url` - URL сторінки
- `Title` - Заголовок сторінки
- `Content` - Текстовий вміст (до 5000 символів)
- `Description` - Meta description
- `Keywords` - Meta keywords
- `IndexedAt` - Дата індексації
- `LastVisitedAt` - Остання дата відвідування
- `VisitCount` - Кількість відвідувань
- `RelevanceScore` - Оцінка релевантності

**SearchQuery** - Історія пошуків
- `Id` - Унікальний ідентифікатор
- `Query` - Пошуковий запит
- `SearchEngine` - Назва пошукової системи
- `SearchedAt` - Дата та час пошуку
- `ResultsCount` - Кількість знайдених результатів

**SearchKeyword** - Ключові слова
- `Id` - Унікальний ідентифікатор
- `SearchIndexId` - Посилання на SearchIndex
- `Keyword` - Ключове слово
- `Frequency` - Частота появи
- `Weight` - Вага слова (заголовки мають вагу 2.0)

### Методи API сервісу

```csharp
// Індексація
await IndexPageAsync(url, title, content, description, keywords);
await UpdateIndexAsync(url, title, content, description, keywords);
await RemoveFromIndexAsync(url);

// Пошук
var results = await SearchAsync(query, maxResults: 50);

// Історія
await SaveSearchQueryAsync(query, searchEngine, resultsCount);
var history = await GetSearchHistoryAsync(limit: 100);
await ClearSearchHistoryAsync();

// Автодоповнення
var suggestions = await GetAutocompleteSuggestionsAsync(partialQuery, 10);
var popular = await GetPopularQueriesAsync(10);

// Статистика
var stats = await GetIndexStatisticsAsync();
// Повертає: (TotalPages, TotalKeywords, LastIndexed)

// Очищення
await ClearIndexAsync();
```

## 🔗 Інтеграція з налаштуваннями

### Кнопка в SearchEngineSettingsPage

Тепер в розділі налаштувань пошукових систем є:
- ✅ Виділена секція **Vetale Search** з оранжево-фіолетовим дизайном
- ✅ Опис локальної пошукової системи
- ✅ Кнопка **"Відкрити Vetale Search"** для швидкого доступу
- ✅ Розділ "Онлайн пошукові системи" нижче

## 🚀 Як використовувати

| Назва | Індекс | Тип |
|-------|--------|-----|
| Vetale Search | 0 | Локальний |
| Google | 1 | Зовнішній |
| Bing | 2 | Зовнішній |
| DuckDuckGo | 3 | Зовнішній |
| Yandex | 4 | Зовнішній |

## 🎨 Кольори

```
Помаранчевий: #FF8A00
Фіолетовий:   #9C27B0
Фон:          #F8F9FA
Білий:        #FFFFFF
Текст:        #3C4043
Текст сірий:  #5F6368
```

## ✨ Особливості

- ✅ Без backend коду (тільки UI)
- ✅ Готово для інтеграції
- ✅ Схожість на Google дизайн
- ✅ Оранжево-фіолетова тема
- ✅ Вибір пошукової системи
- ✅ Hover ефекти
- ✅ Responsive дизайн

## 📝 Наступні кроки

1. Інтегрувати з навігацією браузера
2. Підключити локальну БД для пошуку
3. Реалізувати відкриття зовнішніх пошукових систем
4. Додати автодоповнення
5. Зберігати історію пошуків
6. Додати фільтри та сортування

---

✅ **Готово до використання!** Файли створені та стилізовані.

