# 🔄 Система ротації баз даних історії

## 📋 Опис

Реалізована система автоматичної ротації баз даних для історії браузера та історії чату Vetale AI. Коли база даних досягає максимального розміру або кількості записів, автоматично створюється нова БД, а читання відбувається з усіх баз послідовно.

---

## 🎯 Переваги

✅ **Немає втрати даних** - стара історія зберігається в попередніх БД
✅ **Оптимізація пам'яті** - кожна БД має обмежений розмір
✅ **Прозора робота** - API залишається незмінним
✅ **Автоматична очистка** - видаляються найстаріші БД при досягненні ліміту
✅ **Масштабованість** - підтримка необмеженої кількості записів

---

## 📊 Параметри ротації

### Історія браузера (`RotatingHistoryDatabaseService`)
- **Максимальний розмір БД**: 20 MB
- **Максимум записів на БД**: 5,000 елементів
- **Максимум файлів БД**: 10 файлів
- **Патерн імен**: `history.db`, `history_1.db`, `history_2.db`, ...

### Історія чату Vetale AI (`RotatingVetaleAIChatDatabaseService`)
- **Максимальний розмір БД**: 15 MB
- **Максимум повідомлень на БД**: 3,000 повідомлень
- **Максимум файлів БД**: 10 файлів
- **Патерн імен**: `vetale_chat.db`, `vetale_chat_1.db`, `vetale_chat_2.db`, ...

---

## 🔧 Використання

### Історія браузера

```csharp
// Замість HistoryDatabaseService використовуйте RotatingHistoryDatabaseService
var historyService = new RotatingHistoryDatabaseService(
    baseDatabasePath: "C:\\BrowserData\\history.db",
    encryptionKey: "your-encryption-key"
);

// API залишається таким самим
historyService.AddOrUpdateHistoryItem("https://example.com", "Example Site");

// Отримати історію (автоматично читає з усіх БД)
var history = historyService.GetHistory(
    startDate: DateTime.Now.AddDays(-7),
    endDate: DateTime.Now,
    page: 0,
    pageSize: 50
);

// Пошук працює по всіх базах
var results = historyService.SearchHistory("example");

// Очистка працює по всіх базах
historyService.ClearHistoryOlderThan(DateTime.Now.AddMonths(-6));
```

### Історія чату Vetale AI

```csharp
// Створення сервісу
var chatHistoryService = new RotatingVetaleAIChatDatabaseService(
    baseDatabasePath: "C:\\BrowserData\\vetale_chat.db",
    encryptionKey: "your-encryption-key"
);

// Додавання повідомлень
chatHistoryService.AddMessage(
    role: "user",
    message: "Привіт, Ветале!",
    sessionId: "session-123",
    tokensUsed: null
);

chatHistoryService.AddMessage(
    role: "assistant",
    message: "Вітаю! Чим можу допомогти?",
    sessionId: "session-123",
    tokensUsed: 45
);

// Отримання останніх повідомлень для контексту
var contextMessages = chatHistoryService.GetLastMessages(
    count: 10,
    sessionId: "session-123"
);

// Отримання всіх повідомлень сесії
var sessionMessages = chatHistoryService.GetMessages(
    sessionId: "session-123",
    page: 0,
    pageSize: 50
);

// Пошук в історії чату
var searchResults = chatHistoryService.SearchMessages("Ветале");

// Очистка сесії
chatHistoryService.ClearSession("session-123");
```

---

## 🔄 Як працює ротація

### 1. **Завантаження при старті**
```
[RotatingHistory] Loaded database: history.db
[RotatingHistory] Loaded database: history_1.db
[RotatingHistory] Loaded database: history_2.db
```

### 2. **Автоматична перевірка перед додаванням**
```csharp
// Перед кожним додаванням перевіряється:
- Кількість записів >= MaxHistoryItemsPerDatabase?
- Розмір файлу >= MaxDatabaseSizeBytes?

// Якщо ТАК -> створюється нова БД
```

### 3. **Створення нової БД**
```
[RotatingHistory] Rotation needed: 5000 items in current database
[RotatingHistory] Created new database: history_3.db
```

