# План інтеграції favicon.im (показ favicon у вкладках)

Мета: замінити дефолтну «глобус»-іконку в шаблоні вкладки на favicon поточного сайту, який відкрито у WebView2, з кешуванням, фолбеками і без впливу на існуючу верстку вкладки.

## 1) Вимоги та цільова поведінка
- Для кожної вкладки відображати favicon сайту за її поточним URL.
- Не ламати існуючий шаблон вкладки (рамки, відступи, кольоровий фон зберігаються).
- Підтримати кеш: пам’ять + диск (щоб уникати зайвих мережевих звернень).
- Використовувати favicon.im як основне джерело; на випадок помилок — мати фолбек (наприклад, Google S2).
- Таймаут запитів ≤ 5 сек; у разі збою залишати дефолтну іконку.
- Вибір розміру іконки залежно від DPI (16px стандарт, 32px на high‑DPI).
- Підтримка як мінімум форматів PNG/ICO, повернених сервісом.

## 2) Архітектура рішення

### 2.1. Сервіс для отримання favicon
- Інтерфейс `IFaviconService` з методом:
  - `Task<IImage?> GetFaviconAsync(Uri pageUri, int size = 16, CancellationToken ct = default)`
- Реалізація `FaviconService`:
  - Джерело іконки: `https://favicon.im/{host}?size={size}` (налаштовуваний шаблон).
  - Фолбек: `https://www.google.com/s2/favicons?sz={size}&domain={host}`.
  - Кешування:
    - In‑memory (`ConcurrentDictionary<string, byte[]>`) на сесію.
    - Дисковий кеш у `%LOCALAPPDATA%/VetaleBrowser/favicon-cache` за ключем `{host}-{size}.png`.
  - Обробка помилок: таймаут, 4xx/5xx, пустий контент → повернути `null`.
  - Конфігурація: шаблон endpoint, таймаут, розмір за замовчуванням.

### 2.2. Зміни у UI компонента вкладки
- У `VetaleBrowser.UI/Еlements/Tab.axaml.cs` додати StyledProperty `FaviconSource: IImage?`.
- У `VetaleBrowser.UI/Еlements/Tab.axaml` у контейнері favicon замінити `TextBlock` з «🌐» на `Image` з `Source={TemplateBinding FaviconSource}`; зберегти розміри 16x16, `Stretch=Uniform` і фон бордера.

### 2.3. Зв’язка з навігацією WebView2
- На події завершення навігації (`NavigationCompleted`/`SourceChanged`) визначати `Uri` поточної сторінки вкладки.
- Вирахувати бажаний розмір іконки: 16 або 32 залежно від `RenderScaling` (`VisualRoot.RenderScaling` в Avalonia).
- Викликати `IFaviconService.GetFaviconAsync(uri, size)` і встановити `tab.FaviconSource`.
- У разі помилки — не змінювати `FaviconSource` (залишиться дефолтний вигляд).

## 3) Деталі реалізації

### 3.1. Контракти
- Вхід: `Uri pageUri` (приймаються `http/https`; `file:` та пустий host ігноруються).
- Вихід: `IImage?` (null означає — використати дефолтну іконку).
- Кеш‑ключ: `lower(host)` + `size`.
- Помилки: не кидати зовні; логувати (за потреби) та повертати `null`.

### 3.2. Правила нормалізації host
- Взяти `Uri.Host`, привести до lower‑case.
- Ігнорувати префікс `www.` для ключа кешу не потрібно (favicon для `www.` часто збігається, але сервіс приймає обидва варіанти). Допустимий компроміс: кешувати як є.

### 3.3. DPI/Scale
- Якщо `RenderScaling >= 1.5` → запитувати 32px, інакше 16px.
- На майбутнє: додати реакцію на зміну scale (не критично на перший етап).

### 3.4. Кешування
- Memory‑кеш: миттєві повторні звернення в межах процесу без IO/мережі.
- Disk‑кеш: збереження сирих байтів PNG/ICO; невдалий запис ігнорувати.
- TTL: на старті — без TTL (простота). За потреби додати політику інвалідції (напр., 7 днів).

### 3.5. Fallback‑логіка
1) Спроба `favicon.im`.
2) Якщо `null` → спроба Google S2 (`/s2/favicons`).
3) Якщо `null` → повернути `null` і не ламати UI.

### 3.6. Налаштування
- Опції винести в конфіг (пізніше):
  - `FaviconEndpointTemplate` (default: `https://favicon.im/{0}?size={1}`).
  - `FaviconRequestTimeoutSeconds` (default: 5).
  - `FaviconDiskCacheDir` (default: `%LOCALAPPDATA%/VetaleBrowser/favicon-cache`).

