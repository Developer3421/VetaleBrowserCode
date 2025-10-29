# План створення веб-браузера VetaleBrowser

## Огляд проекту

Професійний веб-браузер на базі Avalonia з інтеграцією WebView2, локальним AI-чатом (Gemma3) з персонажем Ветале, метапошуком та розширеними інструментами розробника.

## Технологічний стек

- **UI Framework**: Avalonia (Cross-platform)
- **WebView**: WebView2 (Chromium-based)
- **AI/LLM**: LlamaSharp + Gemma3 (локальна модель)
- **JavaScript Engine**: NiL.JS
- **HTML Parser**: AngleSharp
- **Database**: LiteDB
- **External APIs**: Gemini API
- **Web Scraping**: Custom scraping для Yahoo, Ecosia, Gigablast

---

## Етап 1: Підготовка та базова архітектура (1-2 тижні)

### 1.1 Структура проекту
```
VetaleBrowser/
├── VetaleBrowser.Core/          # Бізнес-логіка
├── VetaleBrowser.UI/            # Avalonia UI
├── VetaleBrowser.WebEngine/     # WebView2 wrapper
├── VetaleBrowser.AI/            # LlamaSharp + Gemma3
├── VetaleBrowser.Search/        # Метапошук
├── VetaleBrowser.Database/      # LiteDB
├── VetaleBrowser.DevTools/      # Інструменти розробника
└── VetaleBrowser.JavaScript/    # NiL.JS інтеграція
```

### 1.2 NuGet пакети
```xml
<!-- WebView2 -->
<PackageReference Include="Microsoft.Web.WebView2" />
<PackageReference Include="WebViewControl" />

<!-- AI/LLM -->
<PackageReference Include="LLamaSharp" />
<PackageReference Include="LLamaSharp.Backend.Cpu" />

<!-- JavaScript Engine -->
<PackageReference Include="NiL.JS" />

<!-- HTML Parsing -->
<PackageReference Include="AngleSharp" />

<!-- Database -->
<PackageReference Include="LiteDB" />

<!-- HTTP/Scraping -->
<PackageReference Include="HtmlAgilityPack" />
<PackageReference Include="Flurl.Http" />

<!-- Gemini API -->
<PackageReference Include="GenerativeAI" />

<!-- Avalonia -->
<PackageReference Include="Avalonia" />
<PackageReference Include="Avalonia.Desktop" />
<PackageReference Include="Avalonia.ReactiveUI" />
```

### 1.3 Налаштування проекту
- ✅ Створити Solution з проектами
- ✅ Налаштувати Dependency Injection (Microsoft.Extensions.DependencyInjection)
- ✅ Налаштувати логування (Serilog)
- ✅ Створити базову архітектуру MVVM

---

## Етап 2: Базовий UI та навігація (1 тиждень)

### 2.1 Головне вікно
- **Компоненти:**
  - Адресна строка
  - Кнопки навігації (назад, вперед, перезавантажити)
  - Менеджер вкладок (TabControl)
  - Меню налаштувань
  - Панель закладок

### 2.2 Головна сторінка (New Tab)
```
┌─────────────────────────────────────┐
│  [Logo]  VetaleBrowser              │
│                                     │
│  ┌───────────────────────────────┐ │
│  │  🔍 Пошук або URL             │ │
│  └───────────────────────────────┘ │
│                                     │
│  Швидкий доступ: [Закладки]        │
│  Історія | Завантаження            │
└─────────────────────────────────────┘
```

### 2.3 AXAML Views
- `MainWindow.axaml` - головне вікно
- `NewTabPage.axaml` - головна сторінка
- `SearchResultsPage.axaml` - результати пошуку
- `BrowserTab.axaml` - компонент вкладки
- `SettingsWindow.axaml` - налаштування

---

## Етап 3: Інтеграція WebView2 (1-2 тижні)

### 3.1 Базовий WebView
```csharp
public class BrowserTabViewModel : ViewModelBase
{
    public WebView2 WebView { get; set; }
    public string Url { get; set; }
    public string Title { get; set; }
    public bool IsLoading { get; set; }
}
```

