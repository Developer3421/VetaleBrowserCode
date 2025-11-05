# Playwright DevTools - Приклади використання

## Приклад 1: Базовий аналіз сторінки

```csharp
using VetaleBrowser.VetaleBrowser.DevTools.Services;
using VetaleBrowser.VetaleBrowser.Database.Services;

public async Task AnalyzePage()
{
    var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
    
    try
    {
        // Ініціалізація
        await playwright.InitializeAsync();
        
        // Відкрити сторінку
        await playwright.NavigateAsync("https://github.com");
        
        // Захопити DOM
        var domElements = await playwright.CaptureDomStructureAsync();
        Console.WriteLine($"DOM Elements: {domElements.Count}");
        
        // Захопити Performance
        var perf = await playwright.CapturePerformanceSnapshotAsync();
        Console.WriteLine($"Load Time: {perf.LoadTime}ms");
        Console.WriteLine($"Memory: {perf.MemoryUsed / 1024 / 1024}MB");
        
        // Core Web Vitals
        var vitals = await playwright.GetCoreWebVitalsAsync();
        foreach (var vital in vitals)
        {
            Console.WriteLine($"{vital.Key}: {vital.Value}");
        }
    }
    finally
    {
        playwright.Dispose();
    }
}
```

---

## Приклад 2: Пошук елементів та аналіз стилів

```csharp
public async Task FindAndAnalyzeButtons()
{
    var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
    await playwright.InitializeAsync();
    await playwright.NavigateAsync("https://example.com");
    
    // Знайти всі кнопки
    var buttons = await playwright.QuerySelectorAllAsync("button");
    
    foreach (var button in buttons)
    {
        Console.WriteLine($"Button: {button.TagName}");
        
        // Розпарсити стилі
        var styles = JsonSerializer.Deserialize<Dictionary<string, string>>(
            button.ComputedStyles ?? "{}"
        );
        
        if (styles != null)
        {
            Console.WriteLine($"  Background: {styles.GetValueOrDefault("backgroundColor")}");
            Console.WriteLine($"  Color: {styles.GetValueOrDefault("color")}");
            Console.WriteLine($"  Font Size: {styles.GetValueOrDefault("fontSize")}");
        }
    }
    
    playwright.Dispose();
}
```

---

## Приклад 3: Робота з Storage

```csharp
public async Task ManageStorage()
{
    var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
    await playwright.InitializeAsync();
    await playwright.NavigateAsync("https://example.com");
    
    // Встановити дані в localStorage
    await playwright.SetLocalStorageAsync("user_theme", "dark");
    await playwright.SetLocalStorageAsync("user_lang", "uk");
    
    // Захопити всі storage дані
    var storage = await playwright.CaptureStorageAsync();
    
    // Вивести localStorage
    var localStorage = storage.Where(x => x.StorageType == "localStorage");
    foreach (var item in localStorage)
    {
        Console.WriteLine($"{item.Key} = {item.EncryptedValue}");
    }
    
    // Вивести cookies
    var cookies = storage.Where(x => x.StorageType == "cookies");
    foreach (var cookie in cookies)
    {
        Console.WriteLine($"Cookie: {cookie.Key} = {cookie.EncryptedValue}");
        Console.WriteLine($"  Domain: {cookie.Domain}");
        Console.WriteLine($"  Expires: {cookie.ExpiresAt}");
    }
    
    // Очистити cookies
    await playwright.ClearCookiesAsync();
    
    playwright.Dispose();
}
```

---

## Приклад 4: Аналіз ресурсів

```csharp
public async Task AnalyzeResources()
{
    var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
    await playwright.InitializeAsync();
    await playwright.NavigateAsync("https://github.com");
    
    // Захопити всі ресурси
    var resources = await playwright.CapturePageResourcesAsync();
    
    // Групувати за типом
    var scripts = resources.Where(r => r.Type == "script").ToList();
    var stylesheets = resources.Where(r => r.Type == "stylesheet").ToList();
    var images = resources.Where(r => r.Type == "image").ToList();
    
    Console.WriteLine($"Scripts: {scripts.Count}");
    Console.WriteLine($"Stylesheets: {stylesheets.Count}");
    Console.WriteLine($"Images: {images.Count}");
    
    // Вивести найбільші ресурси
    var largestResources = resources
        .OrderByDescending(r => r.EncryptedContent?.Length ?? 0)
        .Take(5);
    
    Console.WriteLine("\nTop 5 largest resources:");
    foreach (var resource in largestResources)
    {
        var size = (resource.EncryptedContent?.Length ?? 0) / 1024.0;
        Console.WriteLine($"  {resource.Url} - {size:F2} KB");
    }
    
    playwright.Dispose();
}
```

