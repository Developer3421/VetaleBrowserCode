# Qwen API Quick Test
# Запустіть цей скрипт щоб знайти робочий endpoint

Write-Host "=== QWEN API DIAGNOSTICS ===" -ForegroundColor Cyan
Write-Host ""

# Список можливих endpoints
$endpoints = @(
    "https://qwen-qwen2-5-7b-instruct.hf.space/run/predict",
    "https://qwen-qwen2-5-7b-instruct.hf.space/api/predict",
    "https://qwen-qwen2-5-7b-instruct.hf.space/predict",
    "https://qwen-qwen2-5-7b-instruct.hf.space/api/run/predict",
    "https://qwen-qwen2-5-7b-instruct.hf.space/call/predict"
)

$body = @{
    data = @("Привіт! Розкажи коротко про себе.")
} | ConvertTo-Json

Write-Host "Request Body:" -ForegroundColor Yellow
Write-Host $body
Write-Host ""
Write-Host "Testing endpoints..." -ForegroundColor Yellow
Write-Host ""

$workingEndpoint = $null

foreach ($endpoint in $endpoints) {
    Write-Host "[$($endpoints.IndexOf($endpoint) + 1)/$($endpoints.Count)] Testing: " -NoNewline -ForegroundColor White
    Write-Host $endpoint -ForegroundColor Cyan
    
    try {
        $response = Invoke-RestMethod -Uri $endpoint `
            -Method Post `
            -Body $body `
            -ContentType "application/json" `
            -TimeoutSec 30 `
            -ErrorAction Stop
        
        Write-Host "    ✅ SUCCESS!" -ForegroundColor Green
        Write-Host "    Response type: $($response.GetType().Name)" -ForegroundColor Gray
        
        if ($response.data) {
            Write-Host "    Response data length: $($response.data.Length)" -ForegroundColor Gray
            if ($response.data.Length -gt 0) {
                Write-Host "    First item: $($response.data[0].Substring(0, [Math]::Min(50, $response.data[0].Length)))..." -ForegroundColor Gray
                $workingEndpoint = $endpoint
                Write-Host ""
                Write-Host "=== WORKING ENDPOINT FOUND ===" -ForegroundColor Green
                Write-Host $endpoint -ForegroundColor Cyan
                break
            }
        } else {
            Write-Host "    ⚠️ Response has no 'data' property" -ForegroundColor Yellow
            Write-Host "    Response: $($response | ConvertTo-Json -Compress)" -ForegroundColor Gray
        }
        
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $statusDesc = $_.Exception.Response.StatusDescription
        
        if ($statusCode) {
            Write-Host "    ❌ HTTP $statusCode - $statusDesc" -ForegroundColor Red
        } else {
            Write-Host "    ❌ $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host ""
}

if ($workingEndpoint) {
    Write-Host ""
    Write-Host "=== FULL TEST WITH WORKING ENDPOINT ===" -ForegroundColor Cyan
    Write-Host ""
    
    $testBody = @{
        data = @("Ти асистент пошукової системи Vetale Search. Користувач ввів запит: 'програмування'. Дай короткий підсумок (2-3 речення) українською мовою.")
    } | ConvertTo-Json
    
    Write-Host "Request:" -ForegroundColor Yellow
    Write-Host $testBody
    Write-Host ""
    
    try {
        $response = Invoke-RestMethod -Uri $workingEndpoint `
            -Method Post `
            -Body $testBody `
            -ContentType "application/json" `
            -TimeoutSec 30
        
        Write-Host "Response:" -ForegroundColor Yellow
        Write-Host ($response | ConvertTo-Json -Depth 5)
        Write-Host ""
        
        if ($response.data -and $response.data.Length -gt 0) {
            Write-Host "=== AI GENERATED SUMMARY ===" -ForegroundColor Green
            Write-Host $response.data[0] -ForegroundColor White
            Write-Host ""
        }
    } catch {
        Write-Host "Error during full test: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "=== ACTION REQUIRED ===" -ForegroundColor Yellow
    Write-Host "Update QwenAiSummaryService.cs with this URL:" -ForegroundColor White
    Write-Host "private const string ApiUrl = ""$workingEndpoint"";" -ForegroundColor Cyan
    
} else {
    Write-Host "=== NO WORKING ENDPOINT FOUND ===" -ForegroundColor Red
    Write-Host ""
    Write-Host "Possible reasons:" -ForegroundColor Yellow
    Write-Host "1. HuggingFace Space is down or sleeping"
    Write-Host "2. API endpoint has changed"
    Write-Host "3. Network/firewall blocking requests"
    Write-Host "4. Service requires authentication"
    Write-Host ""
    Write-Host "Solutions:" -ForegroundColor Yellow
    Write-Host "1. Check https://huggingface.co/spaces/Qwen/Qwen2.5-7B-Instruct"
    Write-Host "2. Try again in a few minutes (Space may be waking up)"
    Write-Host "3. Use alternative AI service (see documentation)"
}

Write-Host ""
Write-Host "=== TEST COMPLETE ===" -ForegroundColor Cyan

