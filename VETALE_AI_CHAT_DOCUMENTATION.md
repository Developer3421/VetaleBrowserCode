# Vetale AI Chat - Документація

## Огляд

Vetale AI Chat - це вбудоване вікно чату з локальним штучним інтелектом на базі LlamaSharp + Gemma3. Воно надає користувачам можливість спілкуватися з AI без потреби в інтернет-з'єднанні.

## Основні файли

### 1. VetaleAIWindow.axaml
**Розташування:** `VetaleBrowser/VetaleBrowser.UI/Windows/VetaleAIWindow.axaml`

Головне вікно для Vetale AI Chat з такими характеристиками:
- **Кольоровий стиль:** Помаранчевий фон (#FF8B4513), сірий градієнт для верхньої панелі
- **Розмір:** 1000x700 px (мінімум: 800x600 px)
- **Стиль кнопок:** Зелені (#4CAF50) з анімацією при наведенні
- **Верхня панель:** Іконка 🤖, кнопка "Vetale Browser", кнопки згортання/закриття

### 2. VetaleAIWindow.axaml.cs
**Розташування:** `VetaleBrowser/VetaleBrowser.UI/Windows/VetaleAIWindow.axaml.cs`

Code-behind для вікна з функціями:
- Завантаження сторінки чату
- Керування вікном (переміщення, згортання, закриття)
- Навігація до головного вікна браузера

### 3. VetaleAIChatPage.axaml
**Розташування:** `VetaleBrowser/VetaleBrowser.UI/Pages/VetaleAIChatPage.axaml`

Сторінка чату в стилі Claude з:
- **Область повідомлень:** Прокручувана панель з бульбашками повідомлень
- **Інструменти під полем вводу:**
  - 💭 **Enable Reasoning** - перемикач для активації міркування AI
  - 🌐 **Response Language** - вибір мови відповідей (Авто, Українська, Англійська, Російська, Німецька, Французька, Іспанська)
  - ➕ **New Chat** - створення нового чату
  - 🗑️ **Clear History** - очищення історії
- **Поле вводу:** Багаторядкове поле з автозміною висоти
- **Кнопка надсилання:** ✈️ Send

### 4. VetaleAIChatPage.axaml.cs
**Розташування:** `VetaleBrowser/VetaleBrowser.UI/Pages/VetaleAIChatPage.axaml.cs`

Логіка чату:
- Обробка введення користувача (Enter для надсилання, Shift+Enter для нового рядка)
- Відображення повідомлень користувача та AI
- Анімація "Thinking..." під час обробки
- Інтеграція з LlamaSharp (TODO: потребує імплементації)

## Дизайн

### Стиль повідомлень

**Повідомлення користувача:**
- Фон: #F0F0F0 (світло-сірий)
- Вирівнювання: праворуч
- Заголовок: "You" / "Ви" (#666666)

**Повідомлення AI:**
- Фон: #E8F5E9 (світло-зелений)
- Вирівнювання: ліворуч
- Заголовок: "Vetale AI" (#4CAF50)

### Інструменти

**Reasoning Toggle:**
- Стиль: Toggle button з зеленою рамкою
- Активний стан: зелений фон (#4CAF50)

**Language Selector:**
- Стиль: ComboBox з зеленою рамкою
- Опції: 7 мов (авто-визначення + 6 конкретних мов)

## Локалізація

Додані ключі в `Strings.en.axaml` та `Strings.uk.axaml`:

```xml
<!-- VetaleAI Chat -->
<x:String x:Key="VetaleAI.Title">Vetale AI Chat — Vetale Browser</x:String>
<x:String x:Key="VetaleAI.Header">Vetale AI Chat</x:String>
<x:String x:Key="VetaleAI.InputPlaceholder">Type your message... / Введіть ваше повідомлення...</x:String>
<x:String x:Key="VetaleAI.Send">Send / Надіслати</x:String>
<x:String x:Key="VetaleAI.NewChat">New Chat / Новий чат</x:String>
<x:String x:Key="VetaleAI.ClearHistory">Clear History / Очистити історію</x:String>
<x:String x:Key="VetaleAI.EnableReasoning">Enable Reasoning / Увімкнути міркування</x:String>
<x:String x:Key="VetaleAI.ResponseLanguage">Response Language / Мова відповідей</x:String>
<x:String x:Key="VetaleAI.WelcomeMessage">Hello! I'm Vetale AI... / Привіт! Я Vetale AI...</x:String>
<x:String x:Key="VetaleAI.Thinking">Thinking... / Міркую...</x:String>
<x:String x:Key="VetaleAI.User">You / Ви</x:String>
<x:String x:Key="VetaleAI.Assistant">Vetale AI</x:String>
```

## Інтеграція з Tools

Вікно відкривається через ToolsMenu:
1. Користувач відкриває Tools (InstrumensButton)
2. Клікає на "Vetale AI Chat" 🤖
3. Вікно відкривається або активується, якщо вже відкрите

**Код інтеграції в `ToolsMainPage.axaml.cs`:**
```csharp
private static Windows.VetaleAIWindow? _vetaleAiWindowInstance;

private void OpenVetaleAIChat()
{
    // Створення або активація існуючого вікна
}
```

## TODO: Інтеграція LlamaSharp

### Модель
**Шлях:** `VetaleBrowser.AI/Model/gemma-3-1b-it-UD-Q2_K_XL.gguf`

### Необхідні кроки:
1. Додати NuGet пакет `LLamaSharp`
2. Реалізувати `InitializeAIModel()` для завантаження моделі
3. Реалізувати `GenerateResponseAsync()` для генерації відповідей
4. Додати підтримку reasoning mode
5. Реалізувати багатомовність відповідей

### Приклад структури:
```csharp
private LLamaContext? _llamaContext;
private LLamaModel? _llamaModel;

private void InitializeAIModel()
{
    var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
        "VetaleBrowser.AI", "Model", "gemma-3-1b-it-UD-Q2_K_XL.gguf");
    
    var parameters = new ModelParams(modelPath)
    {
        ContextSize = 2048,
        GpuLayerCount = 0 // CPU only
    };
    
    _llamaModel = LLamaWeights.LoadFromFile(parameters);
    _llamaContext = _llamaModel.CreateContext(parameters);
}
```

## Використання

1. **Відкриття чату:** Tools → Vetale AI Chat
2. **Написання повідомлення:** Введіть текст у поле вводу
3. **Надсилання:**
   - Натисніть Enter (або Shift+Enter для нового рядка)
   - Натисніть кнопку "Send"
4. **Налаштування:**
   - Увімкніть "Reasoning" для детальнішої відповіді
   - Виберіть мову відповіді з випадаючого списку
5. **Керування:**
   - "New Chat" - почати новий діалог
   - "Clear History" - очистити всі повідомлення

## Особливості

- ✅ Офлайн робота (локальна AI модель)
- ✅ Багатомовність інтерфейсу
- ✅ Вибір мови відповідей
- ✅ Режим міркування (reasoning)
- ✅ Стиль в дусі Claude
- ✅ Інтеграція з існуючою системою вікон
- ⏳ LlamaSharp інтеграція (в розробці)

## Дата створення
4 листопада 2025