### 3.2 Багатопроцесова архітектура
- Кожна вкладка = окремий процес
- Ізоляція пам'яті
- Управління життєвим циклом процесів
```csharp
public class TabProcessManager
{
    private Dictionary<Guid, Process> _tabProcesses;
    
    public Guid CreateTabProcess() { }
    public void TerminateTabProcess(Guid tabId) { }
    public void MonitorTabProcesses() { }
}
```

### 3.3 WebView2 Features
- Управління cookies
- LocalStorage/SessionStorage
- JavaScript injection
- Intercepting requests
- Download manager

---

## Етап 4: Метапошукова система (2-3 тижні)

### 4.1 Архітектура пошуку
```csharp
public interface ISearchProvider
{
    Task<SearchResults> SearchAsync(string query, SearchType type);
}

public enum SearchType
{
    Web,
    Images,
    Videos,
    News
}
```

### 4.2 Імплементація провайдерів

#### 4.2.1 Yahoo Scraper
```csharp
public class YahooSearchProvider : ISearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly IHtmlParser _parser; // AngleSharp
    
    public async Task<SearchResults> SearchAsync(string query, SearchType type)
    {
        // Scraping логіка
    }
}
```

#### 4.2.2 Ecosia Scraper
```csharp
public class EcosiaSearchProvider : ISearchProvider
{
    // Екологічний пошук
}
```

#### 4.2.3 Gigablast Scraper
```csharp
public class GigablastSearchProvider : ISearchProvider
{
    // Відкритий пошук
}
```

### 4.3 Агрегатор результатів
```csharp
public class MetaSearchEngine
{
    private readonly List<ISearchProvider> _providers;
    
    public async Task<AggregatedSearchResults> SearchAllAsync(string query)
    {
        // Паралельний пошук по всіх провайдерах
        // Мердж та ранкінг результатів
    }
}
```

### 4.4 Сторінка результатів пошуку
```
┌─────────────────────────────────────────────┐
│ [◀] [Пошук: "Query"]          [Gemini API ▼]│
├─────────────────────────────────────────────┤
│ Фільтри: [Всі] [Зображення] [Відео] [Новини]│
├─────────────────────────────────────────────┤
│ 📄 Result Title                              │
│    https://example.com                      │
│    Description of the result...             │
├─────────────────────────────────────────────┤
│ 📄 Result Title 2                            │
│    ...                                      │
└─────────────────────────────────────────────┘
```

---

## Етап 5: Інтеграція Gemini API (1 тиждень)

### 5.1 Gemini Service
```csharp
public class GeminiService
{
    private readonly GoogleAI _client;
    
    public async Task<string> AskGemini(string prompt) { }
    public async Task<string> SummarizeSearchResults(List<SearchResult> results) { }
    public async Task<string> AnalyzeWebPage(string html) { }
}
```

### 5.2 UI компонент
- Sidebar в результатах пошуку
- Quick answers
- Summarization
- Contextual suggestions

### 5.3 API для різних типів пошуку
```csharp
public interface IGeminiSearchAPI
{
    Task<GeminiResponse> SearchWeb(string query);
    Task<GeminiResponse> SearchNews(string query);
    Task<GeminiResponse> SearchImages(string query);
    Task<GeminiResponse> SearchVideos(string query);
}
```

---

## Етап 6: Локальний AI чат з Ветале (2-3 тижні)

### 6.1 Інтеграція LlamaSharp + Gemma3

```csharp
public class VetaleAIService
{
    private LLamaContext _context;
    private LLamaModel _model;
    
    public async Task InitializeAsync(string modelPath)
    {
        var parameters = new ModelParams
        {
            ContextSize = 4096,
            GpuLayerCount = 0 // CPU mode
        };
        
        _model = await LLamaModel.LoadFromFileAsync(modelPath, parameters);
    }
    
    public async Task<string> ChatAsync(string userMessage, List<Message> history)
    {
        // Додаємо системний промпт Ветале
    }
}
```

