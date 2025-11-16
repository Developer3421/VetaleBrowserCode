# 🔑 Як отримати та вставити OpenRouter API ключ

## ✅ OpenRouter - БЕЗКОШТОВНІ AI моделі!

OpenRouter надає доступ до багатьох безкоштовних AI моделей через єдиний API.

### 📋 Покрокова інструкція:

## Крок 1: Створіть акаунт на OpenRouter

1. **Відкрийте:** https://openrouter.ai/
2. **Натисніть "Sign In"** (праворуч вгорі)
3. **Оберіть спосіб входу:**
   - 🔵 Через Google
   - ⚫ Через GitHub
   - 📧 Через Email

## Крок 2: Отримайте API ключ

1. **Після входу перейдіть:** https://openrouter.ai/keys
   - Або: натисніть своє ім'я → "API Keys"
   
2. **Натисніть "Create Key"**

3. **Дайте назву ключу** (наприклад, "Vetale Browser")

4. **Скопіюйте ключ!**
   ```
   sk-or-v1-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
   ```
   ⚠️ **ВАЖЛИВО:** Ключ показується тільки один раз! Збережіть його!

## Крок 3: Вставте ключ у код

### Варіант А: Безпосередньо в коді (швидко)

Відкрийте файл:
```
VetaleBrowser.Search\Services\QwenAiSummaryService.cs
```

Знайдіть рядок (біля рядка 27):
```csharp
private const string ApiKey = "YOUR_API_KEY_HERE";
```

Замініть на:
```csharp
private const string ApiKey = "sk-or-v1-ваш-ключ-тут";
```

**Приклад:**
```csharp
private const string ApiKey = "sk-or-v1-1234567890abcdefghijklmnopqrstuvwxyz";
```

### Варіант Б: Через змінну середовища (безпечніше)

#### Windows:

1. **Відкрийте PowerShell від адміністратора**

2. **Встановіть змінну для поточного користувача:**
```powershell
[Environment]::SetEnvironmentVariable("OPENROUTER_API_KEY", "sk-or-v1-ваш-ключ", "User")
```

3. **Перезапустіть Visual Studio** (щоб підхопило змінну)

#### Альтернативний спосіб (тільки для поточної сесії):

У Visual Studio перед запуском:
```powershell
$env:OPENROUTER_API_KEY = "sk-or-v1-ваш-ключ"
dotnet run
```

## 🎯 Безкоштовні моделі на OpenRouter

В коді вже налаштовано на використання безкоштовної моделі. Можете обрати іншу:

```csharp
// Знайдіть рядок (біля рядка 23):
private const string ModelName = "meta-llama/llama-3.2-3b-instruct:free";

// Замініть на одну з цих (всі БЕЗКОШТОВНІ):
```

### 1. **Meta Llama 3.2** (за замовчуванням) ⭐
```csharp
private const string ModelName = "meta-llama/llama-3.2-3b-instruct:free";
```
✅ Швидка, якісна, добре працює з українською  
✅ 3 мільярди параметрів  
✅ Контекст: 128K tokens

### 2. **Google Gemma 2**
```csharp
private const string ModelName = "google/gemma-2-9b-it:free";
```
✅ Від Google  
✅ 9 мільярдів параметрів (більша модель)  
✅ Чудова якість відповідей

### 3. **Microsoft Phi-3**
```csharp
private const string ModelName = "microsoft/phi-3-mini-128k-instruct:free";
```
✅ Від Microsoft  
✅ Дуже швидка  
✅ Контекст: 128K tokens

### 4. **Qwen 2** (оригінальна модель)
```csharp
private const string ModelName = "qwen/qwen-2-7b-instruct:free";
```
✅ Та сама Qwen модель, що була раніше  
✅ 7 мільярдів параметрів  
✅ Добре працює з багатьма мовами

## 🧪 Тестування

### Швидкий тест через PowerShell:

