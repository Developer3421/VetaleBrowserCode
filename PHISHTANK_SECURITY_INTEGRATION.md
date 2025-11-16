# PhishTank Security Integration - Інтеграція безпеки з PhishTank

## Огляд

Реалізовано систему перевірки безпеки веб-сайтів через PhishTank API. Іконка безпеки в адресній строці змінює колір залежно від статусу перевірки сайту.

## Компоненти

### 1. Моделі даних (VetaleBrowser.Search/Models/)

#### SecurityStatus.cs
Enum для статусу безпеки:
- `Unknown` - невідомий статус
- `Safe` - безпечний сайт (зелений)
- `Checking` - перевірка в процесі (жовтий)
- `Dangerous` - небезпечний сайт (червоний)
- `Error` - помилка перевірки (помаранчевий)

#### SecurityCheckResult.cs
Результат перевірки URL:
```csharp
public class SecurityCheckResult
{
    public string Url { get; set; }
    public SecurityStatus Status { get; set; }
    public string Description { get; set; }
    public bool IsPhishing { get; set; }
    public DateTime CheckedAt { get; set; }
    public PhishTankDetails? Details { get; set; }
}
```

### 2. Сервіси (VetaleBrowser.Search/Services/)

#### ISecurityCheckService.cs
Інтерфейс для перевірки безпеки:
```csharp
public interface ISecurityCheckService
{
    Task<SecurityCheckResult> CheckUrlAsync(string url);
    void ClearCache();
}
```

#### PhishTankSecurityService.cs
Реалізація через PhishTank API:
- Перевіряє URL через API PhishTank
- Кешує результати на 30 хвилин
- Локальні URL (localhost, file://, vetale://) вважаються безпечними
- При помилці API не блокує доступ

**ВАЖЛИВО**: Потрібно отримати API ключ на https://phishtank.com і замінити `YOUR_API_KEY_HERE` в константі `ApiKey`

### 3. UI компоненти

#### NavigationBar.axaml
Додано іконку безпеки:
```xml
<Border x:Name="PART_SecurityIcon">
    <Path x:Name="PART_SecurityPath" 
          Fill="#4CAF50" 
          Data="M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z"/>
</Border>
```

#### NavigationBar.axaml.cs
Властивості і методи:
- `SecurityStatus` - поточний статус безпеки
- `SetSecurityCheckService()` - встановлення сервісу
- `CheckUrlSecurityAsync()` - перевірка URL
- `UpdateSecurityIcon()` - оновлення іконки

### 4. Інтеграція в MainWindow

```csharp
private readonly ISecurityCheckService _securityCheckService = 
    new PhishTankSecurityService();

// При ініціалізації NavigationBar:
navigationBar.SetSecurityCheckService(_securityCheckService);
```

## Колірна індикація

| Статус | Колір | Іконка | Tooltip |
|--------|-------|--------|---------|
| Safe | Зелений (#4CAF50) | Щит | ✓ Безпечний сайт |
| Dangerous | Червоний (#F44336) | Щит з ! | ⚠️ НЕБЕЗПЕЧНИЙ САЙТ (фішинг)! |
| Checking | Жовтий (#FFC107) | Щит | ⏳ Перевірка безпеки... |
| Error | Помаранчевий (#FF9800) | Щит | ⚠ Помилка перевірки |
| Unknown | Сірий (#9E9E9E) | Щит | ? Статус невідомий |

## Як працює

1. **Автоматична перевірка**: При введенні URL в адресну строку і натисканні Enter
2. **При зміні URL**: Коли браузер переходить на новий сайт
3. **Кешування**: Результати зберігаються 30 хвилин
4. **Асинхронна перевірка**: Не блокує UI
5. **Відміна**: Попередня перевірка скасовується при новому запиті

## Налаштування PhishTank API

1. Зареєструйтесь на https://phishtank.com
2. Отримайте API ключ
3. Відкрийте `PhishTankSecurityService.cs`
4. Замініть:
```csharp
private const string ApiKey = "YOUR_API_KEY_HERE";
```
на ваш ключ:
```csharp
private const string ApiKey = "ВАШ_API_КЛЮЧ";
```

## Приклад використання

```csharp
// Автоматично викликається при навігації
navigationBar.Url = "https://example.com";
// -> автоматична перевірка безпеки

// Ручна перевірка поточного URL
await navigationBar.CheckCurrentUrlSecurityAsync();

// Очистити кеш
_securityCheckService.ClearCache();
```

## Логування

Всі події логуються в Debug:
```
[Security] ✓ Безпечний: https://google.com - Безпечний сайт
[Security] ⚠️ УВАГА! Фішинговий сайт: https://phishing-site.com
[PhishTank] Помилка перевірки: timeout
```

## Обмеження і особливості

1. **API ліміти**: PhishTank має ліміти на кількість запитів
2. **Локальні сайти**: Не перевіряються (завжди Safe)
3. **Помилки API**: Не блокують доступ до сайту
4. **Кешування**: Економить запити до API
5. **Таймаут**: 10 секунд на запит

## Безпека

- Сервіс НЕ блокує навігацію при виявленні фішингу
- Тільки показує попередження в консолі та іконку
- Рішення про продовження перегляду приймає користувач
- При помилках вважає сайт безпечним (fail-safe)

## Подальші покращення

1. ✅ **Реалізовано**: Локальна база фішингових доменів
2. ✅ **Реалізовано**: Евристичний аналіз підозрілих ознак
3. 🔄 **TODO**: Діалог попередження користувачу
4. 🔄 **TODO**: Автоматичне оновлення бази доменів
5. 🔄 **TODO**: Інтеграція з Google Safe Browsing (без ключа)
6. 🔄 **TODO**: Історія перевірок
7. 🔄 **TODO**: Налаштування чутливості в Settings
8. 🔄 **TODO**: Білий список довірених сайтів

## Переваги підходу без API

✅ **Працює офлайн** - не потрібен інтернет для локальних перевірок  
✅ **Без ключа** - не треба реєструватися  
✅ **Без лімітів** - необмежена кількість перевірок  
✅ **Приватність** - URL не надсилаються на сторонні сервери  
✅ **Швидкість** - локальні перевірки миттєві  
✅ **Розширюваність** - можна додавати свої правила  

## Недоліки підходу

⚠️ **Обмежена база** - тільки відомі домени  
⚠️ **False positives** - можливі помилкові спрацювання  
⚠️ **False negatives** - нові фішинги можуть пропуститися  
⚠️ **Потребує оновлень** - база має оновлюватися вручну

