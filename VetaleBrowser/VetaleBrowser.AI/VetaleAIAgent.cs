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
        "\nUser:", "\nHuman:", "\n\nUser:", "\n\nHuman:",
        "\n\nAssistant:", "\nQuestion:", "User:", "Human:"
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
            var fullPrompt = $"{systemPrompt}\n\nUser: {prompt}\nAssistant:";
            
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Full prompt length: {fullPrompt.Length} chars");

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 4096,
                AntiPrompts = new List<string> { "\nUser:", "\n\nUser:", "\nHuman:", "\n\nHuman:", "User:" }
            };

            var responseBuilder = new StringBuilder();
            var hasResponse = false;
            var tokenCount = 0;

            System.Diagnostics.Trace.WriteLine("VetaleAIAgent: Starting token generation...");

            await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams, cancellationToken))
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

                // Check for end markers
                var currentResponse = responseBuilder.ToString();
                if (ShouldStopGeneration(currentResponse))
                {
                    System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: End marker detected at token {tokenCount}");
                    // Remove end marker from response
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
            var fullPrompt = $"{systemPrompt}\n\nUser: {prompt}\nAssistant:";

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 4096,
                AntiPrompts = new List<string> { "\nUser:", "\n\nUser:", "\nHuman:", "\n\nHuman:", "User:" }
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
                    // Trim end marker(s) from the builder so they never get streamed out
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
        // Check standard end markers
        if (EndMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Check if AI started generating dialogue (self-iteration)
        // Pattern: "User: ... Assistant: ..." or "Q: ... A: ..."
        if (Regex.IsMatch(text, @"(User|Human|Question|Q):\s*.+\s*(Assistant|AI|Answer|A):", RegexOptions.IgnoreCase))
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
        if (text.Length < 100) return false;

        // Check last 50 characters for repetition
        var checkLength = Math.Min(50, text.Length / 4);
        var endPart = text.Substring(text.Length - checkLength);
        var beforeEnd = text.Substring(0, text.Length - checkLength);

        // Count how many times the end pattern appears in the text
        int count = 0;
        int index = 0;
        while ((index = beforeEnd.IndexOf(endPart, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += checkLength;
            if (count >= 2) // If pattern repeats 2+ times, it's likely a loop
            {
                System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Detected repetitive pattern: '{endPart.Substring(0, Math.Min(20, endPart.Length))}...'");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if text has 3+ identical characters in a row (spam detection)
    /// </summary>
    private bool HasRepeatingCharacters(string text)
    {
        if (text.Length < 20) return false;

        // Check last 100 characters for repeating characters
        var checkText = text.Length > 100 ? text.Substring(text.Length - 100) : text;
        
        // Pattern: 3 or more identical characters (except spaces and newlines)
        var match = Regex.Match(checkText, @"([^\s\r\n])\1{2,}");
        if (match.Success)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIAgent: Detected repeating characters: '{match.Value}'");
            return true;
        }

        return false;
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
}
