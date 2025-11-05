# 🚀 Playwright DevTools для VetaleBrowser

## 📋 Огляд

Повнофункціональний інструмент розробника на базі **Microsoft Playwright** з підтримкою:
- ✅ **Elements** - DOM інспекція
- ✅ **Performance** - метрики продуктивності та Core Web Vitals
- ✅ **Application** - управління Storage (localStorage, cookies, etc.)
- ✅ **Sources** - перегляд ресурсів та JavaScript executor

---

## 🎯 Швидкий старт

```csharp
var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
await playwright.InitializeAsync();
await playwright.NavigateAsync("https://example.com");

// Захопити дані
var dom = await playwright.CaptureDomStructureAsync();
var perf = await playwright.CapturePerformanceSnapshotAsync();
var vitals = await playwright.GetCoreWebVitalsAsync();
var storage = await playwright.CaptureStorageAsync();
var resources = await playwright.CapturePageResourcesAsync();
```

---

## 📁 Структура файлів

### Код
```
VetaleBrowser.DevTools/
├── Services/
│   └── PlaywrightDevToolsService.cs       # Головний сервіс
└── Pages/
    ├── PlaywrightDevToolsMainPage         # Головна сторінка
    ├── PlaywrightElementsPage             # Elements
    ├── PlaywrightPerformancePage          # Performance
    ├── PlaywrightApplicationPage          # Application
    └── PlaywrightSourcesPage              # Sources
```

### Документація
```
E:\VetaleBrowser/
├── PLAYWRIGHT_DEVTOOLS_DOCUMENTATION.md           # 📚 Повна документація
├── PLAYWRIGHT_DEVTOOLS_QUICK_START.md             # ⚡ Швидкий старт
├── PLAYWRIGHT_DEVTOOLS_IMPLEMENTATION_SUMMARY.md  # 📊 Підсумок
├── PLAYWRIGHT_DEVTOOLS_EXAMPLES.md                # 💡 10 прикладів
└── PLAYWRIGHT_DEVTOOLS_INDEX.md                   # 📖 Цей файл
```

---

## 📚 Документація

### 1. [Повна документація](PLAYWRIGHT_DEVTOOLS_DOCUMENTATION.md)
Детальний API reference, всі методи, приклади, технічні деталі.

### 2. [Швидкий старт](PLAYWRIGHT_DEVTOOLS_QUICK_START.md)
Базове використання, мінімальний код для початку роботи.

### 3. [Підсумок імплементації](PLAYWRIGHT_DEVTOOLS_IMPLEMENTATION_SUMMARY.md)
Що реалізовано, статистика, виправлені помилки.

### 4. [Приклади використання](PLAYWRIGHT_DEVTOOLS_EXAMPLES.md)
10 практичних прикладів для різних сценаріїв.

---

## 🔧 Встановлення

1. Додати NuGet package:
```bash
dotnet add package Microsoft.Playwright
```

2. Встановити браузери Playwright:
```bash
pwsh bin/Debug/net9.0/playwright.ps1 install
```

---

## 🎨 UI Використання

### Вся функціональність в одній сторінці
```xml
<UserControl xmlns:pages="using:VetaleBrowser.VetaleBrowser.DevTools.Pages">
    <pages:PlaywrightDevToolsMainPage />
</UserControl>
```

### Окремі модулі
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

---

## 📊 Функціонал

### Elements
- Повна DOM структура (до 15 рівнів)
- Computed styles
- Attributes
- CSS selector пошук
- Bounding rectangles

### Performance
- Load Time
- DOM Content Loaded
- First Paint / First Contentful Paint
- Core Web Vitals (LCP, FID, CLS)
- Memory Usage
- Resource Timings

### Application
- localStorage (read/write/delete)
- sessionStorage
- Cookies (read/clear)
- IndexedDB (list databases)

### Sources
- Scripts (external + inline)
- Stylesheets
- Images
- HTML source
- JavaScript executor

---

## 🎯 API Methods

### Navigation
```csharp
await InitializeAsync()
await NavigateAsync(url)
```

### Elements
```csharp
await CaptureDomStructureAsync()
await GetElementDetailsAsync(selector)
await QuerySelectorAllAsync(selector)
```

### Performance
```csharp
await CapturePerformanceSnapshotAsync()
await GetCoreWebVitalsAsync()
```

### Application
```csharp
await CaptureStorageAsync()
await SetLocalStorageAsync(key, value)
await RemoveLocalStorageAsync(key)
await ClearCookiesAsync()
```

### Sources
```csharp
await CapturePageResourcesAsync()
await GetPageHtmlAsync()
await ExecuteScriptAsync(script)
```

### Network
```csharp
EnableNetworkMonitoring(callback)
```

---

## 🎭 Events

```csharp
NavigationCompleted
DomElementCaptured
PerformanceSnapshotCaptured
ResourceCaptured
StorageItemCaptured
```

---

## 💾 Database Storage

Всі дані зберігаються в **LiteDB** через `DevToolsDataService`:

- `devtools_dom_elements`
- `devtools_performance_snapshots`
- `devtools_resources`
- `devtools_storage_items`

---

## 📈 Статистика

- **Рядків коду:** ~2500+
- **Методів API:** 15+
- **UI Сторінок:** 5
- **Підтримуваних типів ресурсів:** 4+
- **Метрик Performance:** 10+

---

## 🌟 Особливості

- ✅ Повна інтеграція з Playwright
- ✅ Автоматичне збереження в базу даних
- ✅ Event-driven архітектура
- ✅ Avalonia UI готові компоненти
- ✅ Підтримка Core Web Vitals
- ✅ JavaScript executor
- ✅ Network monitoring
- ✅ Детальна документація

---

## 📝 Приклади

### Базовий аналіз
```csharp
var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
await playwright.InitializeAsync();
await playwright.NavigateAsync("https://github.com");

var perf = await playwright.CapturePerformanceSnapshotAsync();
Console.WriteLine($"Load Time: {perf.LoadTime}ms");
```

### Пошук елементів
```csharp
var buttons = await playwright.QuerySelectorAllAsync("button");
foreach (var btn in buttons)
{
    Console.WriteLine($"<{btn.TagName}> - {btn.Attributes}");
}
```

### Управління Storage
```csharp
await playwright.SetLocalStorageAsync("theme", "dark");
var storage = await playwright.CaptureStorageAsync();
```

**Більше прикладів:** [PLAYWRIGHT_DEVTOOLS_EXAMPLES.md](PLAYWRIGHT_DEVTOOLS_EXAMPLES.md)

---

## 🔗 Корисні посилання

- [Microsoft Playwright Documentation](https://playwright.dev/dotnet/)
- [Core Web Vitals](https://web.dev/vitals/)
- [Navigation Timing API](https://developer.mozilla.org/en-US/docs/Web/API/Navigation_timing_API)

---

## ⚠️ Вимоги

- .NET 9.0+
- Avalonia UI
- Microsoft.Playwright NuGet package
- LiteDB (через DevToolsDataService)

---

## 🎉 Готово до використання!

Всі компоненти протестовані та готові до інтеграції в VetaleBrowser.

**Автор:** GitHub Copilot  
**Дата:** 5 листопада 2025  
**Версія:** 1.0.0

---

## 📞 Підтримка

Для питань та допомоги дивіться документацію:
- [Повна документація](PLAYWRIGHT_DEVTOOLS_DOCUMENTATION.md)
- [Приклади](PLAYWRIGHT_DEVTOOLS_EXAMPLES.md)

