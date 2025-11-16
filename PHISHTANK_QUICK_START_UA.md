# Швидкий старт - Система безпеки PhishTank

## Що зроблено ✅

Реалізовано API інтеграцію з PhishTank для перевірки безпеки веб-сайтів. Іконка щита в адресній строці змінює колір залежно від безпеки сайту.

## Структура файлів

```
VetaleBrowser.Search/
├── Models/
│   ├── SecurityStatus.cs          // Enum статусів безпеки
│   └── SecurityCheckResult.cs     // Модель результату перевірки
├── Services/
│   ├── ISecurityCheckService.cs   // Інтерфейс сервісу
│   └── PhishTankSecurityService.cs // Реалізація PhishTank API

VetaleBrowser.UI/
└── Еlements/
    ├── NavigationBar.axaml        // UI іконки безпеки
    └── NavigationBar.axaml.cs     // Логіка перевірки

MainWindow.axaml.cs                // Ініціалізація сервісу
```

## Що робить система

### Іконка безпеки показує:

🟢 **Зелений щит** - Сайт безпечний  
🔴 **Червоний щит** - ⚠️ ФІШИНГ! Небезпечний сайт  
🟡 **Жовтий щит** - Перевірка в процесі...  
🟠 **Помаранчевий щит** - Помилка перевірки  
⚪ **Сірий щит** - Статус невідомий  

### Автоматична перевірка:
- При введенні URL в адресну строку
- При кліку на посилання
- При зміні сторінки

## ОБОВ'ЯЗКОВО! Налаштування API ключа

1. **Отримайте API ключ:**
   - Відкрийте https://phishtank.com
   - Зареєструйтесь
   - Отримайте безкоштовний API ключ

2. **Встановіть ключ:**
   
   Відкрийте файл:
   ```
   VetaleBrowser.Search/Services/PhishTankSecurityService.cs
   ```
   
   Знайдіть рядок 24:
   ```csharp
   private const string ApiKey = "YOUR_API_KEY_HERE";
   ```
   
   Замініть на ваш ключ:
   ```csharp
   private const string ApiKey = "abcd1234efgh5678"; // ваш ключ
   ```

## Як працює

```
Користувач вводить URL
        ↓
NavigationBar отримує текст
        ↓
CheckUrlSecurityAsync() викликається
        ↓
PhishTankSecurityService перевіряє URL
        ↓
Результат → SecurityStatus
        ↓
UpdateSecurityIcon() змінює колір
```

## Кешування

- Результати зберігаються **30 хвилин**
- Економить API запити
- Можна очистити: `_securityCheckService.ClearCache()`

## Які сайти НЕ перевіряються

✅ Локальні сайти (завжди безпечні):
- `localhost`
- `127.0.0.1`
- `file://`
- `vetale://` (внутрішні сторінки)

## Логи в консолі

```
[Security] ✓ Безпечний: https://google.com - Безпечний сайт
[Security] ⚠️ УВАГА! Фішинговий сайт: https://bad-site.com
[Security] Помилка перевірки: timeout
```

## Тестування

### Тест безпечного сайту:
1. Введіть: `google.com`
2. Очікується: 🟢 Зелений щит

### Тест фішингу:
1. Знайдіть тестовий фішинговий URL на phishtank.com
2. Введіть URL
3. Очікується: 🔴 Червоний щит + попередження в консолі

### Тест локального:
1. Введіть: `localhost:3000`
2. Очікується: 🟢 Зелений щит (локальний ресурс)

## Важливо!

✅ **БЕЗ API ключа** - працює повністю автономно

✅ **Офлайн режим** - перевірка без інтернету

✅ **Необмежені перевірки** - без лімітів

⚠️ **Можливі помилки**: Евристика може давати false positives

⚠️ **Локальна база**: Потрібно вручну додавати нові домени

⚠️ **Не блокує навігацію**: Тільки показує попередження

## Код організації

Використано namespace **VetaleBrowser.Search**:
- Моделі в `Models/`
- Сервіси в `Services/`
- Ізоляція від UI
- Можливість повторного використання

## Приклад коду

### Перевірка URL:
```csharp
var result = await _securityCheckService.CheckUrlAsync("https://example.com");
if (result.Status == SecurityStatus.Dangerous)
{
    // Показати попередження
}
```

### Встановлення сервісу:
```csharp
navigationBar.SetSecurityCheckService(new PhishTankSecurityService());
```

### Ручна перевірка:
```csharp
await navigationBar.CheckCurrentUrlSecurityAsync();
```

## Що далі?

Можливі покращення:
1. ✨ Діалог попередження для користувача
2. ✨ Блокування небезпечних сайтів (опційно)
3. ✨ Google Safe Browsing інтеграція
4. ✨ Налаштування в Settings
5. ✨ Білий список довірених сайтів

---

**Створено:** 15 листопада 2025  
**Версія:** 1.0  
**API:** PhishTank.com  

