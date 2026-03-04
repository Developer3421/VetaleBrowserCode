using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Service for generating AI summaries using Google Gemini API
/// </summary>
public class GeminiAiSummaryService : IAiSummaryService, IDisposable
{
    private readonly HttpClient _httpClient;
    
    // Current language for response generation

    
    // Google Gemini API endpoint
    private const string ApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    
    // Free Google Gemini models (choose one):
    // 1. "gemini-2.5-flash" - latest fast model (RECOMMENDED)
    // 2. "gemini-1.5-flash" - previous fast version
    // 3. "gemini-1.5-pro" - most powerful, but slower
    private const string ModelName = "gemini-2.5-flash";
    
    // ===== HOW TO GET AN API KEY =====
    // 1. Open: https://aistudio.google.com/app/apikey
    // 2. Click "Get API key" or "Create API key"
    // 3. Select or create a new Google Cloud project
    // 4. Copy the generated API key
    // 5. Paste it below instead of "YOUR_GEMINI_API_KEY_HERE"
    
    // FREE LIMITS:
    // - 15 requests per minute
    // - 1 million tokens per month
    // - 1500 requests per day
    
    // INSERT YOUR GEMINI API KEY HERE:
    private const string DefaultApiKey = ""; // ← API key removed for security. Users must enter their own key.
    // Or set the environment variable: GEMINI_API_KEY
    
    private bool _isDisposed;
    private string _apiKeyToUse = DefaultApiKey;
    
    /// <summary>
    /// Static custom API key set by the user
    /// </summary>
    private static string? _customApiKey;
    
    /// <summary>
    /// Whether a custom API key is being used
    /// </summary>
    public static bool IsUsingCustomApiKey => !string.IsNullOrWhiteSpace(_customApiKey);
    
    /// <summary>
    /// Get the current API key (for verification)
    /// </summary>
    public string CurrentApiKey => _apiKeyToUse;