---

## Приклад 5: Виконання JavaScript

```csharp
public async Task ExecuteCustomScripts()
{
    var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
    await playwright.InitializeAsync();
    await playwright.NavigateAsync("https://example.com");
    
    // Отримати title сторінки
    var title = await playwright.ExecuteScriptAsync("document.title");
    Console.WriteLine($"Title: {title}");
    
    // Отримати всі заголовки
    var headers = await playwright.ExecuteScriptAsync(@"
        Array.from(document.querySelectorAll('h1, h2, h3'))
            .map(h => ({ tag: h.tagName, text: h.textContent.trim() }))
    ");
    Console.WriteLine($"Headers: {headers}");
    
    // Підрахувати кількість зображень
    var imageCount = await playwright.ExecuteScriptAsync(
        "document.querySelectorAll('img').length"
    );
    Console.WriteLine($"Images count: {imageCount}");
    
    // Змінити текст на сторінці
    await playwright.ExecuteScriptAsync(@"
        document.querySelector('h1').textContent = 'Modified by Playwright!'
    ");
    
    playwright.Dispose();
}
```

---

## Приклад 6: Моніторинг мережевих запитів

```csharp
public async Task MonitorNetwork()
{
    var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
    await playwright.InitializeAsync();
    
    var requests = new List<(string Method, string Url, int Status)>();
    
    // Увімкнути моніторинг
    playwright.EnableNetworkMonitoring((method, url, status) =>
    {
        requests.Add((method, url, status));
        Console.WriteLine($"{method} {url} - {status}");
    });
    
    // Навігація з моніторингом
    await playwright.NavigateAsync("https://github.com");
    
    // Почекати завершення всіх запитів
    await Task.Delay(3000);
    
    Console.WriteLine($"\nTotal requests: {requests.Count}");
    Console.WriteLine($"GET requests: {requests.Count(r => r.Method == "GET")}");
    Console.WriteLine($"POST requests: {requests.Count(r => r.Method == "POST")}");
    Console.WriteLine($"Failed (4xx/5xx): {requests.Count(r => r.Status >= 400)}");
    
    playwright.Dispose();
}
```

---

## Приклад 7: Підписка на події

```csharp
public async Task SubscribeToEvents()
{
    var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
    
    // Підписатися на події
    playwright.NavigationCompleted += (sender, url) =>
    {
        Console.WriteLine($"✅ Navigated to: {url}");
    };
    
    playwright.DomElementCaptured += (sender, element) =>
    {
        Console.WriteLine($"📄 DOM element: <{element.TagName}>");
    };
    
    playwright.PerformanceSnapshotCaptured += (sender, snapshot) =>
    {
        Console.WriteLine($"⚡ Performance: {snapshot.LoadTime}ms");
    };
    
    playwright.ResourceCaptured += (sender, resource) =>
    {
        Console.WriteLine($"📦 Resource: {resource.Type} - {resource.Url}");
    };
    
    playwright.StorageItemCaptured += (sender, item) =>
    {
        Console.WriteLine($"💾 Storage: {item.StorageType} - {item.Key}");
    };
    
    await playwright.InitializeAsync();
    await playwright.NavigateAsync("https://example.com");
    
    // Захопити дані (події спрацюють автоматично)
    await playwright.CaptureDomStructureAsync();
    await playwright.CapturePerformanceSnapshotAsync();
    await playwright.CaptureStorageAsync();
    await playwright.CapturePageResourcesAsync();
    
    playwright.Dispose();
}
```

---

## Приклад 8: Інтеграція з Avalonia UI

