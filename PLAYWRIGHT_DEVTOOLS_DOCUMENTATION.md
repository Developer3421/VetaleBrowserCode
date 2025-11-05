# Playwright DevTools - Документація

## Огляд

Playwright DevTools - це повнофункціональний інструмент для розробників, що використовує **Microsoft Playwright** для інспекції веб-сторінок. Він надає чотири основні модулі:

1. **Elements** - Інспекція DOM дерева
2. **Performance** - Аналіз продуктивності сторінки
3. **Application** - Управління Storage (localStorage, sessionStorage, cookies, IndexedDB)
4. **Sources** - Перегляд ресурсів сторінки (scripts, stylesheets, images)

## Архітектура

### Основні компоненти

```
VetaleBrowser.DevTools/
├── Services/
│   └── PlaywrightDevToolsService.cs    # Головний сервіс для роботи з Playwright
└── Pages/
    ├── PlaywrightDevToolsMainPage      # Головна сторінка з табами
    ├── PlaywrightElementsPage          # Elements інспектор
    ├── PlaywrightPerformancePage       # Performance аналізатор
    ├── PlaywrightApplicationPage       # Application storage
    └── PlaywrightSourcesPage           # Sources viewer
```

## PlaywrightDevToolsService

### Ініціалізація

```csharp
var dataService = new DevToolsDataService();
var playwrightService = new PlaywrightDevToolsService(dataService);

// Ініціалізація браузера
await playwrightService.InitializeAsync();

// Навігація до URL
await playwrightService.NavigateAsync("https://example.com");
```

### 1. Elements - DOM Інспекція

#### Захоплення DOM структури

```csharp
var domElements = await playwrightService.CaptureDomStructureAsync();

// Результат: List<DomElement>
foreach (var element in domElements)
{
    Console.WriteLine($"<{element.TagName}> - {element.ElementPath}");
    Console.WriteLine($"Attributes: {element.Attributes}");
    Console.WriteLine($"Computed Styles: {element.ComputedStyles}");
}
```

#### Отримання деталей конкретного елемента

```csharp
var element = await playwrightService.GetElementDetailsAsync("button.submit");

// Отримуємо всі стилі та атрибути конкретного елемента
```

#### CSS Selector пошук

```csharp
var buttons = await playwrightService.QuerySelectorAllAsync("button");
var inputs = await playwrightService.QuerySelectorAllAsync("input[type='text']");
```

**Функції:**
- ✅ Повне DOM дерево з глибиною до 15 рівнів
- ✅ Computed styles для кожного елемента
- ✅ Attributes (id, class, data-*, etc.)
- ✅ Bounding rectangles (позиція та розміри)
- ✅ CSS селектор пошук
- ✅ Ієрархічне відображення

### 2. Performance - Аналіз Продуктивності

#### Захоплення метрик продуктивності

```csharp
var snapshot = await playwrightService.CapturePerformanceSnapshotAsync();

Console.WriteLine($"Load Time: {snapshot.LoadTime} ms");
Console.WriteLine($"DOM Content Loaded: {snapshot.DomContentLoadedTime} ms");
Console.WriteLine($"First Paint: {snapshot.FirstPaintTime} ms");
Console.WriteLine($"Memory Used: {snapshot.MemoryUsed / (1024 * 1024)} MB");
```

#### Core Web Vitals

```csharp
var webVitals = await playwrightService.GetCoreWebVitalsAsync();

// LCP - Largest Contentful Paint (< 2.5s - good, < 4s - needs improvement, > 4s - poor)
double lcp = webVitals["LCP"];

// FID - First Input Delay (< 100ms - good, < 300ms - needs improvement, > 300ms - poor)
double fid = webVitals["FID"];

// CLS - Cumulative Layout Shift (< 0.1 - good, < 0.25 - needs improvement, > 0.25 - poor)
double cls = webVitals["CLS"];
```

**Метрики:**
- ✅ Navigation Timing API
  - Load Time
  - DOM Content Loaded Time
  - Response End Time
  - DOM Interactive Time
- ✅ Paint Timing API
  - First Paint
  - First Contentful Paint
- ✅ Core Web Vitals
  - LCP (Largest Contentful Paint)
  - FID (First Input Delay)
  - CLS (Cumulative Layout Shift)
- ✅ Memory Usage (JS Heap)
- ✅ Resource Timings (детальна інформація про кожен ресурс)

