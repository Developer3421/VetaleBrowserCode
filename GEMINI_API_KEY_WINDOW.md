# API Keys Database & Configuration Windows

## Огляд
Реалізовано повноцінну систему збереження та конфігурації API ключів з:
- **LiteDB база даних** з AES шифруванням для безпечного зберігання ключів
- **Універсальне вікно ApiKeyConfigWindow** для будь-якого API сервісу
- **Автоматичний показ вікон** при першому використанні функцій пошуку

## База даних API ключів

### Файли
- **Модель:** `VetaleBrowser.Database/Models/DatabaseModels.cs` - клас `ApiKeyItem`
- **Інтерфейс:** `VetaleBrowser.Database/Services/IApiKeysService.cs`
- **Сервіс:** `VetaleBrowser.Database/Services/ApiKeysService.cs`
- **Шлях до БД:** `%AppData%/VetaleBrowser/Data/api_keys.db`

### Модель ApiKeyItem
```csharp
public class ApiKeyItem
{
    public int Id { get; set; }
    public string ServiceId { get; set; }      // "gemini", "pexels", "unsplash", "youtube"
    public string ServiceName { get; set; }    // "Google Gemini", "Pexels", etc.
    public string EncryptedApiKey { get; set; } // AES-256 encrypted
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int SuccessfulRequestsCount { get; set; }
    public int FailedRequestsCount { get; set; }
}
```

### Константи сервісів (ApiServiceIds)
```csharp
public static class ApiServiceIds
{
    public const string Gemini = "gemini";
    public const string Pexels = "pexels";
    public const string Unsplash = "unsplash";
    public const string YouTube = "youtube";
}
```

## Універсальне вікно ApiKeyConfigWindow

### Файли
- `VetaleBrowser.UI/Windows/ApiKeyConfigWindow.axaml`
- `VetaleBrowser.UI/Windows/ApiKeyConfigWindow.axaml.cs`

### Попередньо налаштовані конфігурації
```csharp
// Gemini AI
var result = await ApiKeyConfigWindow.ShowGeminiConfigAsync(parentWindow);

// Pexels (пошук зображень)
var result = await ApiKeyConfigWindow.ShowPexelsConfigAsync(parentWindow);

// Unsplash (пошук зображень)
var result = await ApiKeyConfigWindow.ShowUnsplashConfigAsync(parentWindow);

// YouTube (пошук відео)
var result = await ApiKeyConfigWindow.ShowYouTubeConfigAsync(parentWindow);
```

### Результат діалогу
```csharp
public class ApiKeyConfigResult
{
    public bool Saved { get; set; }      // Ключ збережено
    public bool Skipped { get; set; }    // Користувач пропустив
    public bool Cancelled { get; set; } // Вікно закрито
    public string? ApiKey { get; set; }
    public ApiServiceType ServiceType { get; set; }
}
```

## Автоматичний показ вікон

### Пошук зображень (ImageSearchResultsView)
При першому пошуку зображень, якщо немає ключів Pexels/Unsplash:
1. Показується вікно для Pexels API
2. Показується вікно для Unsplash API
3. Сервіс переініціалізується з новими ключами

### Gemini Chat (GeminiChatPanel)
При помилці "API_KEY_REQUIRED":
1. Показується вікно для Gemini API
2. Після введення ключа запит повторюється автоматично

## Пріоритет завантаження ключів

### Gemini
1. Пам'ять (_customApiKey)
2. База даних (api_keys.db)
3. Змінна середовища GEMINI_API_KEY
4. Дефолтний ключ

### Пошук зображень
1. База даних (api_keys.db)
2. Змінні середовища VETALE_PEXELS_API_KEY, VETALE_UNSPLASH_ACCESS_KEY
3. MockImageSearchService (демо-режим)

## Безпека
- Всі API ключі шифруються AES-256
- Ключ шифрування генерується унікально для кожного користувача/машини
- Ключі ніколи не передаються на зовнішні сервери (окрім відповідних API)

## Інтеграція в DatabaseServicesFactory
```csharp
// Отримання сервісу
var apiKeysService = DatabaseServicesFactory.GetApiKeysService();

// Збереження ключа
await apiKeysService.SetApiKeyAsync("gemini", "Google Gemini", "your-api-key");

// Отримання ключа
var geminiKey = await apiKeysService.GetGeminiApiKeyAsync();
var pexelsKey = await apiKeysService.GetPexelsApiKeyAsync();
```