```csharp
public class MyDevToolsViewModel : ReactiveObject
{
    private readonly PlaywrightDevToolsService _playwright;
    private string _url = "https://github.com";
    private string _status = "Ready";
    
    public string Url
    {
        get => _url;
        set => this.RaiseAndSetIfChanged(ref _url, value);
    }
    
    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }
    
    public MyDevToolsViewModel()
    {
        _playwright = new PlaywrightDevToolsService(new DevToolsDataService());
        NavigateCommand = ReactiveCommand.CreateFromTask(Navigate);
        AnalyzeCommand = ReactiveCommand.CreateFromTask(Analyze);
    }
    
    public ReactiveCommand<Unit, Unit> NavigateCommand { get; }
    public ReactiveCommand<Unit, Unit> AnalyzeCommand { get; }
    
    private async Task Navigate()
    {
        Status = "Navigating...";
        await _playwright.InitializeAsync();
        await _playwright.NavigateAsync(Url);
        Status = $"Loaded: {Url}";
    }
    
    private async Task Analyze()
    {
        Status = "Analyzing...";
        
        var dom = await _playwright.CaptureDomStructureAsync();
        var perf = await _playwright.CapturePerformanceSnapshotAsync();
        var vitals = await _playwright.GetCoreWebVitalsAsync();
        
        Status = $"Analysis complete: {dom.Count} elements, {perf.LoadTime}ms load time";
    }
}
```

---

## Приклад 9: Порівняння продуктивності сайтів

```csharp
public async Task CompareWebsites()
{
    var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
    await playwright.InitializeAsync();
    
    var websites = new[] 
    { 
        "https://github.com", 
        "https://stackoverflow.com",
        "https://reddit.com"
    };
    
    foreach (var website in websites)
    {
        Console.WriteLine($"\n=== Analyzing {website} ===");
        
        await playwright.NavigateAsync(website);
        var perf = await playwright.CapturePerformanceSnapshotAsync();
        var vitals = await playwright.GetCoreWebVitalsAsync();
        
        Console.WriteLine($"Load Time: {perf.LoadTime}ms");
        Console.WriteLine($"DOM Content Loaded: {perf.DomContentLoadedTime}ms");
        Console.WriteLine($"Memory: {perf.MemoryUsed / 1024.0 / 1024.0:F2}MB");
        
        if (vitals.TryGetValue("LCP", out var lcp))
            Console.WriteLine($"LCP: {lcp:F2}ms");
        if (vitals.TryGetValue("FID", out var fid))
            Console.WriteLine($"FID: {fid:F2}ms");
        if (vitals.TryGetValue("CLS", out var cls))
            Console.WriteLine($"CLS: {cls:F4}");
    }
    
    playwright.Dispose();
}
```

---

## Приклад 10: Експорт даних в JSON

```csharp
public async Task ExportToJson()
{
    var playwright = new PlaywrightDevToolsService(new DevToolsDataService());
    await playwright.InitializeAsync();
    await playwright.NavigateAsync("https://example.com");
    
    // Захопити всі дані
    var dom = await playwright.CaptureDomStructureAsync();
    var perf = await playwright.CapturePerformanceSnapshotAsync();
    var storage = await playwright.CaptureStorageAsync();
    var resources = await playwright.CapturePageResourcesAsync();
    
    // Створити об'єкт для експорту
    var exportData = new
    {
        Url = playwright.CurrentUrl,
        CapturedAt = DateTime.UtcNow,
        Dom = new
        {
            ElementCount = dom.Count,
            Elements = dom.Take(10) // Перші 10 для прикладу
        },
        Performance = new
        {
            perf.LoadTime,
            perf.DomContentLoadedTime,
            perf.FirstPaintTime,
            MemoryMB = perf.MemoryUsed / 1024.0 / 1024.0
        },
        Storage = new
        {
            LocalStorage = storage.Count(x => x.StorageType == "localStorage"),
            SessionStorage = storage.Count(x => x.StorageType == "sessionStorage"),
            Cookies = storage.Count(x => x.StorageType == "cookies")
        },
        Resources = new
        {
            Total = resources.Count,
            Scripts = resources.Count(x => x.Type == "script"),
            Stylesheets = resources.Count(x => x.Type == "stylesheet"),
            Images = resources.Count(x => x.Type == "image")
        }
    };
    
    // Експортувати в JSON
    var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
    { 
        WriteIndented = true 
    });
    
    await File.WriteAllTextAsync("devtools-export.json", json);
    Console.WriteLine("Exported to devtools-export.json");
    
    playwright.Dispose();
}
```

---

**Всі ці приклади готові до використання!**

Детальна документація: [PLAYWRIGHT_DEVTOOLS_DOCUMENTATION.md](PLAYWRIGHT_DEVTOOLS_DOCUMENTATION.md)

