# 🤖 Інтеграція Google Gemini API для AI-підсумування

## 📋 Огляд

Реалізовано інтеграцію з **Google Gemini API** - потужним AI від Google для генерації підсумків пошукових запитів у Vetale Browser.

### ✨ Переваги Gemini API

- ✅ **БЕЗКОШТОВНО** - 15 запитів/хвилину, 1М токенів/місяць, 1500 запитів/день
- 🚀 **Швидко** - Gemini 1.5 Flash надзвичайно швидкий
- 🎯 **Якісно** - Найновіша модель від Google з високою точністю
- 🌐 **Багатомовність** - Відмінна підтримка української мови
- 🛡️ **Безпека** - Вбудовані фільтри безпечного контенту

---

## 🔑 Як отримати API ключ (БЕЗКОШТОВНО)

### Крок 1: Перейдіть на AI Studio
Відкрийте: **https://aistudio.google.com/app/apikey**

### Крок 2: Увійдіть в акаунт Google
Використайте ваш Google акаунт (Gmail)

### Крок 3: Створіть API ключ
1. Натисніть **"Get API key"** або **"Create API key"**
2. Виберіть існуючий проект або створіть новий
3. Натисніть **"Create API key in existing project"**
4. Скопіюйте згенерований ключ (виглядає як `AIzaSy...`)

### Крок 4: Вставте ключ у код
Відкрийте `GeminiAiSummaryService.cs` та знайдіть рядок:
```csharp
private const string ApiKey = "YOUR_GEMINI_API_KEY_HERE";
```

Замініть на:
```csharp
private const string ApiKey = "AIzaSyВАШ_КЛЮЧ_ТУТ";
```

**АБО** встановіть змінну середовища:
```powershell
[System.Environment]::SetEnvironmentVariable('GEMINI_API_KEY', 'AIzaSyВАШ_КЛЮЧ_ТУТ', 'User')
```

---

## 📊 Доступні моделі

### 1. **gemini-1.5-flash** (Рекомендовано ✅)
- **Швидкість**: ⚡⚡⚡⚡⚡ Найшвидша
- **Якість**: ⭐⭐⭐⭐ Висока
- **Використання**: Для більшості задач
- **Ліміти**: 15 RPM, 1M TPM, 1500 RPD

### 2. **gemini-1.5-pro**
- **Швидкість**: ⚡⚡⚡ Середня
- **Якість**: ⭐⭐⭐⭐⭐ Максимальна
- **Використання**: Складні аналітичні задачі
- **Ліміти**: 2 RPM, 32K TPM

### 3. **gemini-1.0-pro**
- **Швидкість**: ⚡⚡⚡⚡ Швидка
- **Якість**: ⭐⭐⭐ Хороша
- **Використання**: Стара, але стабільна версія
- **Ліміти**: 15 RPM

**Щоб змінити модель**, відредагуйте:
```csharp
private const string ModelName = "gemini-1.5-flash"; // або "gemini-1.5-pro"
```

---

## 🚀 Використання в коді

### Базове використання

```csharp
using VetaleBrowser.VetaleBrowser.Search.Services;

// Створення сервісу
var aiService = new GeminiAiSummaryService();

// Генерація підсумку
var summary = await aiService.GenerateSummaryAsync("як приготувати борщ");

// Перевірка результату
if (summary.State == AiSummaryState.Ready)
{
    Console.WriteLine($"AI підсумок: {summary.SummaryText}");
    Console.WriteLine($"Впевненість: {summary.Confidence:P0}");
}
else if (summary.State == AiSummaryState.Error)
{
    Console.WriteLine($"Помилка: {summary.ErrorMessage}");
}

// Не забудьте звільнити ресурси
aiService.Dispose();
```

### З обробкою помилок

```csharp
var aiService = new GeminiAiSummaryService();

try
{
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var summary = await aiService.GenerateSummaryAsync(query, cts.Token);
    
    switch (summary.State)
    {
        case AiSummaryState.Ready:
            // Успішно згенеровано
            DisplaySummary(summary.SummaryText);
            break;
            
        case AiSummaryState.Error:
            // Помилка API
            LogError(summary.ErrorMessage);
            break;
            
        case AiSummaryState.NoSummary:
            // Не вдалося згенерувати (наприклад, блок безпеки)
            ShowWarning(summary.ErrorMessage);
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Критична помилка: {ex.Message}");
}
finally
{
    aiService.Dispose();
}
```

