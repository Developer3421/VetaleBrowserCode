# Vetale Search - Архітектура

## 🏗️ Структура компонентів

```
┌─────────────────────────────────────────────────────────────┐
│                    VetaleBrowser (UI)                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌───────────────────────────────────────────────────┐     │
│  │      SearchEngineSettingsPage                     │     │
│  │  ┌─────────────────────────────────────────────┐  │     │
│  │  │  📍 Vetale Search Button                    │  │     │
│  │  │  Opens ──────────────────────┐              │  │     │
│  │  └─────────────────────────────│───────────────┘  │     │
│  └────────────────────────────────│──────────────────┘     │
│                                    │                        │
│                                    ▼                        │
│  ┌────────────────────────────────────────────────────┐    │
│  │        VetaleSearchHomePage                        │    │
│  │  ┌──────────────────────────────────────────────┐  │    │
│  │  │  🎨 Оранжево-фіолетовий градієнт             │  │    │
│  │  │  🔍 Пошукове поле                            │  │    │
│  │  │  📋 Вибір пошукової системи:                 │  │    │
│  │  │     • Vetale Search (локальний) ◄──┐         │  │    │
│  │  │     • Google                       │         │  │    │
│  │  │     • Bing                         │         │  │    │
│  │  │     • DuckDuckGo                   │         │  │    │
│  │  │     • Yandex                       │         │  │    │
│  │  │  🔘 Пошук / Мені пощастить          │         │  │    │
│  │  └──────────────────────────────┬─────┘         │  │    │
│  └─────────────────────────────────│───────────────┘  │    │
│                                    │                  │    │
│              Якщо Vetale Search    │   Якщо інші      │    │
│                     │              │     │            │    │
│                     ▼              │     ▼            │    │
│  ┌─────────────────────────────────│────────────┐    │    │
│  │   VetaleSearchResultsPage       │            │    │    │
│  │  ┌───────────────────────────┐  │  WebView   │    │    │
│  │  │  📊 Статистика пошуку     │  │  Navigate  │    │    │
│  │  │  📑 Результати з БД       │  │  to URL    │    │    │
│  │  │  📄 Пагінація             │  │            │    │    │
│  │  └───────────┬───────────────┘  │            │    │    │
│  └──────────────│──────────────────┴────────────┘    │    │
└─────────────────│───────────────────────────────────────┘
                  │
                  │ Query
                  ▼
┌─────────────────────────────────────────────────────────────┐
│              SearchIndexService                             │
├─────────────────────────────────────────────────────────────┤
│  Methods:                                                   │
│  • SearchAsync(query) ──────────┐                          │
│  • IndexPageAsync(...)          │                          │
│  • GetSearchHistoryAsync()      │                          │
│  • GetAutocompleteSuggestionsAsync() │                     │
└─────────────────────────────────│───────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────┐
│                    LiteDB (vetale_search.db)                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌────────────────────┐  ┌────────────────────┐           │
│  │   SearchIndex      │  │   SearchQuery      │           │
│  ├────────────────────┤  ├────────────────────┤           │
│  │ • Url              │  │ • Query            │           │
│  │ • Title            │  │ • SearchEngine     │           │
│  │ • Content          │  │ • SearchedAt       │           │
│  │ • Description      │  │ • ResultsCount     │           │
│  │ • Keywords         │  └────────────────────┘           │
│  │ • VisitCount       │                                   │
│  │ • RelevanceScore   │  ┌────────────────────┐           │
│  └────────────────────┘  │  SearchKeyword     │           │
│                          ├────────────────────┤           │
│                          │ • SearchIndexId    │           │
│                          │ • Keyword          │           │
│                          │ • Frequency        │           │
│                          │ • Weight           │           │
│                          └────────────────────┘           │
└─────────────────────────────────────────────────────────────┘
```

## 📊 Потік даних

### 1. Індексація сторінки
```
WebView (Page Load)
    │
    ├─> Extract: URL, Title, Content, Description, Keywords
    │
    ▼
SearchIndexService.IndexPageAsync()
    │
    ├─> Clean & Process Content
    ├─> Extract Keywords
    ├─> Calculate Relevance Score
    │
    ▼
LiteDB: Insert/Update SearchIndex + SearchKeywords
```

### 2. Пошук
```
User Input (Query)
    │
    ▼
VetaleSearchHomePage
    │
    ├─> If Vetale Search selected
    │   │
    │   ▼
    │   VetaleSearchResultsPage.SetSearchQuery()
    │       │
    │       ▼
    │   SearchIndexService.SearchAsync()
    │       │
    │       ├─> Split query into terms
    │       ├─> Search in: Title, Content, Description, Keywords
    │       ├─> Calculate match scores
    │       ├─> Order by relevance
    │       │
    │       ▼
    │   Return List<SearchIndex>
    │       │
    │       ▼
    │   Display Results in UI
    │
    └─> If External search engine
        │
        ▼
        Navigate WebView to search URL
```

### 3. Історія пошуків
```
User performs search
    │
    ▼
SearchIndexService.SaveSearchQueryAsync()
    │
    ├─> Store: Query, Engine, Date, ResultsCount
    │
    ▼
LiteDB: Insert SearchQuery
    │
    ▼
Can be retrieved for:
    • Autocomplete suggestions
    • Popular queries
    • Search history page
```

## 🎯 Ключові особливості

### Ранжування результатів
```
Score = (Title matches × 10) +
        (Description matches × 5) +
        (Keywords matches × 3) +
        (Content matches × 2) +
        (Visit count × 1)
        
Sort by: Score DESC, RelevanceScore DESC, LastVisited DESC
```

### Обмеження
- **Max indexed pages**: 50,000
- **Max content length**: 5,000 символів
- **Max keywords per page**: 100
- **Max database size**: 1 GB
- **Auto-cleanup**: Видалення найстаріших при перевищенні ліміту

### Оптимізація
- Індекси на: Url, Title, IndexedAt, RelevanceScore, Keyword
- Shared connection для LiteDB
- Async операції
- Batch cleanup (1000 записів за раз)

## 🔄 Життєвий цикл

1. **Startup**: Ініціалізація SearchIndexService
2. **Browse**: Автоматична індексація відвіданих сторінок
3. **Search**: Швидкий пошук в локальній БД
4. **Results**: Ранжовані результати з підсвічуванням
5. **History**: Збереження запитів для автодоповнення

---

**Примітка**: Всі компоненти створені та готові до інтеграції!

