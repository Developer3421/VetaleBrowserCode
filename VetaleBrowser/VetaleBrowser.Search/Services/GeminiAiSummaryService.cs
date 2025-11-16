using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Service for generating AI summaries using Google Gemini API
/// </summary>
public class GeminiAiSummaryService : IAiSummaryService, IDisposable
{
    private readonly HttpClient _httpClient;
    
    // Google Gemini API endpoint
    private const string ApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    
    // БЕЗКОШТОВНІ моделі Google Gemini (оберіть одну):
    // 1. "gemini-2.0-flash-exp" - найновіша експериментальна версія (РЕКОМЕНДОВАНО)
    // 2. "gemini-1.5-flash" - швидка та стабільна
    // 3. "gemini-1.5-pro" - найпотужніша, але повільніша
    // 4. "gemini-1.0-pro" - стара версія
    private const string ModelName = "gemini-2.0-flash";
    
    // ===== ЯК ОТРИМАТИ API КЛЮЧ =====
    // 1. Відкрийте: https://aistudio.google.com/app/apikey
    // 2. Натисніть "Get API key" або "Create API key"
    // 3. Виберіть або створіть новий проект Google Cloud
    // 4. Скопіюйте згенерований API ключ
    // 5. Вставте його нижче замість "YOUR_GEMINI_API_KEY_HERE"
    
    // БЕЗКОШТОВНІ ЛІМІТИ:
    // - 15 запитів на хвилину
    // - 1 мільйон токенів на місяць
    // - 1500 запитів на день
    
    // ВСТАВТЕ ВАШ GEMINI API КЛЮЧ ТУТ:
    private const string ApiKey = "AIzaSyBshGWGaj30VLU00caUUFgquscL5reQfhU";
    // Або встановіть змінну середовища: GEMINI_API_KEY
    
    private bool _isDisposed;
    private readonly string _apiKeyToUse;

    public GeminiAiSummaryService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        // Встановлюємо API ключ
        _apiKeyToUse = ApiKey;
        if (_apiKeyToUse == "YOUR_GEMINI_API_KEY_HERE")
        {
            // Спробуємо отримати з змінної середовища
            _apiKeyToUse = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? ApiKey;
        }
        
