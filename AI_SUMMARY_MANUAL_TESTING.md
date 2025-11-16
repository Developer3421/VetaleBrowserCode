# Як протестувати AI-підсумок вручну

## Швидкий тест з коду

Додайте цей код у MainWindow.axaml.cs для швидкого тестування:

### 1. Знайдіть метод, де відкривається VetaleSearchResultsPage

Наприклад, у методі навігації або обробнику кнопки Vetale Search.

### 2. Додайте виклик тесту

```csharp
// Після створення/відкриття VetaleSearchResultsPage
var searchPage = /* ваш екземпляр VetaleSearchResultsPage */;

// Викликайте тестовий метод
await searchPage.TestAiSummaryAsync("тестовий запит");
```

### 3. Повний приклад

```csharp
private async void OpenVetaleSearch_Click(object sender, RoutedEventArgs e)
{
    var searchPage = new VetaleSearchResultsPage();
    
    // Відкрийте сторінку у вкладці
    // ... ваш код навігації ...
    
    // Зачекайте трохи, щоб сторінка повністю завантажилась
    await Task.Delay(500);
    
    // ТЕСТУВАННЯ: Викликайте тест AI-підсумку
    await searchPage.TestAiSummaryAsync("програмування");
}
```

## Що робить тестовий метод

1. ✅ Перевіряє, чи знайдені UI елементи (_aiSummaryText, _aiSummaryStatusText)
2. ✅ Пише в Debug консоль стан елементів
3. ✅ Встановлює тестовий текст "ТЕСТОВИЙ ТЕКСТ - якщо це видно, UI працює!"
4. ✅ Чекає 1 секунду (щоб ви могли побачити тестовий текст)
5. ✅ Викликає LoadAiSummaryAsync() для реального запиту до Qwen API

## Очікувані логи в Debug консолі

```
[VetaleSearchResultsPage] === TESTING AI SUMMARY ===
[VetaleSearchResultsPage] Test query: програмування
[VetaleSearchResultsPage] UI Check:
  _aiSummaryText: True
  _aiSummaryStatusText: True
  Current text: 'AI-підсумок з'явиться тут після першого пошуку.'
[VetaleSearchResultsPage] Testing direct text assignment...
  Text set to: 'ТЕСТОВИЙ ТЕКСТ - якщо це видно, UI працює!'
[VetaleSearchResultsPage] Testing AI service...
[VetaleSearchResultsPage] Requesting AI summary for: програмування
[VetaleSearchResultsPage] UpdateAiSummaryUI called with State=Loading
[QwenAiSummary] Sending request for query: програмування
[QwenAiSummary] Response: {"data":["..."]}
[QwenAiSummary] Summary generated successfully!
[VetaleSearchResultsPage] UpdateAiSummaryUI called with State=Ready
[VetaleSearchResultsPage] SUCCESS: AI summary ready!
[VetaleSearchResultsPage] === TEST COMPLETE ===
```

## Альтернативний спосіб: через Immediate Window

У режимі Debug у Visual Studio:

1. Поставте breakpoint у методі VetaleSearchResultsPage
2. Запустіть програму (F5)
3. Коли breakpoint спрацює, відкрийте Immediate Window (Ctrl+Alt+I)
4. Введіть:

```csharp
await this.TestAiSummaryAsync("тест")
```

## Якщо UI елементи не знайдені (False)

Це означає, що XAML не завантажився або InitializeControls() викликано занадто рано.

**Рішення:**
- Переконайтеся, що InitializeComponent() викликано у конструкторі
- Перевірте, що InitializeControls() викликано ПІСЛЯ InitializeComponent()
- Перевірте, що x:Name у XAML збігаються з іменами у FindControl<>()

## Якщо текст не відображається у UI

Навіть якщо _aiSummaryText.Text встановлено, текст може не відображатись через:

1. **Елемент прихований** - перевірте Visibility у XAML
2. **Батьківський контейнер згорнутий** - перевірте всі Border/StackPanel вище
3. **Z-index проблема** - інший елемент перекриває текст
4. **Колір тексту збігається з фоном** - перевірте Foreground

## Швидкий фікс: Змусити UI оновитись

Додайте у UpdateAiSummaryUI():

```csharp
private void UpdateAiSummaryUI(AiSearchSummary summary)
{
    // ... існуючий код ...
    
    // FORCE UI REFRESH
    if (_aiSummaryText != null)
    {
        _aiSummaryText.InvalidateVisual();
        _aiSummaryText.InvalidateMeasure();
    }
}
```

## Перевірка через UI Inspector

У режимі Debug:
1. Натисніть кнопку "Live Visual Tree" у Visual Studio
2. Знайдіть VetaleSearchResultsPage
3. Розгорніть дерево до TextBlock з x:Name="AiSummaryText"
4. Перевірте властивості:
   - Text: має бути ваш текст
   - Visibility: має бути Visible
   - Foreground: не має збігатись з Background
   - ActualWidth/ActualHeight: не має бути 0

---

**Після тестування не забудьте видалити виклик TestAiSummaryAsync() з продакшн коду!**

