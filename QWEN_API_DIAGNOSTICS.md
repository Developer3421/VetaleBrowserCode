# Тестування Qwen API та діагностика проблеми

## Кроки для діагностики:

### 1. Запустіть браузер і відкрийте Debug консоль
У Visual Studio: View → Output → Show output from: Debug

### 2. Перейдіть на сторінку Vetale Search
Натисніть на кнопку "Vetale Search" у головному вікні

### 3. Введіть тестовий запит
Наприклад: "веб розробка" або "програмування"

### 4. Перегляньте логи в Debug консолі

Ви повинні побачити таку послідовність логів:

```
[VetaleSearchResultsPage] AI Summary UI Elements:
  - AiSummaryStatusText: FOUND
  - AiSummaryText: FOUND
  - AiSummaryStatePanel: FOUND
  - AiSummaryWarningText: FOUND

[VetaleSearchResultsPage] SetSearchQuery called with: 'веб розробка'
[VetaleSearchResultsPage] Requesting AI summary for: веб розробка

[VetaleSearchResultsPage] UpdateAiSummaryUI called with State=Loading
[VetaleSearchResultsPage] SummaryText length=23
[VetaleSearchResultsPage] Setting SummaryText to: Генерується підсумок...

[QwenAiSummary] Sending request for query: веб розробка
[QwenAiSummary] Request payload: {"data":["Ти асистент..."]}
[QwenAiSummary] Response: {"data":["Згенерований текст..."]}
[QwenAiSummary] Response length: XXX
[QwenAiSummary] JSON parsed successfully
[QwenAiSummary] 'data' property found
[QwenAiSummary] Data array length: 1
[QwenAiSummary] Raw generated text: ...
[QwenAiSummary] Summary generated successfully!

[VetaleSearchResultsPage] UpdateAiSummaryUI called with State=Ready
[VetaleSearchResultsPage] SummaryText length=XXX
[VetaleSearchResultsPage] Setting SummaryText to: ...
[VetaleSearchResultsPage] SUCCESS: AI summary ready!
[VetaleSearchResultsPage] Current text in UI: '...'
```

## Можливі проблеми та рішення:

### Проблема 1: UI елементи NOT FOUND
**Симптом:** 
```
[VetaleSearchResultsPage] ERROR: _aiSummaryText is NULL!
```

**Рішення:** 
XAML файл не завантажився або імена елементів не співпадають. Перевірте:
- `x:Name="AiSummaryStatusText"` в XAML
- `x:Name="AiSummaryText"` в XAML

### Проблема 2: API повертає помилку
**Симптом:**
```
[QwenAiSummary] API error: 404
```

**Рішення:**
API endpoint недоступний. Перевірте інтернет-з'єднання.

### Проблема 3: Порожня відповідь від API
**Симптом:**
```
[QwenAiSummary] ERROR: Data array is empty!
```

**Рішення:**
API повернув порожній масив. Можливо, промпт занадто складний або API перевантажений.

### Проблема 4: Timeout
**Симптом:**
```
[VetaleSearchResultsPage] AI summary error: The operation was canceled.
```

**Рішення:**
API не відповідає протягом 30 секунд. Збільшіть timeout або спробуйте пізніше.

### Проблема 5: JSON parsing error
**Симптом:**
```
[QwenAiSummary] Exception: Unexpected character...
```

**Рішення:**
API повернув некоректний JSON. Перегляньте raw response.

## Ручне тестування API

Якщо автоматичне тестування не працює, спробуйте вручну:

### PowerShell:
```powershell
$body = @{
    data = @("Розкажи про веб розробку")
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://qwen-qwen2-5-7b-instruct.hf.space/run/predict" -Method Post -Body $body -ContentType "application/json"

Write-Host $response.data[0]
```

### C# Interactive (dotnet-script):
```csharp
#r "nuget: System.Text.Json, 8.0.0"

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

var client = new HttpClient();
var payload = new { data = new[] { "Розкажи про веб розробку" } };
var json = JsonSerializer.Serialize(payload);
var response = await client.PostAsync(
    "https://qwen-qwen2-5-7b-instruct.hf.space/run/predict",
    new StringContent(json, Encoding.UTF8, "application/json")
);
var result = await response.Content.ReadAsStringAsync();
Console.WriteLine(result);
```

## Очікуваний результат

Після успішного тестування ви побачите в UI блоці "Vetale AI підсумок":

**Статус:** "Підсумок згенеровано (14:35)"

**Текст:** 
```
Користувач шукає інформацію про веб розробку - створення 
веб-сайтів та веб-додатків. Рекомендується почати з основ 
HTML, CSS та JavaScript, потім перейти до фреймворків.
```

(Текст буде українською мовою, згенерований Qwen AI)

