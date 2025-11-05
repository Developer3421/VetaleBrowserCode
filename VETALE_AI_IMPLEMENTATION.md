# Vetale AI Integration - Complete Implementation

## Огляд / Overview

Реалізовано повноцінного AI агента для VetaleAIChatPage з використанням:
- **Microsoft Agents Framework** - для структурування агента
- **LlamaSharp 0.25.0** - для роботи з локальною моделлю GGUF
- **Gemma 3 1B** модель (`gemma-3-1b-it-UD-Q2_K_XL.gguf`)

## Архітектура

### 1. VetaleAIAgent.cs
Основний клас AI агента з наступними можливостями:

#### Фільтрація символів:
- ✅ Бенгальські символи (`\u0980-\u09FF`)
- ✅ Арабські символи (`\u0600-\u06FF`)
- ✅ Китайські ієрогліфи (`\u4E00-\u9FFF`)
- ✅ Деванагарі (`\u0900-\u097F`)
- ✅ Тайські символи (`\u0E00-\u0E7F`)
- ✅ Zero-width символи (`\u200B-\u200D`, `\uFEFF`)
- ✅ Контрольні символи
- ✅ Надлишкові спеціальні символи (більше 3 підряд)

#### Автостоп по маркерах:
```csharp
private static readonly string[] EndMarkers = { 
    "<|end|>", 
    "<|im_end|>", 
    "</s>", 
    "[END]", 
    "<end_of_turn>" 
};
```

#### Налаштування моделі:
```csharp
var parameters = new ModelParams(_modelPath)
{
    ContextSize = 4096,        // Розмір контексту
    GpuLayerCount = 0,         // CPU only (можна збільшити для GPU)
    UseMemorymap = true,       // Використання memory mapping
    UseMemoryLock = false
};
```

#### Параметри генерації:
```csharp
var inferenceParams = new InferenceParams
{
    MaxTokens = 2048,
    AntiPrompts = new List<string> { "User:", "\nUser:" }
};
```

### 2. VetaleAIService.cs
Сервіс для управління життєвим циклом агента:

- **Ініціалізація**: Автоматичне завантаження моделі з `VetaleBrowser.AI/Model/`
- **Генерація відповідей**: З підтримкою мови та reasoning режиму
- **Скидання контексту**: Для початку нової розмови

#### Підтримувані мови:
- 🇺🇦 Українська
- 🇬🇧 Англійська
- 🇷🇺 Російська
- 🇩🇪 Німецька
- 🇫🇷 Французька
- 🇪🇸 Іспанська
- 🌐 Авто (визначення автоматично)

### 3. VetaleAIChatPage.axaml.cs
Інтеграція з UI:

#### Основні функції:
- ✅ Асинхронна ініціалізація моделі при завантаженні сторінки
- ✅ Відправка повідомлень з Enter (Shift+Enter для нового рядка)
- ✅ Індикатор "Thinking..." під час генерації
- ✅ Скасування генерації при новому чаті
- ✅ Очищення ресурсів при вивантаженні сторінки

#### Lifecycle:
```csharp
Loaded → InitializeAIModel() → _aiService.InitializeAsync()
Unloaded → Dispose() → _aiService.Dispose()
```

## Використання

### Базове використання:
1. Відкрийте Vetale AI через меню браузера
2. Дочекайтесь ініціалізації моделі (відбувається автоматично)
3. Введіть повідомлення і натисніть "Надіслати" або Enter

### Налаштування:
- **💭 Reasoning Mode**: Включає показ процесу міркувань AI
- **🌐 Response Language**: Вибір мови відповіді
- **➕ New Chat**: Почати нову розмову (скидає контекст)
- **🗑️ Clear History**: Очистити всю історію чату

## Технічні деталі

### Фільтрація в реальному часі:
```csharp
await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams, cancellationToken))
{
    // Фільтруємо кожен токен окремо
    var filteredToken = FilterCharacters(token);
    
    if (string.IsNullOrEmpty(filteredToken))
        continue;
        
    responseBuilder.Append(filteredToken);
    
    // Перевіряємо маркери завершення
    if (ShouldStopGeneration(currentResponse))
    {
        break;
    }
}
```

### Системний промпт:
```csharp
"You are Vetale AI, a helpful and knowledgeable assistant integrated into Vetale Browser."
+ languageHint  // "Please respond in Ukrainian."
+ reasoningHint // "Show your reasoning process when answering questions."
+ "Do not use Bengali, Arabic, Chinese, Devanagari, Thai or other non-Latin scripts unless specifically requested."
```

