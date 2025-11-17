# Оновлення WebArchive: перехід на пошук за Query

## Зміст оновлення

WebArchive сервіс було змінено з **пошуку по CDX Server API** (пошук конкретних знімків сайтів) на **простий пошуковий URL** (аналогічно до Google та YouTube).

## До і Після

### ДО (CDX API підхід)
```
Запит: "github"
↓
WebArchive робив запит до CDX Server API
↓
Повертав 10+ конкретних знімків сайтів
↓
Показував список архівних сторінок з датами
```

### ПІСЛЯ (Query підхід)
```
Запит: "github"
↓
WebArchive створює пошуковий URL
↓
Повертає ОДНЕ посилання на пошук в Web Archive
↓
Користувач переходить на web.archive.org і бачить результати
```

## Технічні зміни

### 1. WebArchiveSearchClient.cs

**Видалено:**
- ❌ CDX Server API запити
- ❌ Парсинг JSON відповідей CDX
- ❌ Фільтрація за датами (today, week ago)
- ❌ Ранжування результатів
- ❌ Методи `NormalizeQueryToUrl()`, `ParseCdxLine()`, `GetDisplayUrl()`, `GetTitleFromUrl()`
- ❌ Клас `CdxEntry`
- ❌ Using директиви: `System.Globalization`, `System.Linq`

**Додано:**
- ✅ Простий URL генератор: `https://web.archive.org/web/*/{query}`
- ✅ Один результат-посилання (як YouTube)
- ✅ Спрощена логіка без HTTP запитів

**Новий код:**
```csharp
public async Task<IReadOnlyList<UnifiedSearchResult>> SearchTodayAsync(string query, int maxResults, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(query))
        return Array.Empty<UnifiedSearchResult>();

    // Створюємо URL для пошуку в Web Archive (аналогічно до Google/YouTube)
    var webArchiveSearchUrl = $"https://web.archive.org/web/*/{Uri.EscapeDataString(query)}";
    
    // Повертаємо один результат-посилання на пошук у Web Archive
    var results = new List<UnifiedSearchResult>
    {
        new UnifiedSearchResult
        {
            Title = $"Пошук в Web Archive: \"{query}\"",
            Url = webArchiveSearchUrl,
            DisplayUrl = "web.archive.org › search",
            Snippet = $"Відкрити архівні знімки сайтів для \"{query}\" в Internet Archive.",
            Source = SearchSourceType.WebArchive,
            Timestamp = null,
            RankScore = 1,
            PageNumber = 1
        }
    };

    await Task.CompletedTask; // Для сумісності з async
    return results;
}
```

### 2. UnifiedSearchService.cs

**Змінено:**
- Параметр `maxResults` для WebArchive змінено з `10` на `1`
- Оновлено коментар: "до 10 сьогоднішніх" → "посилання на пошук"

## Порівняння з іншими сервісами

| Сервіс | Тип | Кількість результатів | URL формат |
|--------|-----|----------------------|------------|
| **Wikipedia** | API запит | 1 конкретна стаття | `https://en.wikipedia.org/wiki/...` |
| **WebArchive** (ДО) | CDX API | 10+ знімків | `https://web.archive.org/web/{timestamp}/{url}` |
| **WebArchive** (ПІСЛЯ) | Query URL | 1 посилання на пошук | `https://web.archive.org/web/*/{query}` |
| **YouTube** | Query URL | 1 посилання на пошук | `https://youtube.com/results?search_query={query}` |
| **Google** | Query URL | 1 посилання на пошук | `https://google.com/search?q={query}` |

## Приклади

### Приклад 1: Пошук "wikipedia"

**Старий підхід (CDX API):**
```
📦 Результати:
1. Wikipedia snapshot from 15 Nov 2025: wikipedia.org
2. Wikipedia snapshot from 14 Nov 2025: wikipedia.org/wiki/Main_Page
3. Wikipedia snapshot from 13 Nov 2025: en.wikipedia.org
...
```

**Новий підхід (Query URL):**
```
📦 Результат:
1. 🌐 Пошук в Web Archive: "wikipedia"
   web.archive.org › search
   Відкрити архівні знімки сайтів для "wikipedia" in Internet Archive.
```

### Приклад 2: Пошук "linux kernel"

**Старий підхід:**
- Намагався знайти URL зі словами "linux kernel"
- Часто повертав пусті результати (бо це не URL)

**Новий підхід:**
```
📦 Результат:
1. 🌐 Пошук в Web Archive: "linux kernel"
   web.archive.org › search
   Відкрити архівні знімки сайтів для "linux kernel" in Internet Archive.
```

## Переваги нового підходу

### ✅ Простота
- Немає складних HTTP запитів до CDX API
- Немає парсингу JSON
- Немає обробки помилок мережі

### ✅ Швидкість
- Миттєве створення URL (без HTTP запитів)
- Немає затримок на очікування відповіді CDX API
- Немає timeout issues

### ✅ Узгодженість
- Працює однаково з YouTube та Google
- Зрозумілий формат результату
- Передбачувана поведінка

### ✅ Надійність
- Немає залежності від CDX API
- Не може "не знайти результатів"
- Завжди повертає один результат

### ✅ Зручність
- Користувач бачить всі результати на web.archive.org
- Може використовувати всі фільтри Web Archive
- Може обрати потрібний знімок самостійно

## Недоліки (компроміси)

### ⚠️ Менше інтеграції
- Результати не показуються безпосередньо у Vetale Search
- Потрібен перехід на інший сайт

### ⚠️ Менше контролю
- Не можна відфільтрувати результати за датою у Vetale
- Не можна показати лише найрелевантніші знімки

## Вплив на користувача

### Що зміниться для користувача:

**Було:**
```
Пошук: "github"
→ Vetale показує 10 конкретних знімків GitHub з датами
→ Клік → негайний перехід до конкретного знімка
```

**Стало:**
```
Пошук: "github"
→ Vetale показує посилання "Пошук в Web Archive"
→ Клік → перехід на web.archive.org з результатами пошуку
→ Вибір потрібного знімка на Web Archive
```

### Позитив для користувача:
- ✅ Більше результатів на Web Archive (не обмежені 10)
- ✅ Всі фільтри та опції Web Archive доступні
- ✅ Швидша початкова відповідь Vetale Search

### Негатив для користувача:
- ❌ Додатковий клік для перегляду конкретного знімка
- ❌ Немає попереднього перегляду знімків у Vetale

## Майбутні покращення

🔮 Можливі варіанти:
1. **Гібридний підхід** - показувати і посилання на пошук, і топ-1 знімок з CDX API
2. **Налаштування** - дозволити користувачу обирати між підходами
3. **Інтеграція фреймів** - показувати результати Web Archive у вбудованому фреймі
4. **Кешування популярних запитів** - зберігати топові знімки для популярних сайтів

## Висновок

Зміна WebArchive на query-based підхід робить його **простішим, швидшим та надійнішим**, хоча й менш інтегрованим. Це узгоджує його з іншими зовнішніми пошуковими сервісами (YouTube, Google) і спрощує кодову базу.

**Статус:** ✅ Реалізовано та протестовано

**Дата оновлення:** 18 листопада 2025

---

**Змінені файли:**
- `VetaleBrowser.Search/Services/WebArchiveSearchClient.cs` - спрощено до query URL генератора
- `VetaleBrowser.Search/Services/UnifiedSearchService.cs` - оновлено параметри виклику WebArchive

