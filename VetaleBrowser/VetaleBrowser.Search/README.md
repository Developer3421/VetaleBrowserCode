# Система пошукових підказок Vetale Search

## Огляд

Система автопідказок для Vetale Search, яка використовує Google Suggest API для надання релевантних пошукових запитів у реальному часі.

## Структура проєкту

```
VetaleBrowser.Search/
├── Models/
│   └── SearchSuggestion.cs        # Модель підказки
└── Services/
    ├── ISuggestionsService.cs     # Інтерфейс сервісу
    └── GoogleSuggestionsService.cs # Реалізація через Google API
```

## Компоненти

### 1. SearchSuggestion (Models/SearchSuggestion.cs)

Модель даних для підказки:

```csharp
public class SearchSuggestion
{
    public string Text { get; set; }      // Текст підказки
    public string Type { get; set; }      // Тип: "search", "history", "bookmark"
    public string Icon { get; set; }      // Іконка (емодзі)
    public string? Url { get; set; }      // Опціональний URL
}
```

### 2. ISuggestionsService (Services/ISuggestionsService.cs)

Інтерфейс для сервісів підказок:

```csharp
Task<List<SearchSuggestion>> GetSuggestionsAsync(string query, int maxResults = 8);
```

### 3. GoogleSuggestionsService (Services/GoogleSuggestionsService.cs)

Реалізація через Google Suggest API:

- **Endpoint**: `http://suggestqueries.google.com/complete/search?client=firefox&q={query}`
- **Формат відповіді**: JSON масив `[query, [suggestions...]]`
- **Timeout**: 3 секунди
- **Максимум результатів**: 8 (налаштовується)

## Використання у VetaleSearchResultsPage

### 1. Ініціалізація

```csharp
private readonly ISuggestionsService _suggestionsService;

public VetaleSearchResultsPage()
{
    _suggestionsService = new GoogleSuggestionsService();
    InitializeComponent();
}
```

### 2. Завантаження підказок

При зміні тексту у `SearchInput`:

```csharp
private async Task LoadSuggestionsAsync()
{
    // Дебаунс 300мс
    await Task.Delay(300, token);
    
    var query = _searchInput?.Text ?? string.Empty;
    
    if (query.Length >= 2)
    {
        var suggestions = await _suggestionsService.GetSuggestionsAsync(query, 8);
        _suggestionsListBox.ItemsSource = suggestions;
        _suggestionsPopup.IsOpen = true;
    }
}
```

### 3. UI компоненти (XAML)

**Popup з підказками:**

```xml
<Popup x:Name="SuggestionsPopup"
       Placement="Bottom"
       PlacementTarget="{Binding #SearchInput}"
       IsLightDismissEnabled="True">
    <Border Classes="suggestion-popup">
        <Border Classes="suggestion-popup-inner">
            <ScrollViewer MaxHeight="400">
                <ItemsControl x:Name="SuggestionsListBox">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Classes="suggestion-item"
                                    PointerPressed="SuggestionItem_Click">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Classes="suggestion-icon" Text="{Binding Icon}"/>
                                    <TextBlock Classes="suggestion-text" Text="{Binding Text}"/>
                                </StackPanel>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>
        </Border>
    </Border>
</Popup>
```

## Стилі

Підказки використовують ту ж кольорову схему, що й результати пошуку:

- **Фон**: Beige
- **Рамка**: Градієнт помаранчевий → фіолетовий (`#FFAA00` → `#B040C0`)
- **Тінь**: Помаранчева `#30FF8A00`
- **Текст**: Темно-коричневий `#3E2723`
- **Hover ефект**: Збільшення рамки і тіні

### Класи стилів:

- `.suggestion-popup` - зовнішня рамка (біла)
- `.suggestion-popup-inner` - внутрішня картка (бежева)
- `.suggestion-item` - окрема підказка
- `.suggestion-icon` - іконка (емодзі)
- `.suggestion-text` - текст підказки

## Особливості

### Дебаунс

Запити до API відправляються з затримкою 300мс після останнього введення символу, щоб зменшити кількість запитів.

### Скасування

Кожен новий запит скасовує попередній через `CancellationTokenSource`.

### Мінімальна довжина

Підказки показуються тільки при довжині запиту >= 2 символи.

### Light Dismiss

Popup автоматично закривається при кліку поза ним (`IsLightDismissEnabled="True"`).

## Розширення

### Додавання нових джерел підказок

Можна створити додаткові сервіси:

```csharp
public class BingSuggestionsService : ISuggestionsService
{
    public async Task<List<SearchSuggestion>> GetSuggestionsAsync(string query, int maxResults = 8)
    {
        // Реалізація через Bing API
    }
}

public class LocalHistorySuggestionsService : ISuggestionsService
{
    public async Task<List<SearchSuggestion>> GetSuggestionsAsync(string query, int maxResults = 8)
    {
        // Підказки з локальної історії
    }
}
```

### Комбінований сервіс

```csharp
public class CombinedSuggestionsService : ISuggestionsService
{
    private readonly List<ISuggestionsService> _services;

    public async Task<List<SearchSuggestion>> GetSuggestionsAsync(string query, int maxResults = 8)
    {
        var tasks = _services.Select(s => s.GetSuggestionsAsync(query, maxResults/2));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).Take(maxResults).ToList();
    }
}
```

## Тестування

1. Запустіть браузер
2. Відкрийте сторінку результатів Vetale Search
3. Почніть вводити текст у поле пошуку
4. Через 300мс з'явиться випадаючий список підказок
5. Клікніть на підказку для автозаповнення

## Діагностика

Логи в Debug Console:

```
[GoogleSuggestionsService] Got 8 suggestions for 'test'
[VetaleSearchResultsPage] LoadSuggestionsAsync error: ...
```

## Продуктивність

- **HTTP timeout**: 3 секунди
- **Дебаунс**: 300мс
- **Кешування**: Немає (можна додати)
- **Максимум результатів**: 8 підказок

## Безпека

- Використовується HTTP (не HTTPS) endpoint Google
- Немає передачі персональних даних
- Запити можна легко відстежити у Network Monitor

## Майбутні покращення

- [ ] Кешування підказок
- [ ] Інтеграція з локальною історією
- [ ] Інтеграція із закладками
- [ ] Підтримка множини мов
- [ ] Налаштування провайдера підказок
- [ ] Offline режим з локальними підказками