### Безпека:
- ✅ Обмеження на 2048 токенів за відповідь
- ✅ Максимум 8000 символів (safety limit)
- ✅ Фільтрація шкідливих символів
- ✅ Anti-prompts для запобігання зациклення
- ✅ Cancellation token для скасування

## Шлях до моделі

Модель має знаходитись за шляхом:

```
VetaleBrowser/
└── VetaleBrowser/
    └── VetaleBrowser.AI/
        └── Model/
            └── gemma-3-1b-it-UD-Q2_K_XL.gguf  ← Модель
```

### Автоматичне копіювання

При білді проекту модель автоматично копіюється в output папку завдяки налаштуванню в `.csproj`:

```xml
<ItemGroup>
  <None Include="VetaleBrowser.AI\Model\*.gguf" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### Пошук моделі

Сервіс шукає модель в наступних місцях (в порядку пріоритету):
1. **bin/Debug/net9.0/VetaleBrowser.AI/Model/** - скопійована при білді
2. **../../../VetaleBrowser.AI/Model/** - відносний шлях до проекту
3. **bin/Debug/net9.0/Model/** - альтернативне розташування

Якщо модель знайдена, в логах буде:
```
VetaleAIService: Found model at: E:\VetaleBrowser\...\gemma-3-1b-it-UD-Q2_K_XL.gguf
```

Якщо ні, буде показано всі перевірені шляхи.

## Залежності (додано в .csproj)

```xml
<PackageReference Include="LLamaSharp" Version="0.25.0" />
<PackageReference Include="LLamaSharp.Backend.Cpu" Version="0.25.0" />
<PackageReference Include="Microsoft.Agents.AI.Abstractions" Version="1.0.0-preview.251104.1" />
```

## Створені файли

1. **VetaleBrowser.AI/VetaleAIAgent.cs** - Основний агент
2. **VetaleBrowser.AI/VetaleAIService.cs** - Сервіс управління
3. **VetaleBrowser.UI/Pages/VetaleAIChatPage.axaml.cs** - Інтеграція з UI (оновлено)

## Особливості реалізації

### Потокова генерація:
Відповідь генерується токен за токеном з фільтрацією в реальному часі:
```csharp
await foreach (var token in _executor.InferAsync(...))
{
    // Обробка кожного токену
}
```

### Memory Management:
- Використання `IDisposable` для всіх компонентів
- Автоматичне звільнення ресурсів при закритті сторінки
- `SemaphoreSlim` для thread-safe ініціалізації

### Обробка помилок:
```csharp
try {
    // Генерація відповіді
}
catch (OperationCanceledException) {
    // Скасовано користувачем
}
catch (Exception ex) {
    // Інші помилки
}
```

## Тестування

### Перевірте наступне:
1. ✅ Ініціалізація моделі при відкритті сторінки
2. ✅ Генерація відповідей українською, англійською
3. ✅ Фільтрація спам-символів
4. ✅ Автостоп по маркерах
5. ✅ Reasoning режим
6. ✅ Зміна мови відповіді
7. ✅ Новий чат (скидання контексту)
8. ✅ Скасування довгої генерації
9. ✅ Очищення історії

## Можливі покращення

### В майбутньому можна додати:
- [ ] GPU підтримка (змінити `GpuLayerCount > 0`)
- [ ] Streaming UI (показ токенів в реальному часі)
- [ ] Збереження історії в LiteDB
- [ ] Експорт чату в файл
- [ ] Голосове введення/виведення
- [ ] Вибір різних моделей
- [ ] Налаштування температури та інших параметрів

## Логування

Всі важливі події логуються через `System.Diagnostics.Trace`:
```
VetaleAIAgent: Model initialized successfully
VetaleAIChatPage: AI model initialized successfully
VetaleAIService: Generating response...
VetaleAIAgent: Context reset
```

## Статус

✅ **ПОВНІСТЮ РЕАЛІЗОВАНО**

- Агент з Microsoft Agents Framework
- LlamaSharp інтеграція
- Фільтрація бенгальських та інших спам символів
- Автостоп по внутрішньому флажку кінця відповіді
- UI інтеграція з VetaleAIChatPage
- Підтримка мов та reasoning режиму
- Memory management та error handling

---

**Створено**: 5 листопада 2025
**Автор**: GitHub Copilot
**Версія**: 1.0.0