### Dependency Injection (рекомендовано)

```csharp
// У Program.cs або Startup.cs
services.AddSingleton<IAiSummaryService, GeminiAiSummaryService>();

// У вашому класі
public class SearchViewModel
{
    private readonly IAiSummaryService _aiService;
    
    public SearchViewModel(IAiSummaryService aiService)
    {
        _aiService = aiService;
    }
    
    public async Task LoadSummaryAsync(string query)
    {
        var summary = await _aiService.GenerateSummaryAsync(query);
        // ...
    }
}
```

---

## 🔧 Налаштування параметрів

### Температура (Temperature)
Контролює креативність відповідей:
```csharp
temperature = 0.7  // 0.0 = детерміновано, 1.0 = креативно
```
- **0.0-0.3**: Точні, фактичні відповіді
- **0.4-0.7**: Збалансовано (рекомендовано)
- **0.8-1.0**: Креативні, варіативні відповіді

### Максимальна довжина (maxOutputTokens)
```csharp
maxOutputTokens = 300  // ~200-250 слів
```

### Top-K та Top-P
```csharp
topK = 40      // Кількість найкращих токенів для вибірки
topP = 0.95    // Nucleus sampling (рекомендовано 0.9-0.95)
```

---

## 🛡️ Безпека та фільтри

Gemini автоматично фільтрує:
- ❌ Harassment (Домагання)
- ❌ Hate Speech (Мова ненависті)
- ❌ Sexually Explicit (Сексуальний контент)
- ❌ Dangerous Content (Небезпечний контент)

Якщо контент заблоковано:
```csharp
if (summary.ErrorMessage?.Contains("blocked by safety") == true)
{
    // Запит не пройшов фільтри безпеки
}
```

---

## 📈 Ліміти та квоти

### Безкоштовний рівень (Free Tier)

| Модель | RPM* | TPM** | RPD*** |
|--------|------|-------|--------|
| gemini-1.5-flash | 15 | 1,000,000 | 1,500 |
| gemini-1.5-pro | 2 | 32,000 | - |
| gemini-1.0-pro | 15 | - | - |

**RPM* = Requests Per Minute (запитів на хвилину)
**TPM** = Tokens Per Minute (токенів на хвилину)
**RPD*** = Requests Per Day (запитів на день)

### Обробка помилки "Too Many Requests"

```csharp
if (summary.ErrorMessage?.Contains("ліміт запитів") == true)
{
    // Зачекайте 60 секунд та повторіть
    await Task.Delay(60000);
    summary = await aiService.GenerateSummaryAsync(query);
}
```

---

## 🐛 Діагностика проблем

### Проблема: "API ключ не налаштовано"

**Рішення:**
1. Перевірте, чи вставили ключ у `GeminiAiSummaryService.cs`
2. АБО встановіть змінну `GEMINI_API_KEY`
3. Перезапустіть додаток

### Проблема: "Невірний API ключ" (401/403)

**Рішення:**
1. Перевірте, чи правильно скопіювали ключ (повністю, без пробілів)
2. Переконайтеся, що ключ починається з `AIzaSy`
3. Створіть новий ключ на https://aistudio.google.com/app/apikey

### Проблема: "Перевищено ліміт запитів" (429)

**Рішення:**
1. Зачекайте 1 хвилину
2. Зменшіть частоту запитів
3. Розгляньте можливість кешування результатів

### Проблема: "Запит заблоковано фільтрами безпеки"

**Рішення:**
1. Це нормально для деяких запитів
2. Gemini захищає від небезпечного контенту
3. Переформулюйте запит більш нейтрально

---

## 📝 Приклади запитів та відповідей

### Приклад 1: Пошук рецепту
**Запит:** `"як приготувати борщ"`

**Відповідь Gemini:**
```
Користувач шукає рецепт традиційного українського борщу. Найкраще буде знайти 
покрокові рецепти з фото або відео, де показано як підготувати інгредієнти, 
правильно нарізати овочі та як довго варити. Рекомендується шукати автентичні 
українські рецепти.
```

