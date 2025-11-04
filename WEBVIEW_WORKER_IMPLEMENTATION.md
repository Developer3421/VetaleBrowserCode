# WebView Worker - DevTools Feature

## Опис

WebView Worker - це новий інструмент у складі DevTools, який надає окремий екземпляр WebView для тестування та налагодження веб-сторінок незалежно від основних вкладок браузера.

## Функціональність

### Основні можливості

1. **Незалежний WebView Worker**
   - Окремий екземпляр WebView, що працює незалежно від основних вкладок
   - Власний стек навігації (історія назад/вперед)
   - Власний WebViewManager для керування

2. **Синхронізація з активною вкладкою**
   - Автоматичне відстеження активної вкладки браузера
   - Відображення поточного URL та заголовка активної вкладки
   - Можливість швидкого завантаження URL з поточної вкладки

3. **Навігація**
   - Кнопки назад/вперед (активуються при наявності історії)
   - Оновлення сторінки
   - URL-бар з підтримкою прямого введення URL
   - Автоматичне розпізнавання URL vs пошукових запитів
   - Підтримка Enter для навігації

4. **Статус-бар**
   - Відображення поточного стану (готовий, завантаження, помилка)
   - Індикатор процесу завантаження
   - Іконки стану для швидкого розпізнавання

## Архітектура

### Компоненти

#### WebViewWorkerPage.axaml
- UI розмітка сторінки
- Панель інформації про поточну вкладку
- Панель керування навігацією
- Контейнер для WebView
- Статус-бар

#### WebViewWorkerPage.axaml.cs
- Ініціалізація WebView та WebViewManager
- Моніторинг активної вкладки через DispatcherTimer
- Обробка подій навігації
- Синхронізація стану UI

### Ключові класи

```csharp
public partial class WebViewWorkerPage : UserControl
{
    private WebView? _webView;                    // Екземпляр WebView
    private WebViewManager? _webViewManager;      // Менеджер для навігації
    private TabWorker? _currentActiveTab;         // Поточна активна вкладка
    private readonly DispatcherTimer _monitorTimer; // Моніторинг активної вкладки
}
```

## Використання API

### Ініціалізація WebView

```csharp
_webView = new WebView
{
    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
};

_webViewManager = new WebViewManager();
_webViewManager.Initialize(_webView);
```

### Підписка на події

```csharp
// Навігація
_webViewManager.Navigated += OnWebViewNavigated;

// Зміни властивостей WebView
_webView.PropertyChanged += OnWebViewPropertyChanged;

// Події активної вкладки
_currentActiveTab.TitleChanged += OnCurrentTabTitleChanged;
_currentActiveTab.AddressChanged += OnCurrentTabAddressChanged;
```

### Навігація

```csharp
// Асинхронна навігація
await _webViewManager.NavigateAsync(url);

// Керування історією
_webViewManager.GoBack();
_webViewManager.GoForward();
_webViewManager.Reload();
```

### Отримання TabsManager через рефлексію

```csharp
private TabsManager? GetTabsManager(MainWindow mainWindow)
{
    var field = mainWindow.GetType().GetField("_tabs", 
        System.Reflection.BindingFlags.NonPublic | 
        System.Reflection.BindingFlags.Instance);
    
    return field?.GetValue(mainWindow) as TabsManager;
}
```

## Локалізація

Додані ресурси для англійської та української мов:

### Ключі ресурсів

- `DevTools.Tab.WebViewWorker` - назва вкладки
- `DevTools.WebViewWorker.Title` - заголовок
- `DevTools.WebViewWorker.Description` - опис
- `DevTools.WebViewWorker.CurrentTab` - мітка поточної вкладки
- `DevTools.WebViewWorker.NoActiveTab` - повідомлення про відсутність активної вкладки
- `DevTools.WebViewWorker.LoadCurrentTab` - текст кнопки завантаження
- `DevTools.WebViewWorker.EnterUrl` - placeholder для URL
- `DevTools.WebViewWorker.Go` - кнопка переходу
- `DevTools.WebViewWorker.Back` - кнопка назад
- `DevTools.WebViewWorker.Forward` - кнопка вперед
- `DevTools.WebViewWorker.Refresh` - кнопка оновлення
- `DevTools.WebViewWorker.Placeholder` - текст-заповнювач
- `DevTools.WebViewWorker.Ready` - статус готовності
- `DevTools.WebViewWorker.WebViewReady` - WebView готовий
- `DevTools.WebViewWorker.Loading` - завантаження
- `DevTools.WebViewWorker.Loaded` - завантажено
- `DevTools.WebViewWorker.Refreshing` - оновлення
- `DevTools.WebViewWorker.Navigating` - навігація
- `DevTools.WebViewWorker.NoUrlToLoad` - немає URL
- `DevTools.WebViewWorker.LoadingFromTab` - завантаження з вкладки

## Інтеграція з DevTools

### Додавання вкладки до DevToolsWindow.axaml