```powershell
$apiKey = "sk-or-v1-ваш-ключ"
$body = @{
    model = "meta-llama/llama-3.2-3b-instruct:free"
    messages = @(
        @{
            role = "user"
            content = "Привіт! Напиши коротко про програмування українською мовою."
        }
    )
} | ConvertTo-Json -Depth 5

$headers = @{
    "Authorization" = "Bearer $apiKey"
    "Content-Type" = "application/json"
    "HTTP-Referer" = "https://vetalebrowser.com"
}

$response = Invoke-RestMethod -Uri "https://openrouter.ai/api/v1/chat/completions" `
    -Method Post `
    -Headers $headers `
    -Body $body

Write-Host $response.choices[0].message.content
```

### У Vetale Browser:

1. Вставте API ключ (Крок 3)
2. Запустіть браузер (F5)
3. Відкрийте Vetale Search
4. Введіть запит: "програмування"
5. Натисніть 🔍

**Очікуваний результат:**
```
┌────────────────────────────────────────┐
│ 🤖 Vetale AI підсумок                  │
│ Підсумок згенеровано (14:35)          │
├────────────────────────────────────────┤
│ Користувач шукає інформацію про       │
│ програмування - процес створення      │
│ програмного забезпечення...           │
└────────────────────────────────────────┘
```

## 🔍 Перевірка чи ключ працює

В Debug консолі (View → Output → Debug) шукайте:

**✅ Якщо ключ працює:**
```
[QwenAiSummary] API Key configured: sk-or-v1-...
[QwenAiSummary] Service initialized with OpenRouter
[QwenAiSummary] Response Status Code: OK (200)
[QwenAiSummary] Summary generated successfully!
```

**❌ Якщо ключ НЕ встановлено:**
```
[QwenAiSummary] WARNING: API Key not set! Set ApiKey in code or OPENROUTER_API_KEY env variable
```

**❌ Якщо ключ неправильний:**
```
[QwenAiSummary] Response Status Code: Unauthorized (401)
[QwenAiSummary] API error: 401 - Unauthorized
```

## 💰 Чи це справді безкоштовно?

**ТАК!** Моделі з суфіксом `:free` абсолютно безкоштовні:
- ✅ Необмежена кількість запитів
- ✅ Без кредитної картки
- ✅ Ліміт: ~200K tokens на день (це дуже багато!)
- ✅ Якість відповідей висока

## 📊 Ліміти безкоштовних моделей

| Модель | Параметрів | Швидкість | Якість | Контекст |
|--------|------------|-----------|---------|----------|
| Llama 3.2 | 3B | ⚡⚡⚡ Швидко | ⭐⭐⭐⭐ Відмінно | 128K |
| Gemma 2 | 9B | ⚡⚡ Середньо | ⭐⭐⭐⭐⭐ Чудово | 8K |
| Phi-3 | 3.8B | ⚡⚡⚡ Швидко | ⭐⭐⭐⭐ Відмінно | 128K |
| Qwen 2 | 7B | ⚡⚡ Середньо | ⭐⭐⭐⭐ Відмінно | 32K |

## 🛠️ Troubleshooting

### Проблема: "API Key not set"
**Рішення:** Вставте ключ в код або встановіть змінну середовища

### Проблема: "401 Unauthorized"
**Рішення:** 
- Перевірте, чи правильно скопіювали ключ
- Ключ має починатися з `sk-or-v1-`
- Створіть новий ключ на https://openrouter.ai/keys

### Проблема: "429 Too Many Requests"
**Рішення:** 
- Перевищено денний ліміт (200K tokens)
- Зачекайте до завтра або створіть новий акаунт

### Проблема: Повільні відповіді
**Рішення:** 
- Змініть модель на швидшу (Llama 3.2 або Phi-3)
- Безкоштовні моделі можуть мати чергу в пікові години

## 🎉 Готово!

Після вставки API ключа Vetale Browser почне генерувати AI-підсумки для кожного пошукового запиту!

---

**Корисні посилання:**
- 🏠 OpenRouter: https://openrouter.ai/
- 🔑 API Keys: https://openrouter.ai/keys
- 📚 Документація: https://openrouter.ai/docs
- 🤖 Список моделей: https://openrouter.ai/models