### 3. Application - Storage Management

#### Захоплення всіх типів Storage

```csharp
var storageItems = await playwrightService.CaptureStorageAsync();

// Групування по типу
var localStorage = storageItems.Where(x => x.StorageType == "localStorage");
var sessionStorage = storageItems.Where(x => x.StorageType == "sessionStorage");
var cookies = storageItems.Where(x => x.StorageType == "cookies");
var indexedDB = storageItems.Where(x => x.StorageType == "indexedDB");
```

#### Управління LocalStorage

```csharp
// Встановити значення
await playwrightService.SetLocalStorageAsync("myKey", "myValue");

// Видалити ключ
await playwrightService.RemoveLocalStorageAsync("myKey");
```

#### Управління Cookies

```csharp
// Очистити всі cookies
await playwrightService.ClearCookiesAsync();
```

**Підтримувані типи Storage:**
- ✅ **localStorage** - постійне зберігання в браузері
- ✅ **sessionStorage** - зберігання для поточної сесії
- ✅ **Cookies** - з підтримкою Domain та ExpiresAt
- ✅ **IndexedDB** - список баз даних з версіями

### 4. Sources - Ресурси Сторінки

#### Захоплення всіх ресурсів

```csharp
var resources = await playwrightService.CapturePageResourcesAsync();

foreach (var resource in resources)
{
    Console.WriteLine($"Type: {resource.Type}");
    Console.WriteLine($"URL: {resource.Url}");
    Console.WriteLine($"Content Length: {resource.EncryptedContent?.Length ?? 0}");
}
```

#### Отримання HTML сторінки

```csharp
var html = await playwrightService.GetPageHtmlAsync();
```

#### Виконання JavaScript

```csharp
var result = await playwrightService.ExecuteScriptAsync(@"
    document.querySelector('h1').textContent
");
```

**Типи ресурсів:**
- ✅ **Scripts** (external і inline)
- ✅ **Stylesheets** (external)
- ✅ **Inline Styles**
- ✅ **Images**
- ✅ Автоматичне завантаження вмісту external ресурсів
- ✅ JavaScript executor

### 5. Network Monitoring

#### Відстеження мережевих запитів

```csharp
playwrightService.EnableNetworkMonitoring((method, url, status) =>
{
    Console.WriteLine($"{method} {url} - Status: {status}");
});
```

## Події (Events)

```csharp
// Навігація завершена
playwrightService.NavigationCompleted += (sender, url) =>
{
    Console.WriteLine($"Navigated to: {url}");
};

// DOM елемент захоплений
playwrightService.DomElementCaptured += (sender, element) =>
{
    Console.WriteLine($"Captured: <{element.TagName}>");
};

// Performance snapshot захоплений
playwrightService.PerformanceSnapshotCaptured += (sender, snapshot) =>
{
    Console.WriteLine($"Performance: {snapshot.LoadTime}ms");
};

// Resource захоплений
playwrightService.ResourceCaptured += (sender, resource) =>
{
    Console.WriteLine($"Resource: {resource.Type} - {resource.Url}");
};

// Storage item захоплений
playwrightService.StorageItemCaptured += (sender, item) =>
{
    Console.WriteLine($"Storage: {item.StorageType} - {item.Key}");
};
```

## Приклади використання

### Повний приклад аналізу сторінки

```csharp
using VetaleBrowser.VetaleBrowser.DevTools.Services;
using VetaleBrowser.VetaleBrowser.Database.Services;

// Ініціалізація
var dataService = new DevToolsDataService();
var playwright = new PlaywrightDevToolsService(dataService);

await playwright.InitializeAsync();
await playwright.NavigateAsync("https://example.com");

// 1. Аналіз DOM
var dom = await playwright.CaptureDomStructureAsync();
Console.WriteLine($"DOM Elements: {dom.Count}");

// 2. Аналіз продуктивності
var perf = await playwright.CapturePerformanceSnapshotAsync();
Console.WriteLine($"Load Time: {perf.LoadTime}ms");

var vitals = await playwright.GetCoreWebVitalsAsync();
Console.WriteLine($"LCP: {vitals["LCP"]}ms");

// 3. Аналіз Storage
var storage = await playwright.CaptureStorageAsync();
Console.WriteLine($"Storage Items: {storage.Count}");

// 4. Аналіз ресурсів
var resources = await playwright.CapturePageResourcesAsync();
Console.WriteLine($"Resources: {resources.Count}");

// Cleanup
playwright.Dispose();
```

