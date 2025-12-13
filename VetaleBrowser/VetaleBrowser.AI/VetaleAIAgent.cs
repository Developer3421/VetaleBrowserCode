using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;

namespace VetaleBrowser.VetaleBrowser.AI;

/// <summary>
/// AI Agent for Vetale Browser implementing Microsoft Agents AI Framework with LlamaSharp backend.
/// Based on Vetala (वेताल) - a wise spirit from Indian mythology.
/// </summary>
public class VetaleAIAgent : IDisposable 
{
    private LLamaWeights? _model;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;
    private readonly string _modelPath;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    // New: serialize generation/reset to avoid disposing context while generating
    private readonly SemaphoreSlim _generationLock = new(1, 1);
    
    // Character filtering patterns
    private static readonly Regex BengaliCharsRegex = new(@"[\u0980-\u09FF]+", RegexOptions.Compiled);
    private static readonly Regex ArabicCharsRegex = new(@"[\u0600-\u06FF]+", RegexOptions.Compiled);
    private static readonly Regex ChineseCharsRegex = new(@"[\u4E00-\u9FFF]+", RegexOptions.Compiled);
    private static readonly Regex DevanagariCharsRegex = new(@"[\u0900-\u097F]+", RegexOptions.Compiled);
    private static readonly Regex ThaiCharsRegex = new(@"[\u0E00-\u0E7F]+", RegexOptions.Compiled);
    private static readonly Regex ZeroWidthCharsRegex = new(@"[\u200B-\u200D\uFEFF]", RegexOptions.Compiled);
    
    // Response end markers - мінімальний набір для запобігання критичних проблем
    private static readonly string[] EndMarkers = {
        "<|end|>", "<|im_end|>", "</s>", "[END]", "<end_of_turn>",
        "<|eot_id|>",
        // М'які маркери для початку нового діалогу — тільки з подвійним переносом
        "\n\nUser:", "\n\nHuman:",
        // Common instruction tags в кінці — якщо модель раптом починає новий блок
        "### Instruction:", "### User:"
    };

    public VetaleAIAgent(string modelPath)
    {
        _modelPath = modelPath;
    }

