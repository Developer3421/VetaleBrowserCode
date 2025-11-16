# Тест Qwen API - Діагностика помилки "не знайдено"

## Швидка перевірка API через PowerShell

### Спосіб 1: Простий GET запит (перевірка доступності)
```powershell
# Перевірка чи сервіс взагалі доступний
Invoke-WebRequest -Uri "https://qwen-qwen2-5-7b-instruct.hf.space" -Method Get
```

### Спосіб 2: POST запит з даними (як в коді)
```powershell
$body = @{
    data = @("Розкажи коротко про програмування")
} | ConvertTo-Json

Write-Host "=== REQUEST ===" -ForegroundColor Green
Write-Host "URL: https://qwen-qwen2-5-7b-instruct.hf.space/run/predict"
Write-Host "Body: $body"
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "https://qwen-qwen2-5-7b-instruct.hf.space/run/predict" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -Verbose

    Write-Host "=== RESPONSE ===" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 10)
    
    if ($response.data -and $response.data.Length -gt 0) {
        Write-Host ""
        Write-Host "=== AI SUMMARY ===" -ForegroundColor Cyan
        Write-Host $response.data[0]
    }
} catch {
    Write-Host "=== ERROR ===" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode.value__)"
    Write-Host "Status Description: $($_.Exception.Response.StatusDescription)"
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "Full Exception:" -ForegroundColor Yellow
    Write-Host $_.Exception
}
```

### Спосіб 3: Перевірка різних endpoints
```powershell
$endpoints = @(
    "https://qwen-qwen2-5-7b-instruct.hf.space/run/predict",
    "https://qwen-qwen2-5-7b-instruct.hf.space/api/predict",
    "https://qwen-qwen2-5-7b-instruct.hf.space/predict",
    "https://qwen-qwen2-5-7b-instruct.hf.space/api/run/predict"
)

$body = @{ data = @("test") } | ConvertTo-Json

foreach ($endpoint in $endpoints) {
    Write-Host "Testing: $endpoint" -ForegroundColor Yellow
    try {
        $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
        Write-Host "  ✅ SUCCESS!" -ForegroundColor Green
        Write-Host "  Response: $($response | ConvertTo-Json -Compress)"
        break
    } catch {
        Write-Host "  ❌ FAILED: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
    Write-Host ""
}
```

## Можливі причини помилки 404

### 1. API endpoint змінився
HuggingFace Spaces може змінити endpoint. Спробуйте:
- `/run/predict` → `/api/predict`
- `/run/predict` → `/predict`
- Перевірте документацію: https://huggingface.co/spaces/Qwen/Qwen2.5-7B-Instruct

### 2. Сервіс вимкнено або перезапускається
HuggingFace Spaces може бути:
- Тимчасово недоступний
- В режимі "sleeping" (потрібен час для пробудження)
- Перенесено на інший домен

### 3. Потрібна авторизація
Можливо API тепер потребує токен:
```powershell
$headers = @{
    "Authorization" = "Bearer YOUR_HF_TOKEN"
}

Invoke-RestMethod -Uri "..." -Method Post -Body $body -Headers $headers
```

### 4. Змінився формат запиту
Спробуйте різні формати:

**Формат 1 (поточний):**
```json
{"data": ["текст"]}
```

**Формат 2:**
```json
{"inputs": "текст"}
```

**Формат 3:**
```json
{"query": "текст", "parameters": {}}
```

## Альтернативні рішення

### Варіант 1: Використати офіційний Qwen API
```
https://dashscope.aliyuncs.com/api/v1/services/aigc/text-generation/generation
```

### Варіант 2: Локальна модель через Ollama
```bash
ollama pull qwen2.5:7b
```

Потім в C#:
```csharp
var response = await httpClient.PostAsync(
    "http://localhost:11434/api/generate",
    new StringContent(
        JsonSerializer.Serialize(new { 
            model = "qwen2.5:7b", 
            prompt = prompt 
        })
    )
);
```

### Варіант 3: Інший HuggingFace Space
Знайдіть активний Space з Qwen:
```
https://huggingface.co/spaces?search=qwen
```

## Діагностика в коді

Запустіть браузер і шукайте в Debug консолі:

```
[QwenAiSummary] API URL: https://...
[QwenAiSummary] Response Status Code: 404 (404)
[QwenAiSummary] Error reason: Not Found
[QwenAiSummary] 404 Not Found - API endpoint may have changed
```

Якщо бачите 404 → endpoint змінився або сервіс недоступний.

## Швидкий фікс: Зміна URL в коді

Відредагуйте `QwenAiSummaryService.cs`:

```csharp
// Знайдіть робочий endpoint через PowerShell тести вище
private const string ApiUrl = "https://НОВИЙ_URL/predict";
```

---

**Збережіть цей скрипт як test-qwen-api.ps1 і запустіть для діагностики!**

