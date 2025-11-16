# ⚡ ШВИДКИЙ СТАРТ: OpenRouter AI для Vetale Search

## 🎯 ЩО ТРЕБА ЗРОБИТИ (3 хвилини):

### 1️⃣ Отримайте БЕЗКОШТОВНИЙ API ключ

1. Відкрийте: **https://openrouter.ai/**
2. Натисніть **"Sign In"** (можна через Google/GitHub)
3. Перейдіть: **https://openrouter.ai/keys**
4. Натисніть **"Create Key"**
5. **Скопіюйте ключ** (починається з `sk-or-v1-...`)

### 2️⃣ Вставте ключ у код

Відкрийте файл:
```
VetaleBrowser.Search\Services\QwenAiSummaryService.cs
```

Знайдіть рядок **27**:
```csharp
private const string ApiKey = "YOUR_API_KEY_HERE";
```

Замініть на ваш ключ:
```csharp
private const string ApiKey = "sk-or-v1-1234567890abcdef...";
```

### 3️⃣ Запустіть і тестуйте!

1. **F5** - запустити браузер
2. Відкрийте **Vetale Search**
3. Введіть запит: **"програмування"**
4. Натисніть **🔍**
5. Дивіться AI-підсумок праворуч! ✨

---

## 🆓 Безкоштовні моделі (вже налаштовано):

Код вже використовує **Meta Llama 3.2** (безкоштовно):
```csharp
private const string ModelName = "meta-llama/llama-3.2-3b-instruct:free";
```

**Інші безкоштовні опції** (змініть рядок 23):
```csharp
// Google Gemma (якісніша, але повільніша)
"google/gemma-2-9b-it:free"

// Microsoft Phi-3 (дуже швидка)
"microsoft/phi-3-mini-128k-instruct:free"

// Qwen 2 (оригінальна модель)
"qwen/qwen-2-7b-instruct:free"
```

---

## ✅ Переваги OpenRouter:

- ✅ **100% безкоштовно** (моделі з `:free`)
- ✅ **Без кредитної картки**
- ✅ **Необмежена кількість запитів** (~200K tokens/день)
- ✅ **Стабільний API** (не падає, як HuggingFace Spaces)
- ✅ **Багато моделей** на вибір
- ✅ **Українська мова** підтримується

---

## 🐛 Проблеми?

**API Key not set:**
→ Вставте ключ у код (рядок 27)

**401 Unauthorized:**
→ Перевірте ключ (має починатися з `sk-or-v1-`)

**429 Too Many Requests:**
→ Перевищено ліміт, зачекайте до завтра

---

📚 **Повна інструкція:** `OPENROUTER_API_KEY_GUIDE.md`

🎉 **Готово! Тепер AI працює!**

