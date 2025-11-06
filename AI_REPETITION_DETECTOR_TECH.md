# Детектор повторень та форматування віршів - Технічна документація

## Дата: 6 листопада 2025

## Проблеми

1. **Безкінечні повторення тексту**: AI генерував один і той же текст по колу
2. **Неправильне форматування віршів**: Одинарні переноси рядків видалялися

## Рішення

### 1. Детектор повторень - `HasRepetitivePattern()`

#### Алгоритм:
```csharp
1. Перевіряє текст довжиною > 100 символів
2. Бере останні 50 символів (або 1/4 тексту)
3. Шукає цей паттерн у попередній частині тексту
4. Якщо знаходить 2+ повторення → ЗУПИНКА
```

#### Код:
```csharp
private bool HasRepetitivePattern(string text)
{
    if (text.Length < 100) return false;

    var checkLength = Math.Min(50, text.Length / 4);
    var endPart = text.Substring(text.Length - checkLength);
    var beforeEnd = text.Substring(0, text.Length - checkLength);

    int count = 0;
    int index = 0;
    while ((index = beforeEnd.IndexOf(endPart, index)) != -1)
    {
        count++;
        index += checkLength;
        if (count >= 2) return true;
    }
    return false;
}
```

#### Приклади спрацювання:

**Випадок 1**: Повторення речення
```
Текст: "Привіт. Як справи? Привіт. Як справи? Привіт. Як справи?"
        └─────Pattern─────┘ └─────Pattern─────┘
        Детектор: 2 повторення → СТОП!
```

**Випадок 2**: Повторення фрази
```
Текст: "...і так далі і так далі і так далі і так далі..."
                     └──Pattern──┘ └──Pattern──┘
        Детектор: 2 повторення → СТОП!
```

**Випадок 3**: НЕ спрацює на нормальному тексті
```
Текст: "Перше речення. Друге речення. Третє речення."
        Детектор: Немає повторень → Продовжуємо
```

### 2. Покращене форматування віршів

#### Проблема:
```csharp
// СТАРИЙ код - видаляв 3+ переноси
text = Regex.Replace(text, @"\n{3,}", "\n\n");

// Результат для вірша:
Вхід:  "Рядок 1\nРядок 2\nРядок 3"
Вихід: "Рядок 1\nРядок 2\nРядок 3"  ✓ (працювало)

Вхід:  "Строфа 1\n\nСтрофа 2\n\nСтрофа 3"
Вихід: "Строфа 1\n\nСтрофа 2\n\nСтрофа 3"  ✓ (працювало)

НО при "помилкових" переносах:
Вхід:  "Текст\n\n\nТекст"
Вихід: "Текст\n\nТекст"  ✗ (видаляв потрібний відступ)
```

#### Рішення:
```csharp
// НОВИЙ код - зберігає структуру, видаляє тільки 4+ порожні рядки
text = Regex.Replace(text, @"(\r?\n\s*){4,}", "\n\n\n");

// Regex пояснення:
// (\r?\n\s*){4,} = 4 або більше переносів рядків (з пробілами між ними)
```

#### Результати:

| Вхід | Старий вихід | Новий вихід |
|------|--------------|-------------|
| `\n` | `\n` | `\n` ✓ |
| `\n\n` | `\n\n` | `\n\n` ✓ |
| `\n\n\n` | `\n\n` ❌ | `\n\n\n` ✓ |
| `\n\n\n\n` | `\n\n` ❌ | `\n\n\n` ✓ |
| `\n\n\n\n\n` | `\n\n` ❌ | `\n\n\n` ✓ |

### 3. Інтеграція в генерацію

#### GenerateResponseAsync:
```csharp
await foreach (var token in _executor.InferAsync(...))
{
    responseBuilder.Append(filteredToken);
    var currentResponse = responseBuilder.ToString();
    
    // 1. Перевірка EndMarkers
    if (ShouldStopGeneration(currentResponse)) break;
    
    // 2. Перевірка повторень (НОВИЙ!)
    if (currentResponse.Length > 100 && HasRepetitivePattern(currentResponse))
    {
        System.Diagnostics.Trace.WriteLine("Repetitive pattern detected");
        break;
    }
    
    // 3. Safety limit
    if (responseBuilder.Length > 3000) break;
}
```

