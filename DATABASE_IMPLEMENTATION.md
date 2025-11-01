# Database Implementation - VetaleBrowser

## Огляд
Система збереження вкладок і закладок з використанням **LiteDB** та **AES шифрування**.

## Компоненти

### 1. Моделі даних (`VetaleBrowser.Database/Models/DatabaseModels.cs`)

#### TabModel
- `Id` - унікальний ідентифікатор
- `Url` - адреса вкладки (зашифрована)
- `Title` - заголовок вкладки (зашифрований)
- `FaviconUrl` - URL іконки
- `FaviconData` - дані іконки
- `CreatedAt` - дата створення
- `LastAccessedAt` - остання дата доступу
- `SessionId` - ідентифікатор сесії
- `Order` - порядок вкладки
- `IsActive` - чи активна вкладка

#### BrowserSession
- `Id` - унікальний ідентифікатор
- `StartedAt` - час початку сесії
- `EndedAt` - час завершення сесії
- `IsCurrent` - чи поточна сесія
- `TabCount` - кількість вкладок

#### Bookmark
- `Id` - унікальний ідентифікатор
- `Url` - адреса закладки (зашифрована)
- `Title` - назва закладки (зашифрована)
- `Folder` - папка закладки
- `FaviconUrl` - URL іконки
- `FaviconData` - дані іконки
- `CreatedAt` - дата створення
- `Order` - порядок у папці

### 2. Шифрування (`DatabaseEncryptionService.cs`)

#### Особливості
- **Алгоритм**: AES-256
- **Режим**: CBC (Cipher Block Chaining)
- **Padding**: PKCS7
- **Ключ**: SHA-256 хеш від переданого рядка
- **IV**: Перші 16 байт ключа

#### Методи
```csharp
byte[] Encrypt(byte[] plainData)
byte[] Decrypt(byte[] encryptedData)
string EncryptString(string plainText)
string DecryptString(string encryptedText)
```

### 3. Сервіс бази даних (`TabDatabaseService.cs`)

#### Обмеження для запобігання переповнення
- **MaxTabsPerSession**: 1000 вкладок на сесію
- **MaxTotalTabs**: 10,000 вкладок всього
- **MaxSessions**: 100 сесій
- **MaxBookmarks**: 50,000 закладок
- **MaxDatabaseSizeBytes**: 500 MB

#### Автоматичне очищення
При досягненні максимального розміру БД:
1. Видаляються найстаріші сесії (залишається тільки 100 останніх)
2. Видаляються найстаріші вкладки
3. Виконується оптимізація БД (`Rebuild`)

#### Індекси
- `SessionId` для швидкого пошуку вкладок сесії
- `IsActive` для пошуку активних вкладок
- `LastAccessedAt` для сортування за датою
- `IsCurrent` для пошуку поточної сесії
- `Folder` для фільтрації закладок

#### API

##### Сесії
```csharp
int CreateSession()                    // Створює нову сесію
BrowserSession? GetCurrentSession()    // Отримує поточну сесію
void DeleteSession(int sessionId)      // Видаляє сесію
```

##### Вкладки
```csharp
int AddTab(int sessionId, string url, string title, bool isActive = false)
void UpdateTab(int tabId, string? url = null, string? title = null, bool? isActive = null)
List<TabModel> GetSessionTabs(int sessionId)
void DeleteTab(int tabId)
```

##### Закладки
```csharp
int AddBookmark(string url, string title, string folder = "Закладки")
List<Bookmark> GetBookmarks(string? folder = null)
void DeleteBookmark(int bookmarkId)
```

##### Статистика
```csharp
DatabaseStats GetStats()  // Повертає статистику БД
```

### 4. Конфігурація (`DatabaseConfiguration.cs`)

#### Властивості
- `DatabasePath` - шлях до файлу БД
- `EncryptionKey` - ключ шифрування
- `AutoCleanup` - автоматичне очищення
- `CleanupIntervalDays` - інтервал очищення (днів)

#### Створення
```csharp
// За замовчуванням
var config = DatabaseConfiguration.CreateDefault();

// З кастомним ключем
var config = DatabaseConfiguration.CreateWithKey("MySecretKey");
```

