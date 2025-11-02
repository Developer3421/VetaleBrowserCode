# Налаштування пошукової системи - Документація

## Огляд

Реалізовано повнофункціональну сторінку налаштувань для вибору пошукової системи у Vetale Browser з підтримкою AES шифрування для безпечного зберігання налаштувань.

## Архітектура

### 1. Сервіси бази даних

#### `ISettingsService.cs`
Інтерфейс для роботи з налаштуваннями браузера:
- `GetSearchEngineUrlAsync()` - отримання URL пошукової системи
- `SetSearchEngineUrlAsync(string url)` - встановлення URL
- `GetSearchEngineNameAsync()` - отримання назви пошукової системи
- `SetSearchEngineNameAsync(string name)` - встановлення назви
- `SetSearchEngineAsync(string name, string url)` - встановлення обох параметрів

#### `SettingsService.cs`
Реалізація сервісу з використанням:
- **LiteDB** для зберігання даних
- **AES шифрування** через `DatabaseEncryptionService`
- **Асинхронні операції** для всіх методів
- Індекси для оптимізації пошуку

**За замовчуванням:**
- Пошукова система: Google
- URL: `https://www.google.com/search?q={0}`

### 2. Моделі даних

#### `SettingItem` (в DatabaseModels.cs)
```csharp
public class SettingItem
{
    public int Id { get; set; }
    public string Key { get; set; }           // Ключ налаштування
    public string EncryptedValue { get; set; } // Зашифроване значення
    public DateTime UpdatedAt { get; set; }    // Дата оновлення
}
```

### 3. UI компоненти

#### `SearchEngineSettingsPage.axaml`
Сторінка з такими елементами:
- **Кнопка "Назад"** для повернення до головної сторінки налаштувань
- **RadioButton** для кожної пошукової системи:
  - Google
  - Bing
  - Yahoo
  - Baidu
  - Власний вибір (з TextBox для кастомного URL)
- **Кнопка "Зберегти"** для збереження налаштувань

#### `SearchEngineSettingsPage.axaml.cs`
Логіка сторінки:
- Завантаження поточних налаштувань при відкритті
- Валідація кастомного URL (перевірка на наявність `{0}`)
- Збереження вибору в зашифрованому вигляді
- Події: `BackRequested`, `SettingsSaved`

#### `SettingsWindow.axaml.cs`
Оновлено для:
- Ініціалізації `SettingsService`
- Завантаження `SearchEngineSettingsPage` при виборі відповідного пункту
- Зміни розміру вікна під сторінку (660x600)

## Доступні пошукові системи

| Назва | URL шаблон |
|-------|-----------|
| **Google** | `https://www.google.com/search?q={0}` |
| **Bing** | `https://www.bing.com/search?q={0}` |
| **Yahoo** | `https://search.yahoo.com/search?p={0}` |
| **Baidu** | `https://www.baidu.com/s?wd={0}` |
| **Власний вибір** | Користувач вводить URL з `{0}` як місце для запиту |

## Безпека

### AES Шифрування
Всі налаштування зберігаються в зашифрованому вигляді:
- Використовується `DatabaseEncryptionService`
- Ключ генерується на основі машини та користувача
- Шифрування: AES-256, CBC режим, PKCS7 padding

## Інтеграція з пошуком

### Автоматичне використання вибраної пошукової системи

Після збереження налаштувань, всі пошукові запити в адресній строці автоматично використовують вибрану пошукову систему.

#### NavigationBar інтеграція:
```csharp
// В MainWindow.axaml.cs
var navigationBar = _normalModePage?.NavBar;
if (navigationBar != null && _tabs.Active != null)
{
    navigationBar.Initialize(_tabs.Active.Manager);
    
    // Встановлюємо SettingsService для доступу до налаштувань
    if (_settingsService != null)
    {
        navigationBar.SetSettingsService(_settingsService);
    }
}
```

#### Як це працює:
1. Користувач вводить текст в адресну строку
2. При натисканні Enter перевіряється чи це URL чи пошуковий запит
3. Якщо це пошуковий запит (без "://" і без "." або з пробілами):
   - Витягується URL пошукової системи з `SettingsService`
   - Запит підставляється в шаблон `{0}`
   - Виконується навігація

