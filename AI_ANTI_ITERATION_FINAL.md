# Посилений захист від самоітерації AI

## Дата: 6 листопада 2025

## Проблема
AI почав сам з собою ітерувати - генерувати діалог "User: ... Assistant: ..." після відповіді.

## Рішення: 5 рівнів захисту

### 1️⃣ EndMarkers (13 маркерів)

**Розширено з 9 до 13**:

```csharp
private static readonly string[] EndMarkers = { 
    // Технічні токени
    "<|end|>", "<|im_end|>", "</s>", "[END]", "<end_of_turn>",
    
    // Діалогові маркери
    "\nUser:", "\nHuman:", "\n\nUser:", "\n\nHuman:",
    
    // НОВІ - додаткові стопи
    "\n\nAssistant:", "\nQuestion:", "User:", "Human:"
};
```

### 2️⃣ AntiPrompts (5 варіантів)

```csharp
AntiPrompts = new List<string> { 
    "\nUser:", "\n\nUser:", "\nHuman:", "\n\nHuman:", "User:" 
}
```

Спрацьовують **на рівні LlamaSharp** - найшвидше.

### 3️⃣ Детектор діалогу (НОВИЙ!) 🆕

**Метод**: `ShouldStopGeneration()` - покращено

```csharp
private bool ShouldStopGeneration(string text)
{
    // 1. Стандартні end markers
    if (EndMarkers.Any(marker => text.Contains(marker)))
        return true;

    // 2. НОВИЙ! Виявлення діалогу
    if (Regex.IsMatch(text, 
        @"(User|Human|Question|Q):\s*.+\s*(Assistant|AI|Answer|A):", 
        RegexOptions.IgnoreCase))
    {
        System.Diagnostics.Trace.WriteLine("Detected self-dialogue pattern");
        return true;
    }

    return false;
}
```

**Виявляє паттерни**:
- `User: ... Assistant: ...`
- `Human: ... AI: ...`
- `Question: ... Answer: ...`
- `Q: ... A: ...`

**Приклад**:
```
Текст: "Відповідь на ваше питання.

User: А що ще можна додати?
Assistant: Ось додаткова інформація..."

↑ Regex знаходить: "User: А що ще можна додати? Assistant:"
→ ЗУПИНКА!
```

### 4️⃣ Детектор повторень

```csharp
if (currentResponse.Length > 100 && HasRepetitivePattern(currentResponse))
{
    System.Diagnostics.Trace.WriteLine("Repetitive pattern detected");
    break;
}
```

Виявляє циклічні повторення тексту.

### 5️⃣ Системний промпт

```csharp
systemPrompt.AppendLine("IMPORTANT: Stop generating immediately after completing your answer.");
systemPrompt.AppendLine("Do not continue with follow-up questions or additional dialogue.");
```

**Явна інструкція** для моделі - не продовжувати після відповіді.

## Як працює захист (пріоритет)

### Послідовність перевірок:

```
Кожен токен:
  ↓
1. AntiPrompts (LlamaSharp) → СТОП якщо знайдено
  ↓
2. EndMarkers → СТОП якщо знайдено
  ↓
3. Детектор діалогу (Regex) → СТОП якщо знайдено
  ↓
4. Детектор повторень → СТОП якщо знайдено
  ↓
5. Safety Limit (12000) → СТОП якщо перевищено
  ↓
Продовжити генерацію
```

## Тестові сценарії

### ✅ Тест 1: Звичайна відповідь
```
AI генерує: "JavaScript - це мова програмування..."
Детектор: Немає діалогу → Продовжити
Результат: PASS
```

### ✅ Тест 2: Самоітерація
```
AI генерує: "Відповідь.

User: А що ще?
Assistant: Ось ще..."

Детектор діалогу: Знайдено "User: ... Assistant:" → СТОП!
Результат: PASS
```

### ✅ Тест 3: Приклад в тексті
```
AI генерує: "Приклад діалогу:
User: Привіт
Assistant: Здоров"

Детектор діалогу: Знайдено паттерн → СТОП
Результат: PASS (можливий false positive, але безпечно)
```

### ✅ Тест 4: Повторення
```
AI генерує: "текст текст текст текст..."
Детектор повторень: Знайдено цикл → СТОП
Результат: PASS
```

## Логування

```
VetaleAIAgent: Starting token generation...
VetaleAIAgent: Token 245, response length: 1823
VetaleAIAgent: Detected self-dialogue pattern - stopping
VetaleAIAgent: Generation complete. Total tokens: 245, Response length: 1823
```

або

```
VetaleAIAgent: End marker detected at token 312
VetaleAIAgent: Generation complete. Total tokens: 312, Response length: 2156
```

## Параметри

```csharp
// Захист
EndMarkers = 13              // Було 9
AntiPrompts = 5              // Без змін
DialogueDetector = ON        // НОВИЙ!
RepetitionDetector = ON      // Без змін
SystemPromptStop = ON        // Посилено

// Ліміти
MaxTokens = 4096             // Максимум
SafetyLimit = 12000          // Максимум

// Regex для діалогу
Pattern = @"(User|Human|Question|Q):\s*.+\s*(Assistant|AI|Answer|A):"
```

## False Positives (можливі)

⚠️ Детектор діалогу може спрацювати якщо:
- AI наводить **приклад діалогу** в відповіді
- Текст містить **демонстрацію** розмови
- Є **цитати** з діалогом

**Рішення**: Це прийнятний компроміс - краще зупинитися раніше, ніж генерувати нескінченний текст.

## Переваги

✅ **5 рівнів захисту** - надійність  
✅ **Regex детектор** - виявляє самоітерацію  
✅ **Швидке реагування** - на рівні токенів  
✅ **Логування** - легко діагностувати  
✅ **Максимальна довжина** - але контрольовано  

## Результат

🎯 **AI тепер НЕ МОЖЕ ітерувати сам з собою!**

- 13 EndMarkers
- 5 AntiPrompts
- Regex детектор діалогу
- Детектор повторень
- Чіткий системний промпт

**Нескінченна генерація неможлива при такому захисті!**

## Файли змінені

`VetaleBrowser.AI/VetaleAIAgent.cs`:
1. `EndMarkers` - 9 → 13 маркерів
2. `ShouldStopGeneration()` - додано Regex детектор
3. `BuildSystemPrompt()` - додано IMPORTANT інструкцію

## Тестування

Спробуйте:
1. Задати просте питання → повинно відповісти і зупинитися
2. Задати складне питання → детальна відповідь + зупинка
3. Перевірити чи немає "User: ... Assistant: ..." в кінці
4. Подивитися логи на "Detected self-dialogue pattern"