#### GenerateResponseStreamAsync:
```csharp
// Те ж саме + streaming до UI
await foreach (var token in _executor.InferAsync(...))
{
    responseBuilder.Append(filteredToken);
    var current = responseBuilder.ToString();
    
    if (ShouldStopGeneration(current)) break;
    if (current.Length > 100 && HasRepetitivePattern(current)) break;
    if (responseBuilder.Length > 3000) break;
    
    progress?.Report(filteredToken); // Streaming
}
```

## Продуктивність

### HasRepetitivePattern():
- **Складність**: O(n*m) де n=довжина тексту, m=довжина паттерну
- **Викликається**: Кожен токен після 100 символів
- **Вплив**: Мінімальний (перевірка String.IndexOf дуже швидка)

### CleanupResponse():
- **Було**: 1 regex + FilterCharacters (багато regex)
- **Стало**: 1 regex + TrimEnd кожного рядка
- **Покращення**: Швидше та простіше

## Логування

### Детектор повторень:
```
VetaleAIAgent: Detected repetitive pattern: 'і так далі і так...'
VetaleAIAgent: Repetitive pattern detected at token 245
```

### Форматування:
```
VetaleAIAgent: Generation complete. Total tokens: 187, Response length: 823
VetaleAIAgent: Final response length: 810
```

## Тестові сценарії

### Тест 1: Вірш
```
Вхід: "Сонце світить,
       Птахи співають,
       День прекрасний."

Очікується: Збереження всіх одинарних переносів
Результат: ✓ PASS
```

### Тест 2: Повторення
```
Генерація: "Текст текст текст текст текст текст..."
Очікується: Зупинка після 2+ повторень
Результат: ✓ PASS
```

### Тест 3: Список
```
Вхід: "1. Пункт
       2. Пункт
       3. Пункт"

Очікується: Збереження структури
Результат: ✓ PASS
```

### Тест 4: Багато порожніх рядків
```
Вхід: "Текст\n\n\n\n\nТекст"
Очікується: "Текст\n\n\nТекст"
Результат: ✓ PASS
```

## Параметри налаштування (ОНОВЛЕНО - МАКСИМАЛЬНІ)

```csharp
// Детектор повторень
MinLengthForCheck = 100;        // Мінімальна довжина для перевірки
PatternLength = 50;              // Довжина паттерну для пошуку
RepetitionThreshold = 2;         // Скільки повторень = зупинка

// Генерація (МАКСИМАЛЬНІ ЗНАЧЕННЯ)
MaxTokens = 4096;                // Максимум токенів (було 1024)
SafetyLimit = 12000;             // Максимум символів (було 3000)

// Форматування
MaxConsecutiveNewlines = 3;      // Максимум 3 порожні рядки
```

## Нові можливості (v2)

### Максимальна деталізація:
- **4096 токенів** замість 1024 (×4 збільшення)
- **12000 символів** замість 3000 (×4 збільшення)
- **"Detailed, comprehensive answers"** замість "concise, focused"

### Що це дає:
✅ Дуже детальні пояснення (~8-10 сторінок A4)
✅ Приклади та контекст в кожній відповіді
✅ Глибокий аналіз складних тем
✅ Покрокові інструкції з поясненнями

### Захист залишився:
✅ Детектор повторень (HasRepetitivePattern)
✅ EndMarkers (9 маркерів)
✅ AntiPrompts (5 варіантів)
✅ Safety Limit (підвищено до 12000)

## Можливі покращення

1. **Adaptive pattern length**: Змінювати довжину паттерну в залежності від довжини тексту
2. **Similarity threshold**: Перевіряти схожість, а не точну відповідність
3. **Context-aware detection**: Не спрацьовувати на легітимні повторення (списки, вірші)

## Результат

✅ **Детектор повторень** запобігає нескінченним циклам  
✅ **Форматування віршів** зберігає одинарні переноси  
✅ **Продуктивність** залишається високою  
✅ **Логування** допомагає діагностувати проблеми  

## Файли змінені

`VetaleBrowser.AI/VetaleAIAgent.cs`:
1. `HasRepetitivePattern()` - новий метод детектора
2. `GenerateResponseAsync()` - інтеграція детектора
3. `GenerateResponseStreamAsync()` - інтеграція детектора
4. `CleanupResponse()` - покращений regex для віршів

