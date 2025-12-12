# API Keys Configuration Guide / Налаштування API Ключів

## 📍 Де вставляти дефолтні API ключі

### 1. Gemini API Key (для AI-відповідей у Vetale Search)

**Файл:** `VetaleBrowser.Search/Services/GeminiAiSummaryService.cs`

**Рядок ~46:**
```csharp
// ВСТАВТЕ ВАШ GEMINI API КЛЮЧ ТУТ:
private const string DefaultApiKey = "AIzaSy..."; // ← ВАШ КЛЮЧ
```

**Як отримати:**
1. Відкрийте: https://aistudio.google.com/app/apikey
2. Натисніть "Get API key" або "Create API key"
3. Скопіюйте згенерований ключ

**Ліміти (безкоштовно):**
- 15 запитів/хвилину
- 1 мільйон токенів/місяць
- 1500 запитів/день

---

### 2. Pexels API Key (для пошуку зображень)

**Файл:** `VetaleBrowser.Search/Services/ImageSearchServices.cs`

**Рядок ~31:**
```csharp
public const string PexelsApiKey = ""; // ← ВСТАВТЕ ВАШ PEXELS API KEY ТУТ
```

**Як отримати:**
1. Відкрийте: https://www.pexels.com/api/
2. Зареєструйтесь та увійдіть
3. Створіть новий API ключ

**Ліміти (безкоштовно):**
- 200 запитів/годину
- 20,000 запитів/місяць

---

### 3. Unsplash Access Key (для пошуку зображень)

**Файл:** `VetaleBrowser.Search/Services/ImageSearchServices.cs`

**Рядок ~38:**
```csharp
public const string UnsplashAccessKey = ""; // ← ВСТАВТЕ ВАШ UNSPLASH ACCESS KEY ТУТ
```

**Як отримати:**
1. Відкрийте: https://unsplash.com/developers
2. Зареєструйтесь та створіть додаток
3. Скопіюйте Access Key

**Ліміти (безкоштовно):**
- 50 запитів/годину (демо)
- Необмежено (production, потрібно подати заявку)

---

### 4. YouTube Data API Key (для пошуку відео)

**Файл:** `VetaleBrowser.Search/Services/ImageSearchServices.cs`

**Рядок ~45:**
```csharp
public const string YouTubeApiKey = ""; // ← ВСТАВТЕ ВАШ YOUTUBE API KEY ТУТ
```

**Як отримати:**
1. Відкрийте: https://console.cloud.google.com/apis/library/youtube.googleapis.com
2. Увімкніть YouTube Data API v3
3. Перейдіть до Credentials → Create API Key

**Ліміти (безкоштовно):**
- 10,000 одиниць/день

---

## 🔐 Пріоритет завантаження ключів

Для кожного сервісу ключі завантажуються в такому порядку:

1. **Дефолтні константи** (у коді) - найвищий пріоритет
2. **База даних** (api_keys.db) - якщо користувач ввів через UI
3. **Змінні середовища** - для CI/CD та контейнерів
4. **Mock/Fallback** - якщо нічого не знайдено

## 🌍 Змінні середовища

Альтернативно можна задати ключі через ENV:

```bash
# Windows PowerShell
$env:GEMINI_API_KEY = "your-gemini-key"
$env:VETALE_PEXELS_API_KEY = "your-pexels-key"
$env:VETALE_UNSPLASH_ACCESS_KEY = "your-unsplash-key"
$env:YOUTUBE_API_KEY = "your-youtube-key"

# Linux/macOS
export GEMINI_API_KEY="your-gemini-key"
export VETALE_PEXELS_API_KEY="your-pexels-key"
export VETALE_UNSPLASH_ACCESS_KEY="your-unsplash-key"
export YOUTUBE_API_KEY="your-youtube-key"
```

## 📦 Обмеження результатів

- **Зображення:** максимум 50 на сторінку (`ImageSearchServiceFactory.MaxResultsPerPage`)
- **Відео:** максимум 50 на сторінку (`VideoSearchPage.PageSize`)

## 💾 База даних API ключів

Користувач може ввести свої ключі через UI вікна. Вони зберігаються в:
- **Шлях:** `%AppData%/VetaleBrowser/Data/api_keys.db`
- **Шифрування:** AES-256
- **Формат:** LiteDB

## ⚠️ Важливо

- НЕ комітьте реальні API ключі в публічні репозиторії!
- Використовуйте `.gitignore` або секретні змінні для CI/CD
- Для production рекомендується використовувати ENV або secure vault

