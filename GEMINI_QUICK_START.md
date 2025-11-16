# 🚀 Gemini AI - Швидкий старт

## За 5 хвилин до першого AI-підсумку!

### Крок 1: Отримайте API ключ (2 хв) 🔑

1. Відкрийте: https://aistudio.google.com/app/apikey
2. Увійдіть через Google акаунт
3. Натисніть **"Create API key"**
4. Скопіюйте ключ (формат: `AIzaSy...`)

### Крок 2: Налаштуйте ключ (1 хв) ⚙️

**Варіант А: В коді**
```csharp
// Файл: GeminiAiSummaryService.cs (рядок ~44)
private const string ApiKey = "AIzaSyВАШ_КЛЮЧ_ТУТ";
```

**Варіант Б: Змінна середовища** (рекомендовано)
```powershell
[System.Environment]::SetEnvironmentVariable('GEMINI_API_KEY', 'AIzaSyВАШ_КЛЮЧ_ТУТ', 'User')
```

### Крок 3: Використовуйте! (2 хв) 🎯

```csharp
using VetaleBrowser.VetaleBrowser.Search.Services;

// Створіть сервіс
var ai = new GeminiAiSummaryService();

// Згенеруйте підсумок
var summary = await ai.GenerateSummaryAsync("як приготувати борщ");

// Використайте результат
if (summary.State == AiSummaryState.Ready)
{
    Console.WriteLine(summary.SummaryText);
}

// Звільніть ресурси
ai.Dispose();
```

### ✅ Готово!

Ваш перший AI-підсумок працює! 🎉

---

## 📊 Що далі?

- 📚 [Повна документація](GEMINI_API_INTEGRATION.md)
- 💻 [Приклади коду](GeminiAiExamples.cs)
- 🔧 [Налаштування параметрів](GEMINI_API_INTEGRATION.md#налаштування-параметрів)

---

## ⚡ Швидкі факти

- ✅ **Безкоштовно**: 1M токенів/місяць
- ⚡ **Швидко**: ~1-3 секунди на відповідь
- 🌐 **Українська мова**: Відмінна підтримка
- 🛡️ **Безпечно**: Вбудовані фільтри

---

## 🐛 Проблеми?

**"API ключ не налаштовано"**
→ Перевірте Крок 2

**"Невірний API ключ"**
→ Переконайтеся, що скопіювали повністю (починається з `AIzaSy`)

**"Перевищено ліміт"**
→ Зачекайте 1 хвилину (ліміт: 15 запитів/хв)

---

**Потрібна допомога?** Перегляньте [повну документацію](GEMINI_API_INTEGRATION.md)