### 5. Глобальний менеджер (`DatabaseManager.cs`)

#### Singleton pattern
```csharp
// Отримати екземпляр
var db = DatabaseManager.Instance;

// Ініціалізація
DatabaseManager.Initialize();
DatabaseManager.Initialize(customPath, customKey);

// Додати вкладку до поточної сесії
int tabId = DatabaseManager.AddTabToCurrentSession(url, title, isActive);

// Створити нову сесію
int sessionId = DatabaseManager.CreateNewSession();

// Закрити БД
DatabaseManager.Shutdown();
```

## Використання

### Ініціалізація в App.axaml.cs
```csharp
public override void OnFrameworkInitializationCompleted()
{
    // Ініціалізуємо базу даних
    DatabaseManager.Initialize();
    
    // ... решта коду
    
    base.OnFrameworkInitializationCompleted();
}
```

### Додавання вкладки
```csharp
// Додати вкладку до поточної сесії
var tabId = DatabaseManager.AddTabToCurrentSession(
    url: "https://example.com",
    title: "Example Website",
    isActive: true
);
```

### Робота з закладками
```csharp
var db = DatabaseManager.Instance;

// Додати закладку
int bookmarkId = db.AddBookmark(
    url: "https://example.com",
    title: "Example",
    folder: "Робота"
);

// Отримати всі закладки
var allBookmarks = db.GetBookmarks();

// Отримати закладки з папки
var workBookmarks = db.GetBookmarks("Робота");

// Видалити закладку
db.DeleteBookmark(bookmarkId);
```

### Статистика
```csharp
var stats = DatabaseManager.Instance.GetStats();
Console.WriteLine($"Всього вкладок: {stats.TotalTabs}");
Console.WriteLine($"Всього сесій: {stats.TotalSessions}");
Console.WriteLine($"Розмір БД: {stats.DatabaseSizeBytes / 1024 / 1024} MB");
Console.WriteLine($"Використання: {stats.UsagePercentage}%");
```

## Сторінки UI

### BookmarksPage
- Перегляд всіх закладок
- Фільтрація по папках (Всі, Закладки, Робота, Особисті, Новини)
- Додавання нових закладок через AddBookmarkWindow
- Видалення закладок
- Відкриття закладок в браузері

### HistoryPage
- Перегляд історії вкладок по сесіях
- Статистика використання БД
- Видалення окремих вкладок
- Видалення цілих сесій
- Очищення історії
- Оновлення даних

## Безпека

### Шифрування
- Всі URL та заголовки вкладок шифруються AES-256
- Ключ генерується на основі машини та користувача
- Для продакшн рекомендується використовувати Windows DPAPI

### Рекомендації для продакшн
1. Використовувати `ProtectedData.Protect()` для зберігання ключа шифрування
2. Додати можливість резервного копіювання БД
3. Реалізувати логування помилок
4. Додати валідацію даних перед збереженням
5. Розглянути можливість компресії даних

## Структура файлів

```
VetaleBrowser.Database/
├── Models/
│   └── DatabaseModels.cs          # Моделі даних
├── Services/
│   ├── DatabaseEncryptionService.cs    # Шифрування
│   ├── ITabDatabaseService.cs          # Інтерфейс
│   └── TabDatabaseService.cs           # Основний сервіс
└── DatabaseConfiguration.cs        # Конфігурація

VetaleBrowser.Core/Scripts/GlobalManagers/
└── DatabaseManager.cs              # Глобальний менеджер

VetaleBrowser.UI/Pages/
├── BookmarksPage.axaml            # UI закладок
├── BookmarksPage.axaml.cs
├── HistoryPage.axaml              # UI історії
└── HistoryPage.axaml.cs
```

## Шлях до БД

**За замовчуванням**: `%APPDATA%\VetaleBrowser\Data\browser.db`

**Windows**: `C:\Users\[Username]\AppData\Roaming\VetaleBrowser\Data\browser.db`

## Ліцензія
Частина проекту VetaleBrowser