```xml
<Button Classes="tab-button" x:Name="TabWebViewWorker" Click="ShowWebViewWorkerPage">
    <StackPanel Orientation="Horizontal" Spacing="6">
        <TextBlock Text="🌐" FontSize="14"/>
        <TextBlock Text="{DynamicResource DevTools.Tab.WebViewWorker}"/>
    </StackPanel>
</Button>
```

### Обробник в DevToolsWindow.axaml.cs

```csharp
private void ShowWebViewWorkerPage(object? sender, RoutedEventArgs e)
{
    if (_contentHost != null)
    {
        _contentHost.Content = new WebViewWorkerPage();
        SetActiveTab(this.FindControl<Button>("TabWebViewWorker"));
    }
}
```

## Особливості реалізації

### 1. Моніторинг активної вкладки

Використовується `DispatcherTimer` з інтервалом 500 мс для перевірки змін активної вкладки:

```csharp
private void OnMonitorTick(object? sender, EventArgs e)
{
    var activeTab = tabsManager.Active;
    
    if (activeTab != _currentActiveTab)
    {
        UnsubscribeFromCurrentTab();
        _currentActiveTab = activeTab;
        SubscribeToCurrentTab();
        UpdateCurrentTabInfo();
    }
}
```

### 2. Автоматичне розпізнавання URL

```csharp
if (!url.StartsWith("http://") && !url.StartsWith("https://"))
{
    if (url.Contains(".") && !url.Contains(" "))
    {
        url = "https://" + url; // Це URL
    }
    else
    {
        url = "https://www.google.com/search?q=" + Uri.EscapeDataString(url); // Це пошуковий запит
    }
}
```

### 3. Cleanup при відключенні

```csharp
protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnDetachedFromVisualTree(e);

    _monitorTimer.Stop();
    UnsubscribeFromCurrentTab();
    
    if (_webViewManager != null)
        _webViewManager.Navigated -= OnWebViewNavigated;
    
    if (_webView != null)
        _webView.PropertyChanged -= OnWebViewPropertyChanged;
}
```

## Стилізація

### Зелена тема для кнопок дій

```xml
<Style Selector="Button.action-button">
    <Setter Property="Background" Value="#4CAF50"/>
    <Setter Property="Foreground" Value="White"/>
</Style>
<Style Selector="Button.action-button:pointerover">
    <Setter Property="Background" Value="#66BB6A"/>
</Style>
```

### Адаптивний URL-бар

```xml
<TextBox Classes="url-box" Watermark="{DynamicResource DevTools.WebViewWorker.EnterUrl}"/>
```

## Тестування

### Сценарії тестування

1. **Базова навігація**
   - Введення URL та перехід
   - Використання кнопок назад/вперед
   - Оновлення сторінки

2. **Завантаження з активної вкладки**
   - Відкрити сайт у браузері
   - Відкрити DevTools > WebView Worker
   - Натиснути "Load from Current Tab"
   - Переконатися, що URL завантажено

3. **Синхронізація з вкладками**
   - Перемикання між вкладками
   - Перевірка оновлення інформації про поточну вкладку

4. **Пошукові запити**
   - Введення тексту без протоколу
   - Перевірка переходу на Google Search

## Переваги

1. **Ізольоване тестування** - можна тестувати веб-сторінки без впливу на основні вкладки
2. **Швидкий доступ** - завантаження URL з поточної вкладки в один клік
3. **Повний контроль** - незалежна навігація та історія
4. **Інформативність** - завжди видно, яка вкладка активна в браузері
5. **Гнучкість** - підтримка як URL, так і пошукових запитів

## Обмеження

1. WebView не має властивості `IsLoading` - статус завантаження визначається через події
2. Доступ до `TabsManager` через рефлексію (приватне поле)
3. Таймер моніторингу споживає ресурси (500 мс інтервал)

## Майбутні покращення

1. Додати індикатор прогресу завантаження (progress bar)
2. Показувати favicon сторінки
3. Додати історію навігації з можливістю вибору
4. Інтеграція з іншими DevTools (Console, Network)
5. Можливість відкриття URL в новій вкладці браузера
6. Збереження сесії WebView Worker між перезапусками

## Файли

- `VetaleBrowser.DevTools/Pages/WebViewWorkerPage.axaml` - UI розмітка
- `VetaleBrowser.DevTools/Pages/WebViewWorkerPage.axaml.cs` - логіка
- `VetaleBrowser.UI/Windows/DevToolsWindow.axaml` - інтеграція вкладки
- `VetaleBrowser.UI/Windows/DevToolsWindow.axaml.cs` - обробник вкладки
- `VetaleBrowser.UI/TranslationsDictionaries/Strings.en.axaml` - EN локалізація
- `VetaleBrowser.UI/TranslationsDictionaries/Strings.uk.axaml` - UK локалізація

## Висновок

WebView Worker - це потужний інструмент для розробників, що дозволяє тестувати та налагоджувати веб-сторінки в ізольованому середовищі з повним контролем навігації та синхронізацією з основними вкладками браузера.