    /// <summary>
    /// Initialize the LlamaSharp model
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            await Task.Run(() =>
            {
                var parameters = new ModelParams(_modelPath)
                {
                    ContextSize = 4096,
                    GpuLayerCount = 0, // CPU only
                    UseMemorymap = true,
                    UseMemoryLock = false
                };

                _model = LLamaWeights.LoadFromFile(parameters);
                _context = _model.CreateContext(parameters);
                _executor = new InteractiveExecutor(_context);

                _isInitialized = true;
                System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Model initialized successfully");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Failed to initialize model: {ex}");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Generate a response with character filtering and auto-stop (simple one-pass, no explicit planning)
    /// </summary>
    public async Task<string> GenerateResponseAsync(
        string prompt,
        string? languageHint = null,
        CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Starting response generation for prompt: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");
        
        if (!_isInitialized)
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Not initialized, initializing now...");
            await InitializeAsync();
        }

        await _generationLock.WaitAsync(cancellationToken);
        try
        {
            if (_executor == null)
                throw new InvalidOperationException("Executor is not initialized");

            // Build prompt using VetalePersona with Gemma-3 format
            var fullPrompt = BuildGemmaPrompt(prompt, languageHint);

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 4096,
                AntiPrompts = VetalePersona.GetAntiPrompts()
            };

            var responseBuilder = new StringBuilder();
            var tokenCount = 0;
            var hasResponse = false;

            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Starting token generation...");

            await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams, cancellationToken))
            {
                tokenCount++;

                var filteredToken = FilterCharacters(token);
                if (string.IsNullOrEmpty(filteredToken))
                    continue;

                responseBuilder.Append(filteredToken);
                hasResponse = true;

                var currentResponse = responseBuilder.ToString();

                // Пом'якшуємо умови зупинки, щоб не обрізати план/відповідь занадто рано
                if (ShouldStopGeneration(currentResponse))
                {
                    responseBuilder = new StringBuilder(RemoveEndMarkers(currentResponse));
                    break;
                }

                // Перевірка патернів тільки для довгих шматків
                if (currentResponse.Length > 500 && HasRepetitivePattern(currentResponse))
                {
                    break;
                }

                // Більш толерантний детектор повторюваних символів
                if (HasRepeatingCharacters(currentResponse))
                {
                    break;
                }

                // Збільшений safety‑ліміт довжини
                if (responseBuilder.Length > 24000)
                {
                    break;
                }

                if (DetectLoopingSequence(currentResponse))
                {
                    break;
                }
            }

            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Generation complete. Total tokens: {tokenCount}, Response length: {responseBuilder.Length}");

            var finalResponse = responseBuilder.ToString().Trim();
            finalResponse = CleanupResponse(finalResponse);

            return hasResponse
                ? finalResponse
                : "Я не зміг згенерувати коректну відповідь. Спробуйте переформулювати запит.";
        }
        finally
        {
            if (_generationLock.CurrentCount == 0)
            {
                _generationLock.Release();
            }
        }
    }

    /// <summary>
    /// Generate a response and stream tokens via progress callback (simple one-pass streaming)
    /// </summary>
    public async Task<string> GenerateResponseStreamAsync(
        string prompt,
        string? languageHint = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Starting STREAMING generation for prompt: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");

        if (!_isInitialized)
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Not initialized, initializing now (streaming)...");
            await InitializeAsync();
        }

        await _generationLock.WaitAsync(cancellationToken);
        try
        {
            if (_executor == null)
                throw new InvalidOperationException("Executor is not initialized");

            // Build prompt using VetalePersona with Gemma-3 format
            var fullPrompt = BuildGemmaPrompt(prompt, languageHint);

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 4096,
                AntiPrompts = VetalePersona.GetAntiPrompts()
            };

            var responseBuilder = new StringBuilder();
            var tokenCount = 0;

            await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams, cancellationToken))
            {
                tokenCount++;

                var filtered = FilterCharacters(token);
                if (string.IsNullOrEmpty(filtered))
                    continue;

                responseBuilder.Append(filtered);

                var current = responseBuilder.ToString();

                if (ShouldStopGeneration(current))
                {
                    var trimmed = RemoveEndMarkers(current);
                    responseBuilder.Clear();
                    responseBuilder.Append(trimmed);
                    try { progress?.Report(trimmed); } catch { }
                    break;
                }

                if (current.Length > 500 && HasRepetitivePattern(current))
                {
                    break;
                }

                if (HasRepeatingCharacters(current))
                {
                    break;
                }

                if (responseBuilder.Length > 24000)
                {
                    break;
                }

                // Report token immediately without any delay
                try
                {
                    progress?.Report(filtered);
                }
                catch { }

                // Minimal logging - only every 100 tokens to reduce overhead
                if (tokenCount % 100 == 0)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: [stream] Token {tokenCount}, length {responseBuilder.Length}");
                }
            }

            var finalResponse = CleanupResponse(responseBuilder.ToString().Trim());
            return finalResponse.Length > 0
                ? finalResponse
                : "Я не зміг згенерувати коректну відповідь. Спробуйте ще раз.";
        }
        finally
        {
            if (_generationLock.CurrentCount == 0)
            {
                _generationLock.Release();
            }
        }
    }

    /// <summary>
    /// Filter out unwanted characters (minimal filtering - only critical issues)
    /// </summary>
    private string FilterCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Лише критичне: контрольні та zero‑width символи
        text = Regex.Replace(text, "[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]", string.Empty);
        text = ZeroWidthCharsRegex.Replace(text, string.Empty);

        return text;
    }

    /// <summary>
    /// Check if generation should stop based on end markers
    /// </summary>
    private bool ShouldStopGeneration(string text)
    {
        // Спочатку перевіряємо жорсткі end‑маркери
        if (EndMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            return true;

        // === ДЕТЕКЦІЯ САМОІТЕРАЦІЇ ===
        
        // Перевірка на нумеровані питання/запити (Запит 2, Question 2, etc.)
        if (Regex.IsMatch(text, @"(Запит|Питання|Question|Запрос|Вопрос|Frage|Pregunta|Soru|Demande)\s*[2-9]", RegexOptions.IgnoreCase))
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Detected numbered question pattern - stopping");
            return true;
        }
        
        // Перевірка на ### маркери з номерами
        if (Regex.IsMatch(text, @"###\s*(Запит|Питання|Question|Запрос|Instruction|Anfrage|Demande|Solicitud|İstek)\s*[2-9]?:", RegexOptions.IgnoreCase))
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Detected repeated instruction block - stopping");
            return true;
        }
        
        // Детектор самодіалогу
        if (text.Length > 300 && Regex.IsMatch(text,
                "(User|Human|Користувач|Пользователь|Benutzer|Utilisateur|Usuario|Kullanıcı)\\s*:?[\\s\\S]{10,80}(Assistant|AI|Vetale|Відповідь|Ответ)\\s*:",
                RegexOptions.IgnoreCase))
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Detected self-dialogue pattern - stopping");
            return true;
        }
        
        // Перевірка на повторення запитів про програмування тощо
        if (Regex.IsMatch(text, @"\n\n\*\*?(Як|What|How|Що|Напиши|Write|Create|Створи|Explain|Поясни)", RegexOptions.IgnoreCase))
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Detected new question in response - stopping");
            return true;
        }
        
        // Детекція витоку промпту / внутрішніх інструкцій
        if (Regex.IsMatch(text, @"(Пожалуйста|Please|Будь ласка),?\s*(ответь|відповідь|answer|respond|предоставь)", RegexOptions.IgnoreCase))
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Detected prompt leak - stopping");
            return true;
        }
        
        // Детекція повторення контенту (той самий текст двічі)
        if (text.Length > 200)
        {
            var halfLen = text.Length / 2;
            var firstHalf = text.Substring(0, Math.Min(150, halfLen));
            if (text.Substring(halfLen).Contains(firstHalf.Substring(0, Math.Min(50, firstHalf.Length))))
            {
                System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Detected content repetition - stopping");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Remove end markers from response
    /// </summary>
    private string RemoveEndMarkers(string text)
    {
        foreach (var marker in EndMarkers)
        {
            var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                text = text.Substring(0, index);
            }
        }
        return text;
    }

    /// <summary>
    /// Detect hard looping sequence early (Instruction/Response blocks)
    /// </summary>
    private bool DetectLoopingSequence(string aggregate)
    {
        // Якщо модель починає друкувати додаткові Instruction/Response блоки, зупиняємося
        
        // English
        int instCount = Regex.Matches(aggregate, "### (Instruction|Question|User):").Count;
        int respCount = Regex.Matches(aggregate, "### (Response|Answer|Assistant):").Count;
        
        // Ukrainian
        instCount += Regex.Matches(aggregate, "### (Запит|Питання):").Count;
        respCount += Regex.Matches(aggregate, "### Відповідь:").Count;
        
        // Russian  
        instCount += Regex.Matches(aggregate, "### (Запрос|Вопрос):").Count;
        respCount += Regex.Matches(aggregate, "### Ответ:").Count;
        
        // German
        instCount += Regex.Matches(aggregate, "### (Anfrage|Frage):").Count;
        respCount += Regex.Matches(aggregate, "### Antwort:").Count;
        
        // French
        instCount += Regex.Matches(aggregate, "### (Demande|Question):").Count;
        respCount += Regex.Matches(aggregate, "### Réponse:").Count;
        
        // Spanish
        instCount += Regex.Matches(aggregate, "### (Solicitud|Pregunta):").Count;
        respCount += Regex.Matches(aggregate, "### Respuesta:").Count;
        
        // Turkish
        instCount += Regex.Matches(aggregate, "### (İstek|Soru):").Count;
        respCount += Regex.Matches(aggregate, "### Yanıt:").Count;
        
        if (instCount > 0 || respCount > 1)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repeated instruction/response blocks (inst={instCount}, resp={respCount})");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Допоміжний метод: прибрати дубльований хвіст, якщо модель повторює кінець відповіді
    /// </summary>
    private string RemoveTrailingRepeatedChunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 200)
            return text.Trim();

        for (int size = Math.Min(200, text.Length / 2); size >= 40; size -= 10)
        {
            var tail = text.Substring(text.Length - size);
            var preceding = text.Substring(0, text.Length - size);
            if (preceding.EndsWith(tail, StringComparison.Ordinal))
            {
                System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Trimming duplicated tail size={size}");
                return preceding.TrimEnd();
            }
        }

        return text.Trim();
    }

    /// <summary>
    /// Final cleanup of the response (прибирає зайві пробіли та дубльований хвіст, але не чіпає структуру плану)
    /// </summary>
    private string CleanupResponse(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // === ОБРІЗАННЯ САМОІТЕРАЦІЇ ===
        // Видаляємо все після нового запиту/питання
        text = TruncateAtSelfIteration(text);

        // Прибираємо зайві пробіли в кінці
        text = text.TrimEnd();

        // === ВИПРАВЛЕННЯ ПРОБІЛІВ ===
        // Виправляємо склеєні слова (латиниця та кирилиця)
        text = FixWordSpacing(text);

        // Залишаємо форматування, але зрізаємо 4+ пустих рядків підряд до максимум 2
        text = Regex.Replace(text, "(\\r?\\n\\s*){4,}", "\n\n");

        // Прибираємо пробіли в кінці кожного рядка
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }
        text = string.Join('\n', lines);

        // Прибираємо дубльований хвіст, якщо такий є
        text = RemoveTrailingRepeatedChunk(text);

        // Видаляємо випадкові символи та сміття на початку/кінці
        text = Regex.Replace(text, @"^[\s\*\#\-\:]+", ""); // Зайві символи на початку
        text = Regex.Replace(text, @"[\s\*\#\-\:]+$", ""); // Зайві символи в кінці

        return text.Trim();
    }

    /// <summary>
    /// Обрізає відповідь при виявленні самоітерації (нового питання/запиту)
    /// </summary>
    private string TruncateAtSelfIteration(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Патерни самоітерації - обрізаємо все після них
        var iterationPatterns = new[]
        {
            // === ВИТІК ПРОМПТУ / ВНУТРІШНІ ІНСТРУКЦІЇ ===
            @"---+\s*\*{0,2}(Пожалуйста|Please|Будь ласка)",
            @"\*{0,2}(Пожалуйста|Please|Будь ласка),?\s*(ответь|відповідь|answer|respond|предоставь)",
            @"предоставь\s+ответ\s+на\s+запрос",
            @"ответь\s+на\s+(этот\s+)?запрос",
            
            // === ПОВТОРЕННЯ ЗАПИТУ ===
            @"\*{2}(Склади|Напиши|Создай|Write|Create)",
            
            // Нумеровані запити
            @"\n\n?\*{0,2}(Запит|Питання|Question|Запрос|Вопрос|Frage|Pregunta|Soru|Demande)\s*[2-9]\s*[:\*]",
            // ### маркери
            @"\n\n?###\s*(Запит|Питання|Question|Instruction|Запрос|Anfrage|Demande|Solicitud|İstek)\s*[2-9]?\s*:",
            // User/Human маркери
            @"\n\n?(User|Human|Користувач|Пользователь|Benutzer|Utilisateur|Usuario|Kullanıcı)\s*:",
            // Нові питання про програмування
            @"\n\n?\*{0,2}(Як|What|How|Що|Напиши|Write|Create|Створи)\s+(можна|to|do|is|написати|створити|зробити)",
            // Розділювачі
            @"\n---+\s*\n",
            @"\n\*{3,}\s*\n"
        };

        int minIndex = text.Length;
        
        foreach (var pattern in iterationPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Index > 30 && match.Index < minIndex)
            {
                minIndex = match.Index;
                System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Truncating at pattern at index {match.Index}");
            }
        }

        if (minIndex < text.Length)
        {
            return text.Substring(0, minIndex).TrimEnd();
        }

        return text;
    }

    /// <summary>
    /// Виправляє проблеми з відступами між словами
    /// </summary>
    private string FixWordSpacing(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Додаємо пробіл після крапки/коми/знаку питання, якщо його немає (і наступна - велика літера або літера)
        text = Regex.Replace(text, @"([.!?,:;])([A-ZА-ЯЄІЇҐa-zа-яєіїґ])", "$1 $2");
        
        // Виправляємо випадок коли мала літера прилипає до великої (camelCase -> окремі слова в тексті)
        // Тільки якщо це не скорочення і в контексті речення
        text = Regex.Replace(text, @"([a-zа-яєіїґ])([A-ZА-ЯЄІЇҐ][a-zа-яєіїґ])", "$1 $2");
        
        // Видаляємо подвійні та більше пробілів
        text = Regex.Replace(text, @"  +", " ");
        
        // Пробіл після дужок якщо далі слово
        text = Regex.Replace(text, @"\)([A-Za-zА-Яа-яЄІЇҐєіїґ])", ") $1");
        text = Regex.Replace(text, @"([A-Za-zА-Яа-яЄІЇҐєіїґ])\(", "$1 (");

        return text;
    }

    /// <summary>
    /// Check if text has repetitive pattern (indicates infinite loop) - більш толерантна версія
    /// </summary>
    private bool HasRepetitivePattern(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 100) return false;
        var window = text.Length > 1200 ? text[^1200..] : text;

        // Жорсткий тригер тільки при 3+ повторів великих шматків
        var consecutive = Regex.Match(window, "(.{10,100})\\1{2,}");
        if (consecutive.Success)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Consecutive repetition '{consecutive.Groups[1].Value[..Math.Min(30, consecutive.Groups[1].Value.Length)]}...' ");
            return true;
        }

        // Відключено tail duplication

        // N‑gram тільки при дуже частих повтореннях
        for (int gram = 3; gram <= 8; gram++)
        {
            var counts = new Dictionary<string, int>();
            for (int i = 0; i <= window.Length - gram; i++)
            {
                var g = window.Substring(i, gram);
                if (!Regex.IsMatch(g, "[A-Za-z0-9]")) continue;
                counts[g] = counts.TryGetValue(g, out var c) ? c + 1 : 1;
                if (counts[g] >= 8)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: N-gram '{g}' count={counts[g]}");
                    return true;
                }
            }
        }

        var respCount = Regex.Matches(text, "### Response:").Count;
        if (respCount > 1)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repeated response headers respCount={respCount}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if text has 5+ identical characters in a row (spam detection) - більш толерантно
    /// </summary>
    private bool HasRepeatingCharacters(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var check = text.Length > 200 ? text[^200..] : text;
        // 5+ однакових не-пробільних символів
        if (Regex.IsMatch(check, "([^\\s\\r\\n])\\1{4,}"))
            return true;
        // 6+ однакових знаків пунктуації/символів
        if (Regex.IsMatch(check, "([^\\w\\s])\\1{5,}"))
            return true;
        return false;
    }

    /// <summary>
    /// Build comprehensive anti-prompts list to prevent self-iteration
    /// </summary>
    private List<string> BuildAntiPrompts(string instructionLabel, string responseLabel)
    {
        return new List<string>
        {
            // === END MARKERS ===
            "<|end|>", "<|im_end|>", "</s>", "[END]", "<end_of_turn>", "<|eot_id|>",
            
            // === PROMPT LEAK / INTERNAL INSTRUCTIONS ===
            "Пожалуйста, ответь", "Пожалуйста, предоставь",
            "Please answer", "Please respond", "Please provide",
            "Будь ласка, відповідь", "Будь ласка, надай",
            "ответь на этот запрос", "ответь на запрос",
            "предоставь ответ на запрос",
            "---**Пожалуйста", "---*Пожалуйста", "---Пожалуйста",
            "**Склади", "**Напиши", "**Создай", "**Write", "**Create",
            
            // === ENGLISH DIALOGUE MARKERS ===
            "\n\nUser:", "\n\nHuman:", "\n\nAssistant:", "\n\nAI:",
            "\nUser:", "\nHuman:", "\nAssistant:", "\nAI:",
            "User:", "Human:",
            "### Instruction:", "### User:", "### Question:",
            
            // === LOCALIZED INSTRUCTION/RESPONSE ===
            $"### {instructionLabel}:",
            $"\n\n### {instructionLabel}:",
            $"\n### {instructionLabel}:",
            
            // === UKRAINIAN ===
            "### Запит:", "### Питання:", "### Користувач:",
            "\n\nЗапит:", "\nЗапит:", "\n\nПитання:", "\nПитання:",
            "Запит:", "Питання:", "Користувач:",
            
            // === RUSSIAN ===
            "### Запрос:", "### Вопрос:", "### Пользователь:",
            "\n\nЗапрос:", "\nЗапрос:", "\n\nВопрос:", "\nВопрос:",
            "Запрос:", "Вопрос:", "Пользователь:",
            
            // === GERMAN ===
            "### Anfrage:", "### Frage:", "### Benutzer:",
            "\n\nAnfrage:", "\nAnfrage:", "\n\nFrage:", "\nFrage:",
            "Anfrage:", "Frage:", "Benutzer:",
            
            // === FRENCH ===
            "### Demande:", "### Question:", "### Utilisateur:",
            "\n\nDemande:", "\nDemande:", "\n\nQuestion:", "\nQuestion:",
            "Demande:", "Question:", "Utilisateur:",
            
            // === SPANISH ===
            "### Solicitud:", "### Pregunta:", "### Usuario:",
            "\n\nSolicitud:", "\nSolicitud:", "\n\nPregunta:", "\nPregunta:",
            "Solicitud:", "Pregunta:", "Usuario:",
            
            // === TURKISH ===
            "### İstek:", "### Soru:", "### Kullanıcı:",
            "\n\nİstek:", "\nİstek:", "\n\nSoru:", "\nSoru:",
            "İstek:", "Soru:", "Kullanıcı:",
            
            // === NUMBERED QUESTIONS ===
            "\n\n1.", "\n\n2.", "\n\n#1", "\n\n#2",
            "**Запит 2", "**Питання 2", "**Question 2", "**Запрос 2",
            "Запит 2:", "Питання 2:", "Question 2:", "Запрос 2:",
            
            // === SELF-DIALOGUE PATTERNS ===
            "\n\n**User", "\n\n**Human", "\n\n**Запит", "\n\n**Питання",
            "\n\n---\n", "\n\n***\n", "\n---\n", "\n***\n"
        };
    }

    /// <summary>
    /// Build Gemma-3 compatible prompt using VetalePersona
    /// </summary>
    private string BuildGemmaPrompt(string userMessage, string? languageHint)
    {
        var sb = new StringBuilder();
        
        // System prompt with Vetale persona
        var systemPrompt = VetalePersona.BuildSystemPrompt(languageHint);
        
        // Gemma-3 chat format
        sb.AppendLine("<start_of_turn>user");
        sb.AppendLine(systemPrompt);
        sb.AppendLine();
        sb.AppendLine(userMessage);
        sb.AppendLine("<end_of_turn>");
        sb.Append("<start_of_turn>model\n");
        
        return sb.ToString();
    }

    /// <summary>
    /// Get instruction/response labels in the target language (legacy, kept for compatibility)
    /// </summary>
    private (string Instruction, string Response) GetPromptLabels(string? languageHint)
    {
        return languageHint switch
        {
            "Ukrainian" => ("Запит", "Відповідь"),
            "Russian" => ("Запрос", "Ответ"),
            "German" => ("Anfrage", "Antwort"),
            "French" => ("Demande", "Réponse"),
            "Spanish" => ("Solicitud", "Respuesta"),
            "Turkish" => ("İstek", "Yanıt"),
            _ => ("Instruction", "Response")
        };
    }

    /// <summary>
    /// Build system prompt (legacy, now uses VetalePersona)
    /// </summary>
    private string BuildSystemPrompt(string? languageHint)
    {
        return VetalePersona.BuildSystemPrompt(languageHint);
    }

    /// <summary>
    /// Reset the conversation context
    /// </summary>
    public async Task ResetContextAsync()
    {
        // If not initialized yet, nothing to reset
        if (!_isInitialized || _model == null)
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: ResetContextAsync skipped (not initialized)");
            return;
        }

        // Serialize with any ongoing generation
        await _generationLock.WaitAsync();
        try
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Resetting context...");

            if (_context != null)
            {
                // Dispose old executor and context
                _executor = null;
                _context.Dispose();
            }
            
            // Recreate context and executor
            var parameters = new ModelParams(_modelPath)
            {
                ContextSize = 4096,
                GpuLayerCount = 0,
                UseMemorymap = true,
                UseMemoryLock = false
            };
            
            await Task.Run(() =>
            {
                _context = _model!.CreateContext(parameters);
                _executor = new InteractiveExecutor(_context);
            });
            
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Context reset successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Error resetting context: {ex}");
            throw;
        }
        finally
        {
            if (_generationLock.CurrentCount == 0)
            {
                _generationLock.Release();
            }
        }
    }

    public void Dispose()
    {
        _executor = null;
        _context?.Dispose();
        _model?.Dispose();
        _initLock.Dispose();
        _generationLock.Dispose();
        
        System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Disposed");
    }
}