### 4. **Автоматична очистка старих БД**
```
[RotatingHistory] Deleted old database: history.db
// Залишається тільки 10 найновіших БД
```

### 5. **Читання з усіх БД**
```csharp
// При GetHistory() або SearchHistory():
// 1. Читає з усіх БД (від новішої до старішої)
// 2. Об'єднує результати
// 3. Сортує по даті
// 4. Застосовує пагінацію
```

---

## 📁 Структура файлів

### Приклад для історії браузера:
```
BrowserData/
├── history.db          (найстаріша, буде видалена при новій ротації)
├── history_1.db
├── history_2.db
├── history_3.db
├── ...
└── history_9.db        (поточна, активна для запису)
```

### Приклад для чату VetaleAI:
```
BrowserData/
├── vetale_chat.db
├── vetale_chat_1.db
├── vetale_chat_2.db
├── ...
└── vetale_chat_9.db    (поточна, активна для запису)
```

---

## ⚙️ Інтеграція в DatabaseManager

```csharp
public class DatabaseManager
{
    private RotatingHistoryDatabaseService? _historyService;
    private RotatingVetaleAIChatDatabaseService? _chatHistoryService;
    
    public void InitializeServices()
    {
        var historyPath = Path.Combine(_dataDirectory, "history.db");
        _historyService = new RotatingHistoryDatabaseService(
            historyPath, 
            _encryptionKey
        );
        
        var chatPath = Path.Combine(_dataDirectory, "vetale_chat.db");
        _chatHistoryService = new RotatingVetaleAIChatDatabaseService(
            chatPath, 
            _encryptionKey
        );
    }
    
    public IHistoryDatabaseService GetHistoryService()
    {
        return _historyService ?? throw new InvalidOperationException("Service not initialized");
    }
    
    public IVetaleAIChatDatabaseService GetChatHistoryService()
    {
        return _chatHistoryService ?? throw new InvalidOperationException("Service not initialized");
    }
}
```

---

## 🔐 Безпека

- ✅ Всі дані зашифровані AES-256
- ✅ Шифрування застосовується до кожної БД окремо
- ✅ Той самий ключ шифрування для всіх ротованих БД
- ✅ При видаленні старих БД - файли повністю видаляються

---

## 📈 Продуктивність

### Запис
- **Швидкість**: Без змін (запис тільки в поточну БД)
- **Пам'ять**: Оптимізована (обмежений розмір кожної БД)

### Читання
- **Швидкість**: Залежить від кількості БД (максимум 10)
- **Пам'ять**: Ефективна завдяки lazy loading та пагінації
- **Оптимізація**: Читання від новіших до старіших (частіші запити швидші)

---

## 🧪 Тестування

```csharp
// Тест ротації історії
var service = new RotatingHistoryDatabaseService("test_history.db", "test-key");

// Додаємо 6000 записів (більше ніж MaxHistoryItemsPerDatabase = 5000)
for (int i = 0; i < 6000; i++)
{
    service.AddOrUpdateHistoryItem($"https://site{i}.com", $"Site {i}");
}

// Перевіряємо що створено 2 БД
// test_history.db (5000 записів) + test_history_1.db (1000 записів)

// Отримуємо всі записи
var allHistory = service.GetHistory(page: 0, pageSize: 6000);
Assert.Equal(6000, allHistory.Count);
```

---

## 🐛 Логування

Всі операції ротації логуються в консоль:

```
[RotatingHistory] Loaded database: history.db
[RotatingHistory] Loaded database: history_1.db
[RotatingHistory] Rotation needed: 5000 items in current database
[RotatingHistory] Created new database: history_2.db
[RotatingHistory] Deleted old database: history.db
[RotatingVetaleAIChat] Rotation needed: 15MB database size
[RotatingVetaleAIChat] Created new database: vetale_chat_3.db
```

---

## ✨ Майбутні покращення

- [ ] Асинхронні методи (AddMessageAsync, GetHistoryAsync)
- [ ] Compression старих БД
- [ ] Експорт/імпорт історії
- [ ] Статистика використання (кількість БД, загальний розмір)
- [ ] Налаштування параметрів ротації через конфіг

---

## 📝 Автор

VetaleBrowser Team - 2025

