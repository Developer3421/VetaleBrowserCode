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
/// AI Agent for Vetale Browser using Microsoft Agents Framework and LlamaSharp
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

            var systemPrompt = BuildSystemPrompt(languageHint);
            var fullPrompt = new StringBuilder();
            fullPrompt.AppendLine(systemPrompt);
            fullPrompt.AppendLine();
            fullPrompt.AppendLine("### Instruction:");
            fullPrompt.AppendLine(prompt);
            fullPrompt.AppendLine();
            fullPrompt.Append("### Response:\n");

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 8192,
                AntiPrompts = new List<string>
                {
                    "\n\nUser:", "\n\nHuman:",
                    "### Instruction:", "### User:"
                }
            };

            var responseBuilder = new StringBuilder();
            var tokenCount = 0;
            var hasResponse = false;

            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Starting token generation...");

            await foreach (var token in _executor.InferAsync(fullPrompt.ToString(), inferenceParams, cancellationToken))
            {
                tokenCount++;

                var filteredToken = FilterCharacters(token);
                if (string.IsNullOrEmpty(filteredToken))
                    continue;

                responseBuilder.Append(filteredToken);
                hasResponse = true;

                if (tokenCount <= 5 || tokenCount % 20 == 0)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Token {tokenCount}, response length: {responseBuilder.Length}");
                }

                var currentResponse = responseBuilder.ToString();

                // Пом'якшуємо умови зупинки, щоб не обрізати план/відповідь занадто рано
                if (ShouldStopGeneration(currentResponse))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: End/self-iteration marker at token {tokenCount}");
                    responseBuilder = new StringBuilder(RemoveEndMarkers(currentResponse));
                    break;
                }

                // Перевірка патернів тільки для довгих шматків
                if (currentResponse.Length > 500 && HasRepetitivePattern(currentResponse))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repetitive pattern detected at token {tokenCount}");
                    break;
                }

                // Більш толерантний детектор повторюваних символів
                if (HasRepeatingCharacters(currentResponse))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repeating characters detected at token {tokenCount}");
                    break;
                }

                // Збільшений safety‑ліміт довжини
                if (responseBuilder.Length > 24000)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Safety limit reached at {responseBuilder.Length} chars");
                    break;
                }

                if (DetectLoopingSequence(currentResponse))
                {
                    System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Instruction/Response loop detected");
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

            var systemPrompt = BuildSystemPrompt(languageHint);
            var sb = new StringBuilder();
            sb.AppendLine(systemPrompt);
            sb.AppendLine();
            sb.AppendLine("### Instruction:");
            sb.AppendLine(prompt);
            sb.AppendLine();
            sb.Append("### Response:\n");
            var fullPrompt = sb.ToString();

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 8192,
                AntiPrompts = new List<string>
                {
                    "\n\nUser:", "\n\nHuman:",
                    "### Instruction:", "### User:"
                }
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
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repetitive pattern detected at token {tokenCount} (streaming)");
                    break;
                }

                if (HasRepeatingCharacters(current))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repeating characters detected at token {tokenCount} (streaming)");
                    break;
                }

                if (responseBuilder.Length > 24000)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Safety limit reached at {responseBuilder.Length} chars (streaming)");
                    break;
                }

                try
                {
                    progress?.Report(filtered);
                }
                catch { }

                if (tokenCount <= 5 || tokenCount % 20 == 0)
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

        // Ослаблюємо детектор самодіалогу: лише очевидні великі патерни з обома ролями
        if (text.Length > 400 && Regex.IsMatch(text,
                "(User|Human)\\s*:?[\\s\\S]{10,80}(Assistant|AI)\\s*:",
                RegexOptions.IgnoreCase))
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Detected strong self-dialogue pattern - stopping");
            return true;
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
        int instCount = Regex.Matches(aggregate, "### Instruction:").Count;
        int respCount = Regex.Matches(aggregate, "### Response:").Count;
        if (instCount > 1 || respCount > 1)
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

        // Прибираємо зайві пробіли в кінці
        text = text.TrimEnd();

        // Залишаємо форматування, але зрізаємо 4+ пустих рядків підряд до максимум 3
        text = Regex.Replace(text, "(\\r?\\n\\s*){4,}", "\n\n\n");

        // Прибираємо пробіли в кінці кожного рядка
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }
        text = string.Join('\n', lines);

        // Прибираємо дубльований хвіст, якщо такий є
        text = RemoveTrailingRepeatedChunk(text);

        return text.Trim();
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
    /// Build simple system prompt without explicit planning
    /// </summary>
    private string BuildSystemPrompt(string? languageHint)
    {
        var systemPrompt = new StringBuilder();
        systemPrompt.AppendLine("You are Vetale AI, a helpful assistant for Vetale Browser.");
        systemPrompt.AppendLine("Provide clear, concise and helpful answers to the user's questions.");
        systemPrompt.AppendLine("Do not simulate dialogues or use role labels like 'User:' or 'Assistant:'.");
        systemPrompt.AppendLine("Avoid repeating the same text and stop when the answer is complete.");

        // Persona: Vetale based on Indian Vetala
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("Persona: You are Vetale — a witty, curious, and benevolent spirit inspired by the Indian Vetala (वेताल). You blend ancient wisdom with modern practicality. You may add a light touch of mystique or a short clever hint when appropriate, but you always keep answers concrete and useful.");
        systemPrompt.AppendLine("Identity: Refer to yourself as 'Vetale' when needed. Do not claim to be any other character or service.");
        systemPrompt.AppendLine("Tone: Warm, calm, and respectful; a subtle folklore vibe is okay. Avoid horror or graphic depictions unless explicitly requested and safe to provide (keep it PG‑13).");
        systemPrompt.AppendLine("Safety: Protect the user. Refuse harmful, explicit, or unsafe instructions. Prefer practical, safe alternatives.");
        systemPrompt.AppendLine("Lore handling: If asked about your origin, briefly (1–2 sentences) explain that Vetale is a modern reinterpretation of the Vetala from Indian folklore.");

        if (!string.IsNullOrEmpty(languageHint))
        {
            systemPrompt.AppendLine($"Respond in {languageHint}.");
        }
        else
        {
            systemPrompt.AppendLine("Respond in the same language as the user's request.");
        }

        return systemPrompt.ToString();
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