## 4) План змін у коді (мінімальний)

1) Додати файли сервісу:
   - `VetaleBrowser.UI/Services/IFaviconService.cs`
   - `VetaleBrowser.UI/Services/FaviconService.cs`

2) Оновити `Tab`:
   - `VetaleBrowser.UI/Еlements/Tab.axaml.cs`: додати StyledProperty `FaviconSource` типу `IImage?`.
   - `VetaleBrowser.UI/Еlements/Tab.axaml`: у блоці favicon замінити `TextBlock` на `Image` з `{TemplateBinding FaviconSource}` (залишити фон Border, розмір 16x16 та маржини).

3) Підключити до WebView2:
   - У місці, де створюється/ведеться WebView2 для вкладки (координатор вкладок або вікно):
     - Підписатися на `NavigationCompleted`.
     - Витягти `Uri` з поточного `Source`.
     - Викликати `IFaviconService.GetFaviconAsync(uri, size)`.
     - Встановити `currentTab.FaviconSource = image`.

4) DI/життєвий цикл:
   - Створити один екземпляр `FaviconService` на додаток (singleton) і прокидати в координатор вкладок/VM.

## 5) Тестування

### 5.1. Unit‑тести (мінімум)
- Побудова URL для `favicon.im` та Google S2 з різними хостами й розмірами.
- Нормалізація host та ключ кешу.
- Кеш: після першого успішного завантаження другий виклик — без HTTP (перевірити memory‑кеш; disk‑кеш — інтеграційно).
- Fallback: імітувати 404/timeout для `favicon.im` — має спрацювати Google S2.

### 5.2. Інтеграційні/ручні тести
- Сайти з робочим favicon: `github.com`, `microsoft.com`.
- Сайти без favicon або з редиректами: перевірити, що UI лишається стабільним.
- Високий DPI: на екрані 150% масштаб — має взятися 32px і виглядати чітко.

## 6) Критерії приймання (DoD)
- Вкладка показує favicon для більшості сайтів; у разі збою — дефолтний вигляд без збоїв.
- Повторне відкриття того самого домену відбувається без додаткових запитів (кеш працює).
- Немає зависань UI при повільній мережі (асинхронні виклики, таймаути).
- На high‑DPI іконка не розмита.

## 7) Ризики і альтернативи
- favicon.im може бути недоступним або мати ліміти — для цього є Google S2 фолбек; за потреби можна додати ще `https://icons.duckduckgo.com/ip3/{host}.ico` чи `https://icon.horse/icon/{host}`.
- Деякі сайти віддають лише ICO з низькою роздільністю — вигляд може бути скромним; можна підвищити розмір запиту до 32px/48px.
- Питання ліцензій/умов використання сторонніх сервісів — перевірити і зафіксувати в документації.

## 8) Подальші покращення (не обов’язково на перший реліз)
- TTL для дискового кешу (авто‑очистка, наприклад, раз на тиждень).
- Паралельні запити для декількох вкладок з дедуплікацією (coalescing).
- Визначення favicon з HTML `<link rel="icon">` як пріоритетного джерела (локальний парсер сторінки) — складніше, але більш точне.
- Телеметрія успішності/збоїв завантаження іконок (внутрішній лог).

---

## Додаток A: Скелет інтерфейсу і точки інтеграції (для довідки)

Інтерфейс:
```csharp
public interface IFaviconService
{
    Task<IImage?> GetFaviconAsync(Uri pageUri, int size = 16, CancellationToken ct = default);
}
```

StyledProperty у `Tab`:
```csharp
public static readonly StyledProperty<IImage?> FaviconSourceProperty =
    AvaloniaProperty.Register<Tab, IImage?>(nameof(FaviconSource));

public IImage? FaviconSource
{
    get => GetValue(FaviconSourceProperty);
    set => SetValue(FaviconSourceProperty, value);
}
```

Фрагмент XAML для контейнера favicon:
```xml
<Border Width="16" Height="16" Margin="0,0,8,0" CornerRadius="3" Background="#A78BFA">
    <Image Source="{TemplateBinding FaviconSource}"
           Width="16" Height="16"
           Stretch="Uniform"
           HorizontalAlignment="Center"
           VerticalAlignment="Center" />
</Border>
```

Хук на подію навігації WebView2:
```csharp
webView.NavigationCompleted += async (_, __) =>
{
    if (Uri.TryCreate(webView.Source, UriKind.Absolute, out var uri))
    {
        var scale = (webView.VisualRoot as Visual)?.RenderScaling ?? 1.0;
        var size = scale >= 1.5 ? 32 : 16;
        tab.FaviconSource = await _favicons.GetFaviconAsync(uri, size);
    }
};
```

