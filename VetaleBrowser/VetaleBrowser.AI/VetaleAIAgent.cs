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
    
    // Response end markers - strict to prevent infinite generation
    private static readonly string[] EndMarkers = {
        "<|end|>", "<|im_end|>", "</s>", "[END]", "<end_of_turn>",
        "<|eot_id|>", "</assistant>", "</assistant_response>",
        "\nUser:", "\nHuman:", "\n\nUser:", "\n\nHuman:",
        "User:", "Human:",
        // Also stop if it tries to restart roles
        "\nAssistant:", "Assistant:", "\nAI:", "AI:",
        // Common instruction tags
        "### Instruction:", "### User:", "<|user|>", "<|assistant|>", "[INST]", "[/INST]"
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
    /// Generate a response with character filtering and auto-stop
    /// </summary>
    public async Task<string> GenerateResponseAsync(
        string prompt, 
        string? languageHint = null, 
        bool enableReasoning = false,
        CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Starting response generation for prompt: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");
        
        if (!_isInitialized)
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Not initialized, initializing now...");
            await InitializeAsync();
        }

        // Ensure only one generation or reset happens at a time
        await _generationLock.WaitAsync(cancellationToken);
        try
        {
            if (_executor == null)
            {
                throw new InvalidOperationException("Executor is not initialized");
            }

            // Build system prompt with language hint
            var systemPrompt = BuildSystemPrompt(languageHint, enableReasoning);
            // Switch to instruction/response format to reduce self-dialogue
            var fullPrompt = new StringBuilder();
            fullPrompt.AppendLine(systemPrompt);
            fullPrompt.AppendLine();
            fullPrompt.AppendLine("### Instruction:");
            fullPrompt.AppendLine(prompt);
            fullPrompt.AppendLine();
            fullPrompt.Append("### Response:\n");
            
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Full prompt length: {fullPrompt.Length} chars");

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 4096,
                AntiPrompts = new List<string>
                {
                    // Role markers (various cases)
                    "\nUser:", "\n\nUser:", "User:", "user:", "USER:",
                    "\nHuman:", "\n\nHuman:", "Human:", "human:",
                    "\nAssistant:", "Assistant:", "assistant:",
                    "\nAI:", "AI:", "ai:",
                    // Instruction tags
                    "### Instruction:", "### User:",
                    // Tokenizer-style roles
                    "<|user|>", "<|assistant|>",
                    // Llama chat tags
                    "[INST]", "[/INST]"
                }
            };

            var responseBuilder = new StringBuilder();
            var hasResponse = false;
            var tokenCount = 0;

            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Starting token generation...");

            await foreach (var token in _executor.InferAsync(fullPrompt.ToString(), inferenceParams, cancellationToken))
            {
                tokenCount++;
                
                // Filter out unwanted characters
                var filteredToken = FilterCharacters(token);
                
                if (string.IsNullOrEmpty(filteredToken))
                    continue;

                responseBuilder.Append(filteredToken);
                hasResponse = true;
                
                // Log first few tokens
                if (tokenCount <= 5 || tokenCount % 20 == 0)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Token {tokenCount}, response length: {responseBuilder.Length}");
                }

                // Check for end markers / self-iteration
                var currentResponse = responseBuilder.ToString();
                if (ShouldStopGeneration(currentResponse))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: End/self-iteration marker at token {tokenCount}");
                    responseBuilder = new StringBuilder(RemoveEndMarkers(currentResponse));
                    break;
                }

                // Check for repetitive text (infinite loop detection)
                if (currentResponse.Length > 100 && HasRepetitivePattern(currentResponse))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repetitive pattern detected at token {tokenCount}");
                    break;
                }

                // Check for 3+ identical characters in a row (spam detection)
                if (HasRepeatingCharacters(currentResponse))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repeating characters detected at token {tokenCount}");
                    break;
                }

                // Safety limit - allow long detailed responses
                if (responseBuilder.Length > 12000)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Safety limit reached at {responseBuilder.Length} chars");
                    break;
                }

                // Detect hard looping sequence early (used inline before appending optionally)
                if (DetectLoopingSequence(currentResponse))
                {
                    System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Instruction/Response loop detected");
                    break;
                }
            }

            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Generation complete. Total tokens: {tokenCount}, Response length: {responseBuilder.Length}");

            var finalResponse = responseBuilder.ToString().Trim();
            
            // Final cleanup
            finalResponse = CleanupResponse(finalResponse);

            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Final response length: {finalResponse.Length}");

            return hasResponse ? finalResponse : "I apologize, but I couldn't generate a proper response. Please try again.";
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
    /// Generate a response and stream tokens via progress callback while building the final text.
    /// </summary>
    public async Task<string> GenerateResponseStreamAsync(
        string prompt,
        string? languageHint = null,
        bool enableReasoning = false,
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
            {
                throw new InvalidOperationException("Executor is not initialized");
            }

            var systemPrompt = BuildSystemPrompt(languageHint, enableReasoning);
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
                MaxTokens = 4096,
                AntiPrompts = new List<string>
                {
                    "\nUser:", "\n\nUser:", "User:", "user:", "USER:",
                    "\nHuman:", "\n\nHuman:", "Human:", "human:",
                    "\nAssistant:", "Assistant:", "assistant:",
                    "\nAI:", "AI:", "ai:",
                    "### Instruction:", "### User:",
                    "<|user|>", "<|assistant|>",
                    "[INST]", "[/INST]"
                }
            };

            var responseBuilder = new StringBuilder();
            var tokenCount = 0;

            await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams, cancellationToken))
            {
                tokenCount++;

                // Filter unwanted characters first
                var filteredToken = FilterCharacters(token);
                if (string.IsNullOrEmpty(filteredToken))
                    continue;

                responseBuilder.Append(filteredToken);

                // Check for end markers using the current aggregate
                var current = responseBuilder.ToString();
                if (ShouldStopGeneration(current))
                {
                    var trimmed = RemoveEndMarkers(current);
                    responseBuilder.Clear();
                    responseBuilder.Append(trimmed);
                    break;
                }

                // Check for repetitive text (infinite loop detection)
                if (current.Length > 100 && HasRepetitivePattern(current))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repetitive pattern detected at token {tokenCount} (streaming)");
                    break;
                }

                // Check for 3+ identical characters in a row (spam detection)
                if (HasRepeatingCharacters(current))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repeating characters detected at token {tokenCount} (streaming)");
                    break;
                }

                // Safety length guard - allow long detailed responses
                if (responseBuilder.Length > 12000)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Safety limit reached at {responseBuilder.Length} chars (streaming)");
                    break;
                }

                // Report this token to UI after validating we didn't hit end markers
                try
                {
                    progress?.Report(filteredToken);
                }
                catch { /* swallow progress exceptions to not break generation */ }

                if (tokenCount <= 5 || tokenCount % 20 == 0)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: [stream] Token {tokenCount}, length {responseBuilder.Length}");
                }
            }

            var finalResponse = responseBuilder.ToString().Trim();
            finalResponse = CleanupResponse(finalResponse);
            return finalResponse.Length > 0
                ? finalResponse
                : "I apologize, but I couldn't generate a proper response. Please try again.";
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
    /// Filter out unwanted characters (Bengali, spam symbols, control chars)
    /// </summary>
    private string FilterCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove Bengali characters
        text = BengaliCharsRegex.Replace(text, "");
        
        // Remove Arabic characters (often spam)
        text = ArabicCharsRegex.Replace(text, "");
        
        // Remove Chinese characters (if not relevant)
        text = ChineseCharsRegex.Replace(text, "");
        
        // Remove Devanagari characters
        text = DevanagariCharsRegex.Replace(text, "");
        
        // Remove Thai characters
        text = ThaiCharsRegex.Replace(text, "");
        
        // Remove control characters (except newlines and tabs)
        text = Regex.Replace(text, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]", "");
        
        // Remove zero-width characters
        text = ZeroWidthCharsRegex.Replace(text, "");
        
        // Remove excessive special symbols (more than 3 in a row)
        text = Regex.Replace(text, @"([^\w\s\.,!?;:\-—–()\[\]{}\""'«»„""])\1{2,}", "$1$1");

        return text;
    }

    /// <summary>
    /// Check if generation should stop based on end markers
    /// </summary>
    private bool ShouldStopGeneration(string text)
    {
        // Check standard end markers / role restarts
        if (EndMarkers.Any(marker => text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        // Broad self-dialogue pattern, tolerant to markup and spacing
        if (Regex.IsMatch(text, @"(\*\*|\[)?\s*(User|Human|Question|Q)\s*(\*\*|\])?\s*:.*?(\*\*|\[)?\s*(Assistant|AI|Answer|A)\s*(\*\*|\])?\s*:", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Detected self-dialogue pattern - stopping");
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
    /// Check if text has repetitive pattern (indicates infinite loop)
    /// </summary>
    private bool HasRepetitivePattern(string text)
    {
        // Strict mode: minimal length threshold; analyze recent window
        if (string.IsNullOrEmpty(text) || text.Length < 30) return false;
        var window = text.Length > 800 ? text[^800..] : text;

        // 1. Immediate consecutive chunk repetition (2+ repeats of 5–80 chars)
        var consecutive = Regex.Match(window, @"(.{5,80})\1+");
        if (consecutive.Success)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: STRICT consecutive repetition '{consecutive.Groups[1].Value.Substring(0, Math.Min(30, consecutive.Groups[1].Value.Length))}...' ");
            return true;
        }

        // 2. Tail appears earlier once (last 40–120 chars); ANY previous occurrence triggers stop
        for (int size = 120; size >= 40; size -= 20)
        {
            if (text.Length <= size * 2) continue; // need earlier content
            var tail = text[^size..];
            var prior = text.Substring(0, text.Length - size);
            if (prior.Contains(tail, StringComparison.Ordinal))
            {
                System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: STRICT tail duplication size={size}");
                return true;
            }
        }

        // 3. High-frequency n-gram (3–8 chars) repeated ≥5 times (lower tolerance)
        for (int gram = 3; gram <= 8; gram++)
        {
            var counts = new Dictionary<string,int>();
            for (int i = 0; i <= window.Length - gram; i++)
            {
                var g = window.Substring(i, gram);
                if (!Regex.IsMatch(g, @"[A-Za-z0-9]")) continue; // ignore pure punctuation
                counts[g] = counts.TryGetValue(g, out var c) ? c + 1 : 1;
                if (counts[g] >= 5)
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: STRICT n-gram '{g}' count={counts[g]}");
                    return true;
                }
            }
        }

        // 4. Repeated structural markers (multiple Response headers)
        var respCount = Regex.Matches(text, @"### Response:").Count;
        if (respCount > 1)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: STRICT repeated response headers respCount={respCount}");
            return true;
        }

        return false;
    }

    // Detect hard looping sequence early (used inline before appending optionally)
    private bool DetectLoopingSequence(string aggregate)
    {
        // If model starts injecting new Instruction/Response blocks repeatedly
        int instCount = Regex.Matches(aggregate, @"### Instruction:").Count;
        int respCount = Regex.Matches(aggregate, @"### Response:").Count;
        if (instCount > 1 || respCount > 1)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Repeated instruction/response blocks (inst={instCount}, resp={respCount})");
            return true;
        }
        return false;
    }

    private string RemoveTrailingRepeatedChunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 200) return text.Trim();
        // Try to remove duplicated tail: find largest tail (40-200 chars) that appears earlier immediately before
        for (int size = Math.Min(200, text.Length / 2); size >= 40; size -= 10)
        {
            var tail = text.Substring(text.Length - size);
            var preceding = text.Substring(0, text.Length - size);
            if (preceding.EndsWith(tail))
            {
                System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Trimming duplicated tail size={size}");
                return preceding.TrimEnd();
            }
        }
        return text.Trim();
    }

    /// <summary>
    /// Final cleanup of the response
    /// </summary>
    private string CleanupResponse(string text)
    {
        // Remove trailing incomplete sentences
        text = text.TrimEnd();
        
        // DON'T modify newlines - preserve formatting for poems and lists
        // Only remove excessive blank lines (4+ empty lines in a row)
        text = Regex.Replace(text, @"(\r?\n\s*){4,}", "\n\n\n");
        
        // Remove trailing whitespace from each line while preserving empty lines
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }
        text = string.Join('\n', lines);
        
        // Remove leading/trailing whitespace from entire text
        text = text.Trim();

        // Remove any trailing repeated chunk that might cause looping
        text = RemoveTrailingRepeatedChunk(text);

        return text;
    }

    /// <summary>
    /// Build system prompt with language and reasoning settings
    /// </summary>
    private string BuildSystemPrompt(string? languageHint, bool enableReasoning)
    {
        var systemPrompt = new StringBuilder();
        systemPrompt.AppendLine("You are Vetale AI, a helpful assistant for Vetale Browser.");
        systemPrompt.AppendLine("Provide detailed, comprehensive answers with examples and explanations.");
        systemPrompt.AppendLine("IMPORTANT: Stop generating immediately after completing your answer. Do not continue with follow-up questions or additional dialogue.");
        systemPrompt.AppendLine("Absolutely never include role prefixes like 'User:', 'Assistant:', 'Human:', 'AI:', 'Q:', or 'A:' in your output.");
        systemPrompt.AppendLine("Write a direct response only.");
        
        if (!string.IsNullOrEmpty(languageHint))
        {
            systemPrompt.AppendLine($"Respond in {languageHint}.");
        }
        
        if (enableReasoning)
        {
            systemPrompt.AppendLine("Show your reasoning when answering.");
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

    /// <summary>
    /// Check if text has 3+ identical characters in a row (spam detection)
    /// </summary>
    private bool HasRepeatingCharacters(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        // Limit to recent tail to keep it fast
        var check = text.Length > 200 ? text.Substring(text.Length - 200) : text;
        // 3+ identical non-whitespace characters
        if (Regex.IsMatch(check, @"([^\s\r\n])\1{2,}"))
            return true;
        // 4+ identical punctuation/symbols
        if (Regex.IsMatch(check, @"([^\w\s])\1{3,}"))
            return true;
        return false;
    }
}