### 6.2 Системний промпт Ветале
```csharp
private const string VETALE_SYSTEM_PROMPT = @"
Ти - Ветале, мудрий та хитрий дух з індійської міфології. 
Ти схожий на демона, але насправді є носієм глибоких знань.

Характеристики:
- Мудрий та проникливий
- Може давати поради з філософським підтекстом
- Розповідає історії з прихованим сенсом
- Допомагає користувачу в браузері
- Може аналізувати веб-сторінки
- Пояснює технічні концепції через метафори

Стиль спілкування:
- Дружній, але таємничий
- Використовує метафори
- Іноді жартує
";
```

### 6.3 UI чату
```
┌─────────────────────────────┐
│ 💀 Ветале AI Chat           │
├─────────────────────────────┤
│ User: Що це за сайт?        │
│                             │
│ Ветале: Дозволь розповісти  │
│ тобі історію про...         │
├─────────────────────────────┤
│ [Введіть питання...]   [→]  │
└─────────────────────────────┘
```

### 6.4 Інструменти (Tools) для Ветале
```csharp
public class VetaleTools
{
    // Реальні інструменти для AI
    
    [Tool("Analyze current webpage")]
    public async Task<string> AnalyzeCurrentPage(string url) { }
    
    [Tool("Search in page")]
    public async Task<string> SearchInPage(string query) { }
    
    [Tool("Get page metadata")]
    public async Task<PageMetadata> GetPageMetadata() { }
    
    [Tool("Extract links")]
    public async Task<List<string>> ExtractLinks() { }
    
    [Tool("Run JavaScript")]
    public async Task<string> RunJavaScript(string code) { }
}
```

---

## Етап 7: NiL.JS інтеграція (1-2 тижні)

### 7.1 JavaScript Engine Wrapper
```csharp
public class JavaScriptEngine
{
    private readonly NiL.JS.Core.Context _context;
    
    public JavaScriptEngine()
    {
        _context = new NiL.JS.Core.Context();
        RegisterAPIs();
    }
    
    private void RegisterAPIs()
    {
        // Expose browser APIs to JavaScript
        _context.DefineVariable("browser").Assign(new BrowserAPI());
        _context.DefineVariable("vetale").Assign(new VetaleAPI());
    }
    
    public object Execute(string code) { }
}
```

### 7.2 Custom Browser APIs
```csharp
public class BrowserAPI
{
    public void navigate(string url) { }
    public string getCurrentUrl() { }
    public void executeDevTools(string command) { }
}
```

### 7.3 Використання
- User scripts
- Extensions
- DevTools console
- Page automation

---

## Етап 8: Інструменти розробника (2-3 тижні)

### 8.1 DevTools UI
```
┌─────────────────────────────────────────┐
│ [Elements] [Console] [Network] [Sources]│
├─────────────────────────────────────────┤
│ DOM Tree:                               │
│ ▼ <html>                                │
│   ▼ <body>                              │
│     ▼ <div class="container">          │
│       <p>Text</p>                       │
├─────────────────────────────────────────┤
│ Properties | Styles | Computed          │
└─────────────────────────────────────────┘
```

### 8.2 Компоненти DevTools

#### 8.2.1 Elements Inspector
```csharp
public class ElementsInspector
{
    public async Task<DomTree> GetDomTree() 
    {
        // Використовує AngleSharp
    }
    
    public async Task<ElementStyles> GetComputedStyles(string selector) { }
    public async Task ModifyElement(string selector, string property, string value) { }
}
```

#### 8.2.2 Console
```csharp
public class DevToolsConsole
{
    private readonly JavaScriptEngine _jsEngine;
    
    public async Task<ConsoleResult> ExecuteCommand(string command) 
    {
        // Виконання через NiL.JS
    }
    
    public void Log(LogLevel level, string message) { }
}
```

#### 8.2.3 Network Monitor
```csharp
public class NetworkMonitor
{
    public event EventHandler<RequestEventArgs> RequestStarted;
    public event EventHandler<ResponseEventArgs> ResponseReceived;
    
    public List<NetworkRequest> GetRequests() { }
    public void ClearRequests() { }
    public async Task<string> GetRequestDetails(Guid requestId) { }
}
```