### Приклад 2: Технічне питання
**Запит:** `"як встановити node.js на windows"`

**Відповідь Gemini:**
```
Користувач хоче встановити Node.js на комп'ютер з Windows. Найкращим рішенням 
буде завантажити офіційний інсталятор з nodejs.org, вибрати LTS версію для 
стабільності, та слідувати інструкціям майстра встановлення. Після встановлення 
варто перевірити версію командою node --version у командному рядку.
```

### Приклад 3: Загальна інформація
**Запит:** `"історія України"`

**Відповідь Gemini:**
```
Користувач цікавиться історією України - це широка тема, що охоплює тисячоліття 
від Трипільської культури до сучасності. Рекомендується шукати енциклопедичні 
статті, документальні відео або академічні джерела. Можливо, варто уточнити 
конкретний історичний період.
```

---

## 🔄 Порівняння з іншими AI сервісами

| Характеристика | Gemini Flash | OpenRouter | Qwen |
|----------------|--------------|------------|------|
| **Швидкість** | ⚡⚡⚡⚡⚡ | ⚡⚡⚡ | ⚡⚡⚡⚡ |
| **Якість (UA)** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Безкоштовно** | ✅ 1M токенів | ✅ Обмежено | ✅ Обмежено |
| **RPM ліміт** | 15 | Варіюється | Варіюється |
| **Підтримка UA** | Відмінна | Хороша | Середня |
| **Налаштування** | Проста | Середня | Середня |

**Рекомендація:** Gemini Flash - найкращий вибір для українськомовних запитів!

---

## 🎯 Best Practices

### 1. Кешування результатів
```csharp
private readonly Dictionary<string, AiSearchSummary> _cache = new();

public async Task<AiSearchSummary> GetSummaryAsync(string query)
{
    if (_cache.TryGetValue(query, out var cached))
    {
        if ((DateTime.UtcNow - cached.GeneratedAt).TotalMinutes < 30)
            return cached;
    }
    
    var summary = await _aiService.GenerateSummaryAsync(query);
    _cache[query] = summary;
    return summary;
}
```

### 2. Обробка timeout
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var summary = await aiService.GenerateSummaryAsync(query, cts.Token);
```

### 3. Retry логіка
```csharp
for (int i = 0; i < 3; i++)
{
    var summary = await aiService.GenerateSummaryAsync(query);
    
    if (summary.State == AiSummaryState.Ready)
        return summary;
        
    if (summary.ErrorMessage?.Contains("ліміт") == true)
        await Task.Delay(60000); // Зачекати 1 хв
    else
        await Task.Delay(1000 * (i + 1)); // Exponential backoff
}
```

### 4. Логування
```csharp
// Gemini автоматично логує в Debug консоль
// Всі запити та відповіді видимі через System.Diagnostics.Debug
```

---

## 📚 Додаткові ресурси

- **Офіційна документація**: https://ai.google.dev/docs
- **AI Studio**: https://aistudio.google.com/
- **API Reference**: https://ai.google.dev/api/rest
- **Pricing**: https://ai.google.dev/pricing
- **Playground**: https://aistudio.google.com/app/prompts/new_chat

---

## ✅ Чеклист інтеграції

- [ ] Отримано API ключ з AI Studio
- [ ] Вставлено ключ у `GeminiAiSummaryService.cs` або змінну середовища
- [ ] Перезапущено додаток
- [ ] Протестовано базовий запит
- [ ] Перевірено логи в Debug консолі
- [ ] Налаштовано обробку помилок
- [ ] Додано кешування (опціонально)
- [ ] Налаштовано retry логіку (опціонально)

---

## 🎉 Готово!

Тепер ваш Vetale Browser має **потужний AI-підсумування** на базі Google Gemini!

**Автор:** VetaleBrowser Team  
**Дата:** 16 листопада 2025  
**Версія:** 1.0

---

## 📞 Підтримка

Якщо виникли проблеми:
1. Перевірте Debug консоль - усі логи там
2. Перевірте чеклист вище
3. Переконайтеся, що API ключ правильний
4. Перевірте інтернет-з'єднання

**Успішної роботи з Gemini AI! 🚀**