### Приклад з UI (Avalonia)

```csharp
public class MyDevToolsPage : UserControl
{
    private readonly PlaywrightDevToolsService _playwright;

    public MyDevToolsPage()
    {
        _playwright = new PlaywrightDevToolsService(new DevToolsDataService());
        _playwright.DomElementCaptured += OnDomElementCaptured;
    }

    private async void OnCaptureDom(object? sender, RoutedEventArgs e)
    {
        var elements = await _playwright.CaptureDomStructureAsync();
        
        // Відобразити в TreeView
        foreach (var element in elements)
        {
            var item = new TreeViewItem
            {
                Header = $"<{element.TagName}>",
                Tag = element
            };
            DomTree.Items.Add(item);
        }
    }

    private void OnDomElementCaptured(object? sender, DomElement element)
    {
        // Обробка події захоплення елемента
        Debug.WriteLine($"Captured: <{element.TagName}>");
    }
}
```

## UI Компоненти

### PlaywrightDevToolsMainPage

Головна сторінка з чотирма табами:
- Elements
- Performance
- Application
- Sources

Використання:
```xml
<UserControl xmlns:pages="using:VetaleBrowser.VetaleBrowser.DevTools.Pages">
    <pages:PlaywrightDevToolsMainPage />
</UserControl>
```

### Окремі сторінки

Якщо потрібен лише один модуль:

```xml
<!-- Тільки Elements -->
<pages:PlaywrightElementsPage />

<!-- Тільки Performance -->
<pages:PlaywrightPerformancePage />

<!-- Тільки Application -->
<pages:PlaywrightApplicationPage />

<!-- Тільки Sources -->
<pages:PlaywrightSourcesPage />
```

## Налаштування

### Playwright Browser Options

Налаштування браузера можна змінити в `PlaywrightDevToolsService.InitializeAsync()`:

```csharp
_browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,  // true для headless режиму
    Args = new[] { "--disable-blink-features=AutomationControlled" },
    SlowMo = 0,  // Затримка між діями (для debugging)
    Timeout = 30000  // Timeout для запуску браузера
});
```

### Viewport Size

```csharp
var context = await _browser.NewContextAsync(new BrowserNewContextOptions
{
    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
    UserAgent = "Custom User Agent"
});
```

## Database Storage

Всі дані зберігаються в LiteDB через `DevToolsDataService`:

- **DomElement** → `devtools_dom_elements` collection
- **PerformanceSnapshot** → `devtools_performance_snapshots` collection
- **PageResource** → `devtools_resources` collection
- **StorageItem** → `devtools_storage_items` collection

Приклад запиту:
```csharp
var dataService = new DevToolsDataService();
var elements = await dataService.GetDomElementsAsync(sessionId);
```

## Обмеження та Міркування

1. **DOM Глибина**: Максимально 15 рівнів (configurable в JavaScript коді)
2. **DOM Елементи**: Максимально 100 дочірніх елементів per parent
3. **Resource Content**: Обмежено 50KB per resource (з truncation)
4. **Text Content**: Обмежено 200 символів в DOM elements
5. **Network**: Playwright автоматично відстежує всі запити

## Performance Tips

1. Використовуйте `WaitUntilState.NetworkIdle` для повного завантаження
2. Захоплюйте DOM після `DOMContentLoaded` події
3. Використовуйте CSS селектори замість XPath для швидкості
4. Очищайте старі дані з database періодично
5. Закривайте браузер через `Dispose()` після використання

## Troubleshooting

### Браузер не запускається

Переконайтеся що Playwright browsers встановлені:
```bash
pwsh bin/Debug/net9.0/playwright.ps1 install
```

### Timeout при навігації

Збільште timeout:
```csharp
await _page.GotoAsync(url, new PageGotoOptions
{
    WaitUntil = WaitUntilState.Load,  // Замість NetworkIdle
    Timeout = 60000  // 60 секунд
});
```

### JavaScript execution failed

Переконайтеся що сторінка повністю завантажена перед виконанням скриптів.

## Ліцензія

Цей компонент є частиною VetaleBrowser проекту.

## Автор

Розроблено з використанням Microsoft Playwright для VetaleBrowser.