#### 8.2.4 Sources (Debugger)
```csharp
public class SourcesDebugger
{
    public async Task<List<Script>> GetScripts() { }
    public async Task SetBreakpoint(string scriptId, int line) { }
    public async Task StepOver() { }
    public async Task StepInto() { }
}
```

### 8.3 WebView2 DevTools Protocol
```csharp
public class ChromeDevToolsService
{
    private CoreWebView2DevToolsProtocolHelper _devTools;
    
    public async Task EnableDomainAsync(string domain) { }
    public async Task<JObject> SendCommandAsync(string method, JObject parameters) { }
}
```

---

## Етап 9: База даних LiteDB (1 тиждень)

### 9.1 Схема даних
```csharp
// Історія
public class HistoryEntry
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public string Title { get; set; }
    public DateTime VisitedAt { get; set; }
    public int VisitCount { get; set; }
}

// Закладки
public class Bookmark
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public string Title { get; set; }
    public string Folder { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; }
}

// Налаштування
public class Settings
{
    public Guid Id { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
}

// Завантаження
public class Download
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public string FilePath { get; set; }
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public DownloadStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
}

// Чат історія з Ветале
public class ChatMessage
{
    public Guid Id { get; set; }
    public string Role { get; set; } // user/assistant
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
    public string SessionId { get; set; }
}
```

### 9.2 Database Service
```csharp
public class DatabaseService
{
    private readonly LiteDatabase _db;
    
    public DatabaseService(string dbPath)
    {
        _db = new LiteDatabase(dbPath);
    }
    
    public ILiteCollection<HistoryEntry> History => _db.GetCollection<HistoryEntry>();
    public ILiteCollection<Bookmark> Bookmarks => _db.GetCollection<Bookmark>();
    public ILiteCollection<Settings> Settings => _db.GetCollection<Settings>();
    public ILiteCollection<Download> Downloads => _db.GetCollection<Download>();
    public ILiteCollection<ChatMessage> ChatHistory => _db.GetCollection<ChatMessage>();
}
```

---

## Етап 10: Професійні функції (2-3 тижні)

