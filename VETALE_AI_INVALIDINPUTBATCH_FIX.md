# Виправлення InvalidInputBatch Error / InvalidInputBatch Error Fix

## Проблема
При перемиканні мови або створенні нового чату виникала помилка `InvalidInputBatch`.

## Причина
LlamaSharp `InteractiveExecutor` зберігає контекст розмови. При зміні налаштувань або довгій розмові контекст переповнюється або стає невалідним, що призводить до помилки `InvalidInputBatch`.

## Рішення

### 1. Async ResetContext в VetaleAIAgent
**Було:**
```csharp
public void ResetContext()
{
    if (_executor != null && _context != null)
    {
        _executor = new InteractiveExecutor(_context);
        System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Context reset");
    }
}
```

**Проблема:** Просте перестворення executor не очищує контекст правильно.

**Стало:**
```csharp
public async Task ResetContextAsync()
{
    if (_context != null && _model != null)
    {
        // Dispose old executor and context
        _executor = null;
        _context.Dispose();
        
        // Recreate context and executor
        var parameters = new ModelParams(_modelPath)
        {
            ContextSize = 4096,
            GpuLayerCount = 0,
            UseMemorymap = true,
            UseMemoryLock = false
        };
        
        await Task.Run(() =>
        {
            _context = _model.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);
        });
        
        System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Context reset successfully");
    }
}
```

**Чому краще:**
- Повністю видаляє старий контекст
- Створює новий контекст з нуля
- Гарантує чистий стан для нової розмови

### 2. Async ResetContext в VetaleAIService
```csharp
public async Task ResetContextAsync()
{
    if (_agent != null)
    {
        await _agent.ResetContextAsync();
    }
}
```

### 3. Async NewChat_Click в VetaleAIChatPage
**Було:**
```csharp
private void NewChat_Click(object? sender, RoutedEventArgs e)
{
    // ...clear messages...
    _aiService?.ResetContext();
}
```

**Стало:**
```csharp
private async void NewChat_Click(object? sender, RoutedEventArgs e)
{
    try
    {
        // Cancel ongoing generation
        _cancellationTokenSource?.Cancel();
        
        // Clear messages
        // ...
        
        // Reset AI context
        if (_aiService != null)
        {
            await _aiService.ResetContextAsync();
        }
    }
    catch (Exception ex)
    {
        AddAssistantMessage($"Error resetting chat: {ex.Message}");
    }
}
```

### 4. Автоматичний Retry при InvalidInputBatch
Найважливіше виправлення - автоматичне скидання контексту при помилці:

```csharp
private async Task<string> GenerateResponseAsync(string prompt)
{
    var maxRetries = 2;
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            // Generate response
            var response = await _aiService.GenerateResponseAsync(...);
            return response;
        }
        catch (Exception ex) when (
            ex.Message.Contains("InvalidInputBatch") || 
            ex.Message.Contains("context") || 
            ex.Message.Contains("batch"))
        {
            if (attempt < maxRetries - 1)
            {
                // Reset context and retry
                await _aiService.ResetContextAsync();
                await Task.Delay(500);
                continue;
            }
            else
            {
                return "Error: AI context error. Please click 'New Chat' to reset.";
            }
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
```

**Як працює:**
1. Спроба згенерувати відповідь
2. Якщо виникає помилка контексту → автоматичний reset
3. Затримка 500ms для стабілізації
4. Повторна спроба
5. Максимум 2 спроби

## Коли скидається контекст

### Автоматично:
- ✅ При помилці `InvalidInputBatch`
- ✅ При помилці, що містить слово "context" або "batch"
- ✅ Автоматичний retry з reset

### Вручну:
- ✅ При натисканні "New Chat"
- ✅ При натисканні "Clear History"

### НЕ скидається:
- ❌ При зміні мови (використовує новий промпт з тим же контекстом)
- ❌ При зміні reasoning режиму (також новий промпт)

## Переваги рішення

### 1. Прозоре для користувача
Користувач не бачить помилки - система автоматично виправляє ситуацію.

### 2. Збереження контексту коли можливо
Контекст зберігається між повідомленнями в одній мові.

### 3. Автоматичне відновлення
При будь-якій помилці контексту - автоматичний reset і retry.

### 4. Інформативні помилки
Якщо retry не допоміг - чітке повідомлення що робити.

## Логування

### Успішний reset:
```
VetaleAIChatPage: New chat requested
VetaleAIChatPage: Resetting AI context...
VetaleAIAgent: Resetting context...
VetaleAIAgent: Context reset successfully
VetaleAIChatPage: AI context reset complete
```

### Автоматичний retry при помилці:
```
VetaleAIChatPage: Context error on attempt 1: InvalidInputBatch
VetaleAIChatPage: Resetting context and retrying...
VetaleAIAgent: Resetting context...
VetaleAIAgent: Context reset successfully
VetaleAIChatPage: Calling AI service (attempt 2)...
VetaleAIChatPage: Response received
```

### Невдалий retry:
```
VetaleAIChatPage: Context error on attempt 2: InvalidInputBatch
User sees: "Error: AI context error. Please click 'New Chat' to reset."
```

## Тестування

### 1. Тест зміни мови:
1. Надішліть повідомлення українською
2. Змініть мову на English
3. Надішліть нове повідомлення
4. ✅ Має працювати без помилок

### 2. Тест нового чату:
1. Ведіть розмову (5+ повідомлень)
2. Натисніть "New Chat"
3. Надішліть нове повідомлення
4. ✅ Контекст має очиститись

### 3. Тест довгої розмови:
1. Надішліть багато повідомлень підряд (10+)
2. ✅ Якщо виникне помилка контексту - автоматичний reset
3. ✅ Користувач отримає відповідь після retry

### 4. Тест reasoning режиму:
1. Увімкніть reasoning
2. Надішліть запитання
3. Вимкніть reasoning
4. Надішліть інше запитання
5. ✅ Має працювати без помилок

## Оновлені файли

- ✅ `VetaleBrowser.AI/VetaleAIAgent.cs`
  - Async `ResetContextAsync()` з повним recreate контексту
  
- ✅ `VetaleBrowser.AI/VetaleAIService.cs`
  - Async `ResetContextAsync()`
  
- ✅ `VetaleBrowser.UI/Pages/VetaleAIChatPage.axaml.cs`
  - Async `NewChat_Click()` з правильним reset
  - Автоматичний retry при помилках контексту
  - Детальне логування

## Статус

✅ **ВИПРАВЛЕНО**

`InvalidInputBatch` помилка тепер:
- Автоматично обробляється з retry
- Контекст повністю скидається при "New Chat"
- Інформативні повідомлення для користувача

---

**Дата**: 5 листопада 2025
**Версія**: 2.0