    public GeminiAiSummaryService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        InitializeApiKey();
        
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Service initialized with Google Gemini");
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Model: {ModelName}");
    }
    
    /// <summary>
    /// Constructor with custom API key
    /// </summary>
    public GeminiAiSummaryService(string? customApiKey)
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        if (!string.IsNullOrWhiteSpace(customApiKey))
        {
            _customApiKey = customApiKey;
        }
        
        InitializeApiKey();
        
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Service initialized with Google Gemini (custom key provided)");
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Model: {ModelName}");
    }
    
    private void InitializeApiKey()
    {
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] === InitializeApiKey START ===");
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] _customApiKey (static): {(_customApiKey != null ? $"'{_customApiKey.Substring(0, Math.Min(10, _customApiKey.Length))}...'" : "null")}");
        
        // Priority: 1) memory (_customApiKey) > 2) default key > 3) database > 4) environment variable
        
        // 1. Custom key in memory
        if (!string.IsNullOrWhiteSpace(_customApiKey))
        {
            _apiKeyToUse = _customApiKey;
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ✓ Using custom API Key (memory): {_apiKeyToUse.Substring(0, Math.Min(10, _apiKeyToUse.Length))}...");
            return;
        }
        
        // 2. Default key (if set in code)
        if (!string.IsNullOrWhiteSpace(DefaultApiKey) && DefaultApiKey != "YOUR_GEMINI_API_KEY_HERE")
        {
            _apiKeyToUse = DefaultApiKey;
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ✓ Using default API Key: {_apiKeyToUse.Substring(0, Math.Min(10, _apiKeyToUse.Length))}...");
            return;
        }
        
        // 3. Environment variable (fast, without blocking)
        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] GEMINI_API_KEY env var: {(envKey != null ? $"'{envKey.Substring(0, Math.Min(10, envKey.Length))}...'" : "not set")}");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            _apiKeyToUse = envKey;
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ✓ Using environment API Key: {_apiKeyToUse.Substring(0, Math.Min(10, _apiKeyToUse.Length))}...");
            return;
        }
        
        // 4. Try to load from database (may be slow)
        try
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Trying to load from database...");
            var dbKey = LoadApiKeyFromDatabase();
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Database returned: {(dbKey != null ? $"'{dbKey.Substring(0, Math.Min(10, dbKey.Length))}...'" : "null")}");
            if (!string.IsNullOrWhiteSpace(dbKey))
            {
                _customApiKey = dbKey; // Cache it
                _apiKeyToUse = dbKey;
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ✓ Using saved API Key (database): {_apiKeyToUse.Substring(0, Math.Min(10, _apiKeyToUse.Length))}...");
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Error loading from database: {ex.Message}");
        }
        
        // If nothing is found - use placeholder
        _apiKeyToUse = "YOUR_GEMINI_API_KEY_HERE";
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ⚠️ WARNING: API Key not set! Using placeholder.");
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Get your free key at: https://aistudio.google.com/app/apikey");
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] === InitializeApiKey END ===");
    }
    
    /// <summary>
    /// Load API key from database
    /// </summary>
    private static string? LoadApiKeyFromDatabase()
    {
        try
        {
            var apiKeysService = DatabaseServicesFactory.TryGetApiKeysService();
            if (apiKeysService != null)
            {
                // Use Task.Run to avoid deadlock in UI thread
                var key = Task.Run(async () => await apiKeysService.GetGeminiApiKeyAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Loaded API key from database");
                    return key;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Error loading API key from database: {ex.Message}");
        }
        return null;
    }
    
    /// <summary>
    /// Save API key to database
    /// </summary>
    private static void SaveApiKeyToDatabase(string? apiKey)
    {
        try
        {
            var apiKeysService = DatabaseServicesFactory.TryGetApiKeysService();
            if (apiKeysService != null)
            {
                // Use Task.Run to avoid deadlock in UI thread
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Task.Run(async () => await apiKeysService.RemoveApiKeyAsync(ApiServiceIds.Gemini).ConfigureAwait(false)).GetAwaiter().GetResult();
                    System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Removed API key from database");
                }
                else
                {
                    Task.Run(async () => await apiKeysService.SetGeminiApiKeyAsync(apiKey).ConfigureAwait(false)).GetAwaiter().GetResult();
                    System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Saved API key to database");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Error saving API key to database: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Set custom API key for all service instances and save to database
    /// </summary>
    public static void SetCustomApiKey(string? apiKey)
    {
        _customApiKey = apiKey;
        SaveApiKeyToDatabase(apiKey); // Save to DB for persistent storage
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Custom API key set: {(string.IsNullOrWhiteSpace(apiKey) ? "cleared" : apiKey.Substring(0, Math.Min(10, apiKey.Length)) + "...")}");
    }
    
    /// <summary>
    /// Clear custom API key (use default) and remove from database
    /// </summary>
    public static void ClearCustomApiKey()
    {
        _customApiKey = null;
        SaveApiKeyToDatabase(null); // Remove from DB
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Custom API key cleared, using default");
    }
    
    /// <summary>
    /// Reinitialize API key (call after SetCustomApiKey)
    /// </summary>
    public void RefreshApiKey()
    {
        InitializeApiKey();
    }

    /// <summary>
    /// Set language for AI responses
    /// </summary>

    /// <summary>
    /// Get language name for prompt
    /// </summary>
    private string GetLanguageName(string code) => code switch
    {
        "uk" => "Ukrainian (Українська)",
        "en" => "English",
        "de" => "German (Deutsch)",
        "ru" => "Russian (Русский)",
        "tr" => "Turkish (Türkçe)",
        _ => "English"
    };

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

        // Get the current interface language at the start
        var currentLanguage = GetCurrentInterfaceLanguage();
        var languageName = GetLanguageName(currentLanguage);
        
        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Current interface language: {currentLanguage}, Language name: {languageName}");

        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                summary.State = AiSummaryState.NoSummary;
                summary.ErrorMessage = LocalizeMessage("EmptyQuery", currentLanguage);
                return summary;
            }

            // Check for API key presence
            if (_apiKeyToUse == "YOUR_GEMINI_API_KEY_HERE" || string.IsNullOrWhiteSpace(_apiKeyToUse))
            {
                summary.State = AiSummaryState.Error;
                summary.ErrorMessage = "API_KEY_REQUIRED"; // Special code for UI - need to show key input window
                System.Diagnostics.Debug.WriteLine("[GeminiAiSummary] API key not configured");
                return summary;
            }
            
            // Create prompt for Gemini based on current language
            string prompt;
            
            if (isChat)
            {
                // Chat prompt - natural conversation
                prompt = GetChatPrompt(query, currentLanguage, languageName);
            }
            else
            {
                // Search summary prompt
                prompt = GetSearchSummaryPrompt(query, currentLanguage, languageName);
            }

            // Gemini API uses generateContent format
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
                    maxOutputTokens = 8192, // Maximum tokens without limits
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

            // Compose URL with API key
            var apiUrl = $"{ApiBaseUrl}/{ModelName}:generateContent?key={_apiKeyToUse}";

            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Sending request for query: {query}");
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Model: {ModelName}");
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Request size: {jsonContent.Length} bytes");

            // Retry logic for rate limits (429) - wait without showing error
            HttpResponseMessage response;
            int maxRetries = 10; // More retries
            int retryCount = 0;
            int delayMs = 2000; // Start with 2 seconds
            
            while (true)
            {
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
                
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Response Status: {response.StatusCode} ({(int)response.StatusCode}), Attempt: {retryCount + 1}");
                
                // If success - exit
                if (response.IsSuccessStatusCode)
                {
                    break;
                }
                
                // If rate limited (429) - wait and retry (without showing error)
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        // After many retries - just return empty response without error
                        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Max retries reached, returning empty response");
                        summary.State = AiSummaryState.NoSummary;
                        summary.SummaryText = "";
                        return summary;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Rate limited, waiting {delayMs}ms before retry...");
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, 30000); // Exponential backoff up to 30 seconds
                    continue;
                }
                
                // Other errors - exit the loop
                break;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] API error: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Error body: {errorBody}");
                
                summary.State = AiSummaryState.Error;
                
                // Detailed error analysis with localization
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 - model not found
                    summary.ErrorMessage = $"Model '{ModelName}' not found. Check model name.";
                    System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Model not found: {ModelName}");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    summary.ErrorMessage = LocalizeMessage("BadRequest", currentLanguage);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || 
                         response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    summary.ErrorMessage = LocalizeMessage("InvalidApiKey", currentLanguage);
                }
                else
                {
                    // For all other errors - generic message without details
                    summary.ErrorMessage = LocalizeMessage("ApiError", currentLanguage);
                }
                
                return summary;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Response received: {responseJson.Length} bytes");

            // Parse Gemini response
            using var doc = JsonDocument.Parse(responseJson);
            
            // Gemini returns: {"candidates": [{"content": {"parts": [{"text": "..."}]}}]}
            if (doc.RootElement.TryGetProperty("candidates", out var candidatesElement) &&
                candidatesElement.GetArrayLength() > 0)
            {
                var firstCandidate = candidatesElement[0];
                
                // Check for content blocking
                if (firstCandidate.TryGetProperty("finishReason", out var finishReasonElement))
                {
                    var finishReason = finishReasonElement.GetString();
                    if (finishReason == "SAFETY")
                    {
                        summary.State = AiSummaryState.NoSummary;
                        summary.ErrorMessage = LocalizeMessage("SafetyBlocked", currentLanguage);
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
                        
                        // Clean up response
                        generatedText = generatedText.Trim();
                        
                        // Remove possible prefixes
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
                        summary.Confidence = 0.90; // Gemini usually provides higher quality results
                        summary.GeneratedAt = DateTime.UtcNow;

                        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ✓ Summary generated successfully!");
                        System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Preview: {generatedText.Substring(0, Math.Min(100, generatedText.Length))}...");
                        
                        return summary;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ERROR: Invalid response structure");
                summary.State = AiSummaryState.NoSummary;
                summary.ErrorMessage = LocalizeMessage("InvalidResponse", currentLanguage);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] ERROR: No candidates in response");
                summary.State = AiSummaryState.NoSummary;
                summary.ErrorMessage = LocalizeMessage("NoCandidates", currentLanguage);
            }
        }
        catch (TaskCanceledException)
        {
            summary.State = AiSummaryState.Error;
            summary.ErrorMessage = LocalizeMessage("Timeout", currentLanguage);
            System.Diagnostics.Debug.WriteLine("[GeminiAiSummary] Request timeout");
        }
        catch (HttpRequestException ex)
        {
            summary.State = AiSummaryState.Error;
            summary.ErrorMessage = LocalizeMessage("NetworkError", currentLanguage) + $": {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Network error: {ex}");
        }
        catch (JsonException ex)
        {
            summary.State = AiSummaryState.Error;
            summary.ErrorMessage = LocalizeMessage("JsonParseError", currentLanguage);
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] JSON parsing error: {ex}");
        }
        catch (Exception ex)
        {
            summary.State = AiSummaryState.Error;
            summary.ErrorMessage = LocalizeMessage("GenericError", currentLanguage) + $": {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Exception: {ex}");
        }

        return summary;
    }

    /// <summary>
    /// Get current interface language from LocalizationService
    /// </summary>
    private static string GetCurrentInterfaceLanguage()
    {
        try
        {
            // Get language directly from LocalizationService (UI)
            var langCode = VetaleBrowser.UI.Services.LocalizationService.CurrentLanguageCode;
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Got langCode from LocalizationService: '{langCode}'");
            
            if (!string.IsNullOrWhiteSpace(langCode))
            {
                return langCode;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Error getting interface language from LocalizationService: {ex.Message}");
        }
        
        // Fallback: try to get language from settings via database
        try
        {
            var settingsService = DatabaseServicesFactory.TryGetSettingsService();
            if (settingsService != null)
            {
                var langCode = Task.Run(async () => await settingsService.GetLanguageAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
                System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Got langCode from DB settings: '{langCode}'");
                
                if (!string.IsNullOrWhiteSpace(langCode))
                {
                    return langCode;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiAiSummary] Error getting interface language from DB: {ex.Message}");
        }
        
        System.Diagnostics.Debug.WriteLine("[GeminiAiSummary] Returning default language: en");
        // Default - English (standard)
        return "en";
    }

    /// <summary>
    /// Generate chat prompt based on language
    /// </summary>
    private static string GetChatPrompt(string query, string langCode, string languageName)
    {
        return langCode switch
        {
            "uk" => $@"Ти - Gemini, розумний AI-асистент від Google, вбудований у браузер Vetale Browser. Ти допомагаєш користувачам відповідаючи на їхні запитання.

Користувач запитав: ""{query}""

Твоє завдання:
1. Дати корисну та детальну відповідь українською мовою
2. Бути дружнім та професійним
3. Якщо питання потребує коду або прикладів - надай їх
4. Відповідай природно, як у бесіді

Твоя відповідь:",

            "en" => $@"You are Gemini, an intelligent AI assistant from Google, built into the Vetale Browser. You help users by answering their questions.

User asked: ""{query}""

Your task:
1. Give a useful and detailed answer in English
2. Be friendly and professional
3. If the question requires code or examples - provide them
4. Respond naturally, as in a conversation

Your response:",

            "de" => $@"Du bist Gemini, ein intelligenter KI-Assistent von Google, eingebaut in den Vetale Browser. Du hilfst Benutzern, indem du ihre Fragen beantwortest.

Benutzer fragte: ""{query}""

Deine Aufgabe:
1. Gib eine nützliche und detaillierte Antwort auf Deutsch
2. Sei freundlich und professionell
3. Wenn die Frage Code oder Beispiele erfordert - stelle sie bereit
4. Antworte natürlich, wie in einem Gespräch

Deine Antwort:",

            "ru" => $@"Ты - Gemini, умный AI-ассистент от Google, встроенный в браузер Vetale Browser. Ты помогаешь пользователям, отвечая на их вопросы.

Пользователь спросил: ""{query}""

Твоя задача:
1. Дать полезный и подробный ответ на русском языке
2. Быть дружелюбным и профессиональным
3. Если вопрос требует кода или примеров - предоставь их
4. Отвечай естественно, как в беседе

Твой ответ:",

            "tr" => $@"Sen Gemini'sin, Vetale Browser'a entegre edilmiş Google'dan akıllı bir yapay zeka asistanı. Kullanıcılara sorularını yanıtlayarak yardım ediyorsun.

Kullanıcı sordu: ""{query}""

Görevin:
1. Türkçe olarak faydalı ve ayrıntılı bir cevap ver
2. Arkadaş canlısı ve profesyonel ol
3. Soru kod veya örnekler gerektiriyorsa - sağla
4. Doğal bir şekilde, bir sohbetteki gibi yanıt ver

Cevabın:",

            _ => $@"You are Gemini, an intelligent AI assistant from Google, built into the Vetale Browser. You help users by answering their questions.

User asked: ""{query}""

Your task:
1. Give a useful and detailed answer in {languageName}
2. Be friendly and professional
3. If the question requires code or examples - provide them
4. Respond naturally, as in a conversation

Your response:"
        };
    }

    /// <summary>
    /// Generate search summary prompt based on language
    /// </summary>
    private static string GetSearchSummaryPrompt(string query, string langCode, string languageName)
    {
        return langCode switch
        {
            "uk" => $@"Ти - асистент пошукової системи Vetale Search. Користувач ввів пошуковий запит: ""{query}""

Твоє завдання:
1. Зрозуміти намір користувача та що саме він шукає
2. Написати короткий та зрозумілий підсумок (2-3 речення) українською мовою
3. Дати корисну пораду або контекст для пошуку

Відповідай ТІЛЬКИ українською мовою, коротко та по суті. Без зайвих пояснень, без повторення запиту.

Підсумок:",

            "en" => $@"You are an assistant for the Vetale Search engine. User entered search query: ""{query}""

Your task:
1. Understand the user's intent and what they are looking for
2. Write a short and clear summary (2-3 sentences) in English
3. Give useful advice or context for the search

Respond ONLY in English, briefly and to the point. No unnecessary explanations, no repeating the query.

Summary:",

            "de" => $@"Du bist ein Assistent für die Vetale-Suchmaschine. Benutzer gab Suchanfrage ein: ""{query}""

Deine Aufgabe:
1. Verstehe die Absicht des Benutzers und wonach er sucht
2. Schreibe eine kurze und klare Zusammenfassung (2-3 Sätze) auf Deutsch
3. Gib nützliche Ratschläge oder Kontext für die Suche

Antworte NUR auf Deutsch, kurz und prägnant. Keine unnötigen Erklärungen, keine Wiederholung der Anfrage.

Zusammenfassung:",

            "ru" => $@"Ты - ассистент поисковой системы Vetale Search. Пользователь ввёл поисковый запрос: ""{query}""

Твоя задача:
1. Понять намерение пользователя и что именно он ищет
2. Написать короткое и понятное резюме (2-3 предложения) на русском языке
3. Дать полезный совет или контекст для поиска

Отвечай ТОЛЬКО на русском языке, кратко и по существу. Без лишних объяснений, без повторения запроса.

Резюме:",

            "tr" => $@"Sen Vetale Search arama motoru için bir asistansın. Kullanıcı arama sorgusu girdi: ""{query}""

Görevin:
1. Kullanıcının amacını ve ne aradığını anla
2. Türkçe olarak kısa ve net bir özet yaz (2-3 cümle)
3. Arama için faydalı tavsiye veya bağlam ver

SADECE Türkçe olarak yanıt ver, kısa ve öz. Gereksiz açıklama yok, sorguyu tekrarlama yok.

Özet:",

            _ => $@"You are an assistant for the Vetale Search engine. User entered search query: ""{query}""

Your task:
1. Understand the user's intent and what they are looking for
2. Write a short and clear summary (2-3 sentences) in {languageName}
3. Give useful advice or context for the search

Respond ONLY in {languageName}, briefly and to the point. No unnecessary explanations, no repeating the query.

Summary:"
        };
    }

    /// <summary>
    /// Localization of error messages
    /// </summary>
    private static string LocalizeMessage(string key, string langCode)
    {
        return (langCode, key) switch
        {
            // Empty query
            ("uk", "EmptyQuery") => "Порожній запит",
            ("en", "EmptyQuery") => "Empty query",
            ("de", "EmptyQuery") => "Leere Anfrage",
            ("ru", "EmptyQuery") => "Пустой запрос",
            ("tr", "EmptyQuery") => "Boş sorgu",


            // Bad request
            ("uk", "BadRequest") => "Невірний запит до API. Перевірте налаштування.",
            ("en", "BadRequest") => "Invalid API request. Check your settings.",
            ("de", "BadRequest") => "Ungültige API-Anfrage. Überprüfen Sie Ihre Einstellungen.",
            ("ru", "BadRequest") => "Неверный запрос к API. Проверьте настройки.",
            ("tr", "BadRequest") => "Geçersiz API isteği. Ayarlarınızı kontrol edin.",

            // Invalid API key
            ("uk", "InvalidApiKey") => "Невірний API ключ. Перевірте ключ або отримайте новий на https://aistudio.google.com/app/apikey",
            ("en", "InvalidApiKey") => "Invalid API key. Check your key or get a new one at https://aistudio.google.com/app/apikey",
            ("de", "InvalidApiKey") => "Ungültiger API-Schlüssel. Überprüfen Sie Ihren Schlüssel oder erhalten Sie einen neuen unter https://aistudio.google.com/app/apikey",
            ("ru", "InvalidApiKey") => "Неверный API ключ. Проверьте ключ или получите новый на https://aistudio.google.com/app/apikey",
            ("tr", "InvalidApiKey") => "Geçersiz API anahtarı. Anahtarınızı kontrol edin veya https://aistudio.google.com/app/apikey adresinden yeni bir tane alın",

            // API error
            ("uk", "ApiError") => "Помилка API",
            ("en", "ApiError") => "API error",
            ("de", "ApiError") => "API-Fehler",
            ("ru", "ApiError") => "Ошибка API",
            ("tr", "ApiError") => "API hatası",

            // Safety blocked
            ("uk", "SafetyBlocked") => "Запит заблоковано фільтрами безпеки Gemini",
            ("en", "SafetyBlocked") => "Request blocked by Gemini safety filters",
            ("de", "SafetyBlocked") => "Anfrage durch Gemini-Sicherheitsfilter blockiert",
            ("ru", "SafetyBlocked") => "Запрос заблокирован фильтрами безопасности Gemini",
            ("tr", "SafetyBlocked") => "İstek Gemini güvenlik filtreleri tarafından engellendi",

            // Invalid response
            ("uk", "InvalidResponse") => "Не вдалося отримати текст з відповіді Gemini",
            ("en", "InvalidResponse") => "Failed to get text from Gemini response",
            ("de", "InvalidResponse") => "Fehler beim Abrufen des Textes aus der Gemini-Antwort",
            ("ru", "InvalidResponse") => "Не удалось получить текст из ответа Gemini",
            ("tr", "InvalidResponse") => "Gemini yanıtından metin alınamadı",

            // No candidates
            ("uk", "NoCandidates") => "Gemini не згенерував відповідь",
            ("en", "NoCandidates") => "Gemini did not generate a response",
            ("de", "NoCandidates") => "Gemini hat keine Antwort generiert",
            ("ru", "NoCandidates") => "Gemini не сгенерировал ответ",
            ("tr", "NoCandidates") => "Gemini yanıt oluşturmadı",

            // Timeout
            ("uk", "Timeout") => "Час очікування вичерпано (30 сек)",
            ("en", "Timeout") => "Request timeout (30 sec)",
            ("de", "Timeout") => "Zeitüberschreitung der Anfrage (30 Sek.)",
            ("ru", "Timeout") => "Время ожидания истекло (30 сек)",
            ("tr", "Timeout") => "İstek zaman aşımı (30 sn)",

            // Network error
            ("uk", "NetworkError") => "Помилка мережі",
            ("en", "NetworkError") => "Network error",
            ("de", "NetworkError") => "Netzwerkfehler",
            ("ru", "NetworkError") => "Ошибка сети",
            ("tr", "NetworkError") => "Ağ hatası",

            // JSON parse error
            ("uk", "JsonParseError") => "Помилка парсингу відповіді API",
            ("en", "JsonParseError") => "API response parsing error",
            ("de", "JsonParseError") => "Fehler beim Parsen der API-Antwort",
            ("ru", "JsonParseError") => "Ошибка парсинга ответа API",
            ("tr", "JsonParseError") => "API yanıtı ayrıştırma hatası",

            // Generic error
            ("uk", "GenericError") => "Помилка",
            ("en", "GenericError") => "Error",
            ("de", "GenericError") => "Fehler",
            ("ru", "GenericError") => "Ошибка",
            ("tr", "GenericError") => "Hata",

            // Default fallback
            _ => key
        };
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
