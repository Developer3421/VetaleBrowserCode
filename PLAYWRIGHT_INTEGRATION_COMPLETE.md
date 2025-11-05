# Інтеграція Playwright з DevTools - Інструкція

## Огляд

Тепер VetaleBrowser DevTools використовує **headless Chromium через Playwright** для аналізу веб-сторінок паралельно з локальним WebView.

## Як це працює

1. **WebView (локальний)** - відображає сторінку користувачеві на вкладці "WebView Worker"
2. **Playwright (headless)** - автоматично запускається у фоні для глибокого аналізу:
   - DOM Elements
   - Performance метрики
   - Resources (scripts, styles, images)
   - Storage (localStorage, sessionStorage, cookies)

### Потік роботи

```
WebView (відображає сторінку) 
    ↓
    URL → беремо адресу
    ↓
Playwright (headless) → відкриває ту ж сторінку у фоні
    ↓
Аналізує DOM, Performance, Resources, Storage
    ↓
Зберігає дані в базу
    ↓
Відображає результати в DevTools
```

## Встановлення

### 1. Встановлення Playwright браузерів

Після першої компіляції проекту, запустіть команду:

**PowerShell:**
```powershell
playwright install chromium
```

**Або через dotnet (якщо playwright не в PATH):**
```powershell
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

**Альтернатива - вручну:**
```csharp
// Код вже є в PlaywrightDevToolsService.cs
await PlaywrightDevToolsService.EnsureBrowsersInstalledAsync();
```

### 2. Перевірка встановлення

Після запуску VetaleBrowser перевірте консоль Debug:
```
[WebViewWorkerService] Playwright initialized in headless mode
```

Якщо ви бачите це повідомлення - Playwright працює!

## Використання

### Основні DevTools сторінки

1. **Elements Page** (`PlaywrightElementsPage`)
   - Відкрийте сторінку в WebView Worker
   - Натисніть "Capture DOM"
   - Playwright автоматично проаналізує DOM структуру

2. **Performance Page** (`PlaywrightPerformancePage`)
   - Відкрийте сторінку в WebView Worker
   - Натисніть "Capture Performance"
   - Playwright збере метрики завантаження

3. **Sources Page** (`PlaywrightSourcesPage`)
   - Відкрийте сторінку в WebView Worker
   - Натисніть "Capture Resources"
   - Playwright проаналізує всі scripts, styles, images

4. **Application Page** (`PlaywrightApplicationPage`)
   - Відкрийте сторінку в WebView Worker
   - Натисніть "Capture Storage"
   - Playwright збере localStorage, sessionStorage, cookies

### Код приклад

```csharp
var service = new WebViewWorkerService(dataService);

// Attach local WebView (показує сторінку користувачу)
service.AttachLocalWebView(myWebView);

// Playwright автоматично використовується для аналізу
var domElements = await service.CaptureDomStructureAsync();
var performance = await service.CapturePerformanceSnapshotAsync();
var resources = await service.CapturePageResourcesAsync();
var storage = await service.CaptureStorageAsync();
```

## Залежності

Всі залежності вже додані в `VetaleBrowser.csproj`:

```xml
<PackageReference Include="Microsoft.Playwright" Version="1.55.0" />
<PackageReference Include="WebViewControl-Avalonia" Version="3.120.9" />
```

## Переваги

✅ **Headless режим** - Playwright працює у фоні без відкриття вікна  
✅ **Паралельна робота** - WebView показує сторінку, Playwright аналізує  
✅ **Автоматичне взаємодія** - Просто відкрийте URL в WebView, Playwright зробить решту  
✅ **Fallback** - Якщо Playwright не доступний, працює тільки WebView  
✅ **Кращий аналіз** - Playwright має повний доступ до Chrome DevTools Protocol  

## Troubleshooting

### Playwright не ініціалізується

```
[WebViewWorkerService] Failed to initialize Playwright: ...
```

**Рішення:**
1. Встановіть браузери: `playwright install chromium`
2. Перевірте права доступу до папки користувача
3. Перезапустіть VetaleBrowser

### Браузери не встановлені

```
Executable doesn't exist at ...
```

**Рішення:**
```powershell
playwright install chromium
```

### Playwright повільно працює

Це нормально для першого запуску. Наступні запуски будуть швидшими через кешування.

## Актуальність API

Код використовує **актуальний Playwright API** (версія 1.55.0):

✅ `new()` синтаксис замість `BrowserTypeLaunchOptions`  
✅ `new()` синтаксис замість `BrowserNewContextOptions`  
✅ `new()` синтаксис замість `PageGotoOptions`  
✅ Без застарілих подій `Load` та `DOMContentLoaded`  

## Структура коду

```
VetaleBrowser.DevTools/
├── Services/
│   ├── WebViewWorkerService.cs      ← Головний сервіс (інтеграція)
│   ├── PlaywrightDevToolsService.cs ← Playwright логіка
│   └── DevToolsDataService.cs       ← База даних
├── Pages/
│   ├── WebViewWorkerPage.axaml.cs   ← WebView UI
│   ├── PlaywrightElementsPage.cs    ← DOM аналіз
│   ├── PlaywrightPerformancePage.cs ← Performance
│   ├── PlaywrightSourcesPage.cs     ← Resources
│   └── PlaywrightApplicationPage.cs ← Storage
```

## Підсумок

Тепер VetaleBrowser DevTools має потужний headless Chrome для аналізу веб-сторінок! 🚀

Playwright працює паралельно з WebView:
- WebView показує сторінку
- Playwright аналізує DOM, Performance, Resources, Storage у фоні

Просто відкрийте сторінку в WebView Worker та натисніть кнопки захоплення даних!