        if (_apiKeyToUse != "YOUR_GEMINI_API_KEY_HERE")
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] API Key configured: {_apiKeyToUse.Substring(0, Math.Min(10, _apiKeyToUse.Length))}...");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] WARNING: API Key not set!");
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Get your free key at: https://aistudio.google.com/app/apikey");
        }
        
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Service initialized with Google Gemini");
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Model: {ModelName}");
    }

    /// <summary>
    /// Generate AI summary for search query using Google Gemini
    /// </summary>
    public async Task<AiSearchSummary> GenerateSummaryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return await GenerateResponseAsync(query, isChat: false, cancellationToken);
    }

    /// <summary>
    /// Generate chat response using Google Gemini
    /// </summary>
    public async Task<AiSearchSummary> GenerateChatResponseAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        return await GenerateResponseAsync(userMessage, isChat: true, cancellationToken);
    }

    /// <summary>
    /// Internal method to generate response (for search summary or chat)
    /// </summary>
    private async Task<AiSearchSummary> GenerateResponseAsync(
        string query,
        bool isChat,
        CancellationToken cancellationToken = default)
    {
        var summary = new AiSearchSummary
        {
            Query = query,
            State = AiSummaryState.Loading
        };

        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                summary.State = AiSummaryState.NoSummary;
                summary.ErrorMessage = "Порожній запит";
                return summary;
            }

            // Перевірка наявності API ключа
            if (_apiKeyToUse == "YOUR_GEMINI_API_KEY_HERE" || string.IsNullOrWhiteSpace(_apiKeyToUse))
            {
                summary.State = AiSummaryState.Error;
                summary.ErrorMessage = "API ключ не налаштовано. Отримайте безкоштовний ключ на https://aistudio.google.com/app/apikey";
                System.Diagnostics.Debug.WriteLine("[GeminiAiSummary] API key not configured");
                return summary;
            }

            // Створюємо промпт для Gemini
            string prompt;
            
            if (isChat)
            {
                // Промпт для чату - природна розмова
                prompt = $@"Ти - Gemini, розумний AI-асистент від Google, вбудований у браузер Vetale Browser. Ти допомагаєш користувачам відповідаючи на їхні запитання.

Користувач запитав: ""{query}""

Твоє завдання:
1. Дати корисну та детальну відповідь українською мовою
2. Бути дружнім та професійним
3. Якщо питання потребує коду або прикладів - надай їх
4. Відповідай природно, як у бесіді

Твоя відповідь:";
            }
            else
            {
                // Промпт для підсумування пошуку
                prompt = $@"Ти - асистент пошукової системи Vetale Search. Користувач ввів пошуковий запит: ""{query}""

Твоє завдання:
1. Зрозуміти намір користувача та що саме він шукає
2. Написати короткий та зрозумілий підсумок (2-3 речення) українською мовою
3. Дати корисну пораду або контекст для пошуку

Відповідай ТІЛЬКИ українською мовою, коротко та по суті. Без зайвих пояснень, без повторення запиту.

Підсумок:";
            }

            // Gemini API використовує формат generateContent
            var requestData = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = isChat ? 1000 : 300, // Більше токенів для чату
                    candidateCount = 1
                },
                safetySettings = new[]
                {
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestData, new JsonSerializerOptions 
            { 
                WriteIndented = false 
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Формуємо URL з API ключем
            var apiUrl = $"{ApiBaseUrl}/{ModelName}:generateContent?key={_apiKeyToUse}";

            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Sending request for query: {query}");
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Model: {ModelName}");
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Request size: {jsonContent.Length} bytes");

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);

            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Response Status: {response.StatusCode} ({(int)response.StatusCode})");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] API error: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Error body: {errorBody}");
                
                summary.State = AiSummaryState.Error;
                
                // Детальний аналіз помилок
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    summary.ErrorMessage = "Невірний запит до API. Перевірте налаштування.";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || 
                         response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    summary.ErrorMessage = "Невірний API ключ. Перевірте ключ або отримайте новий на https://aistudio.google.com/app/apikey";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    summary.ErrorMessage = "Перевищено ліміт запитів. Зачекайте хвилину та спробуйте знову.";
                }
                else
                {
                    summary.ErrorMessage = $"Помилка API: {response.StatusCode} - {response.ReasonPhrase}";
                }
                
                return summary;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Response received: {responseJson.Length} bytes");

            // Парсинг відповіді Gemini
            using var doc = JsonDocument.Parse(responseJson);
            
            // Gemini повертає: {"candidates": [{"content": {"parts": [{"text": "..."}]}}]}
            if (doc.RootElement.TryGetProperty("candidates", out var candidatesElement) &&
                candidatesElement.GetArrayLength() > 0)
            {
                var firstCandidate = candidatesElement[0];
                
                // Перевірка на блокування контенту
                if (firstCandidate.TryGetProperty("finishReason", out var finishReasonElement))
                {
                    var finishReason = finishReasonElement.GetString();
                    if (finishReason == "SAFETY")
                    {
                        summary.State = AiSummaryState.NoSummary;
                        summary.ErrorMessage = "Запит заблоковано фільтрами безпеки Gemini";
                        System.Diagnostics.Debug.WriteLine("[GeminiAiSummary] Content blocked by safety filters");
                        return summary;
                    }
                }
                
                if (firstCandidate.TryGetProperty("content", out var contentElement) &&
                    contentElement.TryGetProperty("parts", out var partsElement) &&
                    partsElement.GetArrayLength() > 0)
                {
                    var firstPart = partsElement[0];
                    if (firstPart.TryGetProperty("text", out var textElement))
                    {
                        var generatedText = textElement.GetString() ?? string.Empty;
                        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Raw text length: {generatedText.Length}");
                        
                        // Очищення відповіді
                        generatedText = generatedText.Trim();
                        
                        // Видаляємо можливі префікси
                        var prefixes = new[] { "Підсумок:", "Відповідь:", "Результат:" };
                        foreach (var prefix in prefixes)
                        {
                            if (generatedText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                generatedText = generatedText.Substring(prefix.Length).Trim();
                            }
                        }

                        summary.SummaryText = generatedText;
                        summary.State = AiSummaryState.Ready;
                        summary.Confidence = 0.90; // Gemini зазвичай дає якісніші результати
                        summary.GeneratedAt = DateTime.UtcNow;

                        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ✓ Summary generated successfully!");
                        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Preview: {generatedText.Substring(0, Math.Min(100, generatedText.Length))}...");
                        
                        return summary;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ERROR: Invalid response structure");
                summary.State = AiSummaryState.NoSummary;
                summary.ErrorMessage = "Не вдалося отримати текст з відповіді Gemini";
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ERROR: No candidates in response");
                summary.State = AiSummaryState.NoSummary;
                summary.ErrorMessage = "Gemini не згенерував відповідь";
            }
        }
        catch (TaskCanceledException)
        {
            summary.State = AiSummaryState.Error;
            summary.ErrorMessage = "Час очікування вичерпано (30 сек)";
            System.Diagnostics.Debug.WriteLine("[GeminiAiSummary] Request timeout");
        }
        catch (HttpRequestException ex)
        {
            summary.State = AiSummaryState.Error;
            summary.ErrorMessage = $"Помилка мережі: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Network error: {ex}");
        }
        catch (JsonException ex)
        {
            summary.State = AiSummaryState.Error;
            summary.ErrorMessage = "Помилка парсингу відповіді API";
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] JSON parsing error: {ex}");
        }
        catch (Exception ex)
        {
            summary.State = AiSummaryState.Error;
            summary.ErrorMessage = $"Помилка: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Exception: {ex}");
        }

        return summary;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _httpClient.Dispose();
        _isDisposed = true;
        
        System.Diagnostics.Debug.WriteLine("[GeminiAiSummary] Service disposed");
    }
}

