# Скрипт для завантаження користувацької моделі Whisper (ОПЦІОНАЛЬНО)
# VetaleBrowser вже має вбудовану базову модель!
# Використовуйте цей скрипт тільки якщо хочете іншу модель (tiny/small/medium)

Write-Host ""
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   Завантаження користувацької моделі Whisper" -ForegroundColor Yellow
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "⚠️  УВАГА: VetaleBrowser вже має вбудовану базову модель!" -ForegroundColor Yellow
Write-Host "   Цей скрипт потрібен ТІЛЬКИ якщо ви хочете:" -ForegroundColor Gray
Write-Host "   - Швидшу модель (tiny)" -ForegroundColor Gray
Write-Host "   - Точнішу модель (small/medium)" -ForegroundColor Gray
Write-Host ""

$response = Read-Host "Продовжити завантаження? (y/n)"
if ($response -ne 'y') {
    Write-Host "Скасовано. Використовуйте вбудовану модель!" -ForegroundColor Green
    exit
}

Write-Host ""
Write-Host "Оберіть модель:" -ForegroundColor Cyan
Write-Host "1. Tiny (75 MB)   - швидка, менш точна" -ForegroundColor White
Write-Host "2. Base (142 MB)  - [ВБУДОВАНА] баланс швидкості/якості" -ForegroundColor Green
Write-Host "3. Small (466 MB) - точніша, повільніша" -ForegroundColor White
Write-Host "4. Medium (1.5 GB) - найточніша, дуже повільна" -ForegroundColor White
Write-Host ""

$choice = Read-Host "Ваш вибір (1-4)"

$models = @{
    "1" = @{ Name = "ggml-tiny.bin"; Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin"; Size = "75 MB" }
    "2" = @{ Name = "ggml-base.bin"; Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin"; Size = "142 MB" }
    "3" = @{ Name = "ggml-small.bin"; Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin"; Size = "466 MB" }
    "4" = @{ Name = "ggml-medium.bin"; Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin"; Size = "1.5 GB" }
}

if (-not $models.ContainsKey($choice)) {
    Write-Host "Невірний вибір!" -ForegroundColor Red
    exit 1
}

$model = $models[$choice]
$ModelUrl = $model.Url
$ModelFile = $model.Name
$ModelSize = $model.Size

# Зберігаємо в AppData
$OutputDir = Join-Path $env:APPDATA "VetaleBrowser\Models"
$OutputPath = Join-Path $OutputDir $ModelFile

Write-Host ""
Write-Host "Завантаження: $ModelFile ($ModelSize)" -ForegroundColor Green
Write-Host ""

# Створюємо директорію
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "✓ Створено директорію: $OutputDir" -ForegroundColor Cyan
}

# Перевіряємо, чи модель вже існує
if (Test-Path $OutputPath) {
    Write-Host "Модель вже існує: $OutputPath" -ForegroundColor Yellow
    $response = Read-Host "Перезавантажити? (y/n)"
    if ($response -ne 'y') {
        Write-Host "Завантаження скасовано." -ForegroundColor Yellow
        exit
    }
}

# Завантажуємо модель
Write-Host "Завантаження з: $ModelUrl" -ForegroundColor Cyan
Write-Host ""

try {
    $webClient = New-Object System.Net.WebClient
    
    # Додаємо обробник прогресу
    Register-ObjectEvent -InputObject $webClient -EventName DownloadProgressChanged -SourceIdentifier WebClient.DownloadProgressChanged -Action {
        $percent = $eventArgs.ProgressPercentage
        $received = $eventArgs.BytesReceived / 1MB
        $total = $eventArgs.TotalBytesToReceive / 1MB
        Write-Progress -Activity "Завантаження моделі Whisper" -Status "$percent% завершено" -PercentComplete $percent -CurrentOperation "$([math]::Round($received, 2)) MB з $([math]::Round($total, 2)) MB"
    } | Out-Null
    
    $webClient.DownloadFileAsync($ModelUrl, $OutputPath)
    
    while ($webClient.IsBusy) {
        Start-Sleep -Milliseconds 100
    }
    
    Unregister-Event -SourceIdentifier WebClient.DownloadProgressChanged
    $webClient.Dispose()
    
    Write-Progress -Activity "Завантаження моделі Whisper" -Completed
    Write-Host ""
    Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "   ✓ Модель успішно завантажена!" -ForegroundColor Green
    Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""
    Write-Host "Розташування: $OutputPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "VetaleBrowser автоматично використає цю модель!" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "✗ Помилка завантаження: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Спробуйте завантажити вручну:" -ForegroundColor Yellow
    Write-Host "1. Відкрийте: $ModelUrl" -ForegroundColor Cyan
    Write-Host "2. Збережіть як: $OutputPath" -ForegroundColor Cyan
    exit 1
}

Write-Host ""
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "   Інформація про модель" -ForegroundColor Magenta
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "Назва: $ModelFile" -ForegroundColor White
Write-Host "Розмір: $ModelSize" -ForegroundColor White
Write-Host "Підтримка мов: 99+ (українська, англійська, та ін.)" -ForegroundColor White
Write-Host "Локація: $OutputPath" -ForegroundColor White
Write-Host ""
Write-Host "Щоб повернутися до вбудованої моделі - просто видаліть цей файл." -ForegroundColor Gray
Write-Host ""

