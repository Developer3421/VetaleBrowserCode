# 🔄 Database Rotation - Quick Start

## 📦 Що створено:

### 1. **Для історії браузера:**
- `RotatingHistoryDatabaseService.cs` - Сервіс з автоматичною ротацією БД

### 2. **Для чату VetaleAI:**
- `VetaleAIChatMessage.cs` - Модель повідомлення чату
- `IVetaleAIChatDatabaseService.cs` - Інтерфейс сервісу чату
- `VetaleAIChatDatabaseService.cs` - Базовий сервіс чату
- `RotatingVetaleAIChatDatabaseService.cs` - Сервіс чату з ротацією

---

## 🚀 Швидкий старт

### Крок 1: Використання для історії браузера

Знайдіть де створюється `HistoryDatabaseService` та замініть на:

```csharp
// БУЛО:
var historyService = new HistoryDatabaseService(dbPath, encryptionKey);

// СТАЛО:
var historyService = new RotatingHistoryDatabaseService(dbPath, encryptionKey);
```

### Крок 2: Використання для чату VetaleAI (коли буде реалізовано)

```csharp
var chatService = new RotatingVetaleAIChatDatabaseService(
    "C:\\BrowserData\\vetale_chat.db",
    encryptionKey
);

// Додавання повідомлень
chatService.AddMessage("user", "Привіт!", "session-1");
chatService.AddMessage("assistant", "Вітаю!", "session-1");

// Отримання контексту для AI
var context = chatService.GetLastMessages(10, "session-1");
```

---

## ✨ Ключові фічі:

✅ **Автоматична ротація** при досягненні ліміту (20MB / 5000 записів)
✅ **Читання з усіх БД** прозоро для користувача
✅ **Автоматична очистка** старих БД (максимум 10 файлів)
✅ **Той самий API** - сумісність з існуючим кодом
✅ **AES-256 шифрування** для всіх БД

---

## 📖 Детальна документація:

Дивіться: `DATABASE_ROTATION_SYSTEM.md`