**Приклад:**
- Користувач вводить: `котики`
- Якщо вибрано Google: навігація на `https://www.google.com/search?q=котики`
- Якщо вибрано Bing: навігація на `https://www.bing.com/search?q=котики`
- Якщо власний URL `https://duckduckgo.com/?q={0}`: навігація на `https://duckduckgo.com/?q=котики`

### Код інтеграції в NavigationBar

```csharp
private async void OnAddressBarKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
{
    if (e.Key == Avalonia.Input.Key.Enter && _addressBar != null && _webViewManager != null)
    {
        var url = _addressBar.Text ?? "";
        if (string.IsNullOrWhiteSpace(url))
            return;

        // Check if it's a URL or search query
        if (!url.Contains("://"))
        {
            if (url.Contains(".") && !url.Contains(" "))
            {
                // Looks like a domain
                url = "https://" + url;
            }
            else
            {
                // Search query - use saved search engine
                var searchUrl = await GetSearchEngineUrlAsync();
                url = string.Format(searchUrl, Uri.EscapeDataString(url));
            }
        }

        await _webViewManager.NavigateAsync(url);
    }
}

private async System.Threading.Tasks.Task<string> GetSearchEngineUrlAsync()
{
    if (_settingsService != null)
    {
        try
        {
            var searchUrl = await _settingsService.GetSearchEngineUrlAsync();
            return searchUrl;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting search engine: {ex}");
        }
    }
    
    // Fallback to Google if settings not available
    return "https://www.google.com/search?q={0}";
}
```

## Використання

### 1. Відкриття налаштувань
Користувач натискає кнопку "Налаштування" в навігаційній панелі → відкривається `SettingsWindow` → вибирає "Search Engine" → відкривається `SearchEngineSettingsPage`.

### 2. Вибір пошукової системи
- Натиснути на потрібну радіо-кнопку
- Для власного вибору: ввести URL з `{0}` (наприклад: `https://duckduckgo.com/?q={0}`)
- Натиснути "Зберегти"

### 3. Інтеграція з пошуком
Для використання збережених налаштувань в адресній строці потрібно:
```csharp
var settingsService = new SettingsService(config.DatabasePath, config.EncryptionKey);
var searchUrl = await settingsService.GetSearchEngineUrlAsync();
var query = "мій запит";
var finalUrl = string.Format(searchUrl, Uri.EscapeDataString(query));
```

## Структура файлів

```
VetaleBrowser/
├── VetaleBrowser.Database/
│   ├── Services/
│   │   ├── ISettingsService.cs          ← Інтерфейс
│   │   ├── SettingsService.cs           ← Реалізація з AES
│   │   └── DatabaseEncryptionService.cs  (існуючий)
│   ├── Models/
│   │   └── DatabaseModels.cs            ← SettingItem модель
│   └── DatabaseConfiguration.cs          (існуючий)
└── VetaleBrowser.UI/
    ├── Pages/
    │   ├── SearchEngineSettingsPage.axaml    ← UI
    │   └── SearchEngineSettingsPage.axaml.cs ← Логіка
    └── Windows/
        └── SettingsWindow.axaml.cs           ← Оновлено

```

## Можливості розширення

### Додавання нової пошукової системи
1. Додати RadioButton в XAML
2. Додати запис в словник `_searchEngines`
3. Додати case в методі `OnSaveClick`

### VetaleSearch (майбутня інтеграція)
Коли локальна пошукова система VetaleSearch буде готова:
1. Додати RadioButton для VetaleSearch
2. Використовувати локальний URL (наприклад: `http://localhost:8080/search?q={0}`)
3. Можливо, додати перевірку доступності сервера

## Примітки

- Всі налаштування зберігаються в `browser.db` в папці `%AppData%/VetaleBrowser/Data/`
- База даних автоматично створюється при першому запуску
- Шифрування забезпечує безпеку навіть якщо файл БД буде скопійовано
- UI адаптується під різні розміри екрану завдяки ScrollViewer

## Тестування

Для тестування:
1. Запустити Vetale Browser
2. Відкрити Налаштування
3. Вибрати "Search Engine"
4. Змінити пошукову систему
5. Натиснути "Зберегти"
6. Перезапустити браузер і перевірити, чи зберігся вибір