### 10.1 Менеджер вкладок
- Drag & Drop вкладок
- Закріплені вкладки
- Групи вкладок
- Відновлення сесії
- Hibernate неактивних вкладок (економія пам'яті)

### 10.2 Менеджер завантажень
```csharp
public class DownloadManager
{
    public event EventHandler<DownloadProgressEventArgs> ProgressChanged;
    
    public async Task<Guid> StartDownloadAsync(string url, string savePath) { }
    public async Task PauseDownloadAsync(Guid downloadId) { }
    public async Task ResumeDownloadAsync(Guid downloadId) { }
    public async Task CancelDownloadAsync(Guid downloadId) { }
}
```

### 10.3 Профілі користувачів
- Окремі профілі з ізольованими даними
- Переключення між профілями
- Синхронізація (опціонально)

### 10.4 Розширення та теми
```csharp
public interface IBrowserExtension
{
    string Id { get; }
    string Name { get; }
    void OnLoad();
    void OnPageLoad(string url);
}

public class ExtensionManager
{
    public void LoadExtension(string path) { }
    public void UnloadExtension(string id) { }
}
```

### 10.5 Приватний режим
- Окремі WebView2 instances
- Без збереження історії
- Ізоляція cookies

### 10.6 Блокування реклами (базовий)
```csharp
public class AdBlocker
{
    private HashSet<string> _blockedDomains;
    
    public bool ShouldBlockRequest(string url) { }
    public void LoadFilterList(string path) { }
}
```

### 10.7 Автозаповнення форм
```csharp
public class AutoFillManager
{
    public async Task SaveFormData(string url, Dictionary<string, string> formData) { }
    public async Task<Dictionary<string, string>> GetFormData(string url) { }
}
```

---

## Етап 11: Оптимізація та тестування (2 тижні)

### 11.1 Оптимізація продуктивності
- Lazy loading компонентів
- Віртуалізація списків
- Кешування результатів пошуку
- Memory profiling
- Оптимізація AI inference (квантизація моделі)

### 11.2 Тестування
```csharp
// Unit tests
[Fact]
public async Task SearchEngine_Should_ReturnResults()
{
    var engine = new MetaSearchEngine();
    var results = await engine.SearchAllAsync("test query");
    Assert.NotEmpty(results.Items);
}

// Integration tests
[Fact]
public async Task WebView_Should_LoadPage()
{
    var tab = new BrowserTab();
    await tab.NavigateAsync("https://example.com");
    Assert.Equal("https://example.com", tab.Url);
}
```

### 11.3 Error Handling
- Глобальний exception handler
- Crash reporting
- Логування помилок
- Graceful degradation

---

## Етап 12: Фінальна інтеграція (1-2 тижні)

### 12.1 Об'єднання всіх компонентів
- Інтеграція WebView2 + Search + AI
- Синхронізація стану між компонентами
- Lifecycle management

### 12.2 UI/UX поліпшення
- Анімації та переходи
- Keyboard shortcuts
- Accessibility
- Responsive design

### 12.3 Налаштування
```
Settings:
├── General
│   ├── Startup behavior
│   ├── Default search engine
│   └── Download location
├── Appearance
│   ├── Theme (Light/Dark)
│   └── Font size
├── Privacy
│   ├── Clear browsing data
│   ├── Do Not Track
│   └── Cookies
├── Vetale AI
│   ├── Model path
│   ├── Temperature
│   └── Max tokens
└── Advanced
    ├── Hardware acceleration
    └── Developer mode
```

---

## Етап 13: Документація та розгортання (1 тиждень)

### 13.1 Документація
- README.md з інструкціями
- API документація
- Посібник користувача
- Developer guide

### 13.2 Packaging
```xml
<!-- Self-contained publish -->
<PropertyGroup>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>true</PublishTrimmed>
</PropertyGroup>
```

### 13.3 Installer
- WiX Toolset або Inno Setup
- Auto-update mechanism
- Registry entries

---

## Часова оцінка

| Етап | Тривалість | Пріоритет |
|------|-----------|-----------|
| 1. Підготовка | 1-2 тижні | Високий |
| 2. Базовий UI | 1 тиждень | Високий |
| 3. WebView2 | 1-2 тижні | Високий |
| 4. Метапошук | 2-3 тижні | Високий |
| 5. Gemini API | 1 тиждень | Середній |
| 6. Ветале AI | 2-3 тижні | Високий |
| 7. NiL.JS | 1-2 тижні | Середній |
| 8. DevTools | 2-3 тижні | Середній |
| 9. LiteDB | 1 тиждень | Високий |
| 10. Професійні функції | 2-3 тижні | Середній |
| 11. Оптимізація | 2 тижні | Високий |
| 12. Інтеграція | 1-2 тижні | Високий |
| 13. Документація | 1 тиждень | Низький |

**Загальна оцінка:** 4-6 місяців для повної реалізації

---

## Технічні виклики та рішення

### Виклик 1: Продуктивність AI
**Проблема:** Gemma3 може бути повільним на CPU
**Рішення:** 
- Використання квантизованої моделі (4-bit/8-bit)
- Streaming responses
- Background processing
- GPU acceleration (опціонально)

### Виклик 2: Scraping сайтів
**Проблема:** Блокування, зміни структури
**Рішення:**
- User-agent rotation
- Rate limiting
- Fallback механізми
- Regular expressions + AngleSharp

### Виклик 3: Багатопроцесовість
**Проблема:** IPC між процесами вкладок
**Рішення:**
- Named pipes
- Shared memory
- Message queue system

### Виклик 4: Memory leaks
**Проблема:** WebView2 може споживати багато пам'яті
**Рішення:**
- Dispose pattern
- Tab hibernation
- Periodic cleanup
- Memory monitoring

---

## Архітектура рішення

```
┌─────────────────────────────────────────────────────────┐
│                    MainWindow (UI)                      │
├─────────────────────────────────────────────────────────┤
│  TabManager │ AddressBar │ SearchBox │ VetaleChat      │
└────┬─────────────────┬─────────────┬──────────────┬─────┘
     │                 │             │              │
     ▼                 ▼             ▼              ▼
┌─────────┐  ┌──────────────┐  ┌─────────┐  ┌──────────┐
│WebView2 │  │MetaSearch    │  │GeminiAPI│  │LlamaSharp│
│(Process)│  │Engine        │  │Service  │  │+Gemma3   │
└────┬────┘  └──────┬───────┘  └────┬────┘  └────┬─────┘
     │              │               │            │
     │         ┌────┴───────────────┴────────────┴─────┐
     │         │         Services Layer                │
     │         │  ┌──────────┐  ┌────────────────┐    │
     └─────────┼─>│DevTools  │  │DownloadManager │    │
               │  └──────────┘  └────────────────┘    │
               │  ┌──────────┐  ┌────────────────┐    │
               │  │NiL.JS    │  │ExtensionManager│    │
               │  └──────────┘  └────────────────┘    │
               └───────────────────┬──────────────────┘
                                   │
                            ┌──────▼──────┐
                            │   LiteDB    │
                            │  Database   │
                            └─────────────┘
```

---

## Пріоритизація MVP (Minimum Viable Product)

### Фаза 1 (MVP) - 6-8 тижнів
1. ✅ Базовий UI з вкладками
2. ✅ WebView2 інтеграція
3. ✅ Базова навігація
4. ✅ Історія + Закладки (LiteDB)
5. ✅ Один пошуковий провайдер
6. ✅ Базовий Ветале чат

### Фаза 2 - 6-8 тижнів
1. Метапошук (всі провайдери)
2. DevTools (Elements + Console)
3. Gemini API інтеграція
4. NiL.JS для user scripts
5. Професійні функції (завантаження, профілі)

### Фаза 3 - 4-6 тижнів
1. Повні DevTools
2. Ветале Tools
3. Розширення
4. Оптимізація
5. Packaging

---

## Рекомендації для початку

### Крок 1: Налаштуйте проект
```bash
dotnet new sln -n VetaleBrowser
dotnet new avalonia.mvvm -n VetaleBrowser.UI
dotnet new classlib -n VetaleBrowser.Core
dotnet new classlib -n VetaleBrowser.WebEngine
dotnet new classlib -n VetaleBrowser.AI
dotnet new classlib -n VetaleBrowser.Search
```

### Крок 2: Встановіть залежності
```bash
cd VetaleBrowser.UI
dotnet add package Microsoft.Web.WebView2
dotnet add package LiteDB
dotnet add package Avalonia.ReactiveUI
```

### Крок 3: Створіть базову структуру
- Models
- ViewModels
- Views
- Services
- Repositories

### Крок 4: Імплементуйте по етапах
Слідуйте плану від Етапу 1 до Етапу 13

---

## Корисні ресурси

### Документація
- [Avalonia Docs](https://docs.avaloniaui.net/)
- [WebView2 Docs](https://learn.microsoft.com/en-us/microsoft-edge/webview2/)
- [LlamaSharp GitHub](https://github.com/SciSharp/LLamaSharp)
- [NiL.JS GitHub](https://github.com/nilproject/NiL.JS)
- [AngleSharp Docs](https://anglesharp.github.io/)
- [LiteDB Docs](https://www.litedb.org/docs/)

### Моделі AI
- [Gemma Models](https://ai.google.dev/gemma)
- Завантажити квантизовану модель з Hugging Face

### Приклади коду
- WebView2 samples
- Avalonia samples
- LlamaSharp examples

---

## Підсумок

Цей проект є амбітним та технічно складним, але цілком реалізовним при правильному підході.

**Ключові фактори успіху:**
1. Поетапна розробка (не намагайтеся зробити все відразу)
2. Тестування після кожного етапу
3. Модульна архітектура
4. Правильне управління пам'яттю
5. Performance profiling

**Рекомендована послідовність:**
MVP → Метапошук → AI/Ветале → DevTools → Професійні функції

Удачі у розробці VetaleBrowser! 🚀💀

