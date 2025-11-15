using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.AI;

/// <summary>
/// Service for managing Vetale AI Agent lifecycle
/// </summary>
public class VetaleAIService : IDisposable
{
    private VetaleAIAgent? _agent;
    private readonly string _modelPath;
    private bool _isDisposed;
    private static readonly SemaphoreSlim _instanceLock = new(1, 1);

    public VetaleAIService()
    {
        // Try multiple locations to find the model
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        
        var possiblePaths = new[]
        {
            // In bin output (copied during build) - most common in Debug
            Path.Combine(baseDir, "VetaleBrowser.AI", "Model", "gemma-3-1b-it-UD-Q2_K_XL.gguf"),
            // In project directory (navigate up from bin/Debug/net9.0/)
            Path.Combine(baseDir, "..", "..", "..", "VetaleBrowser.AI", "Model", "gemma-3-1b-it-UD-Q2_K_XL.gguf"),
            // Simplified in bin
            Path.Combine(baseDir, "Model", "gemma-3-1b-it-UD-Q2_K_XL.gguf")
        };
        
        _modelPath = string.Empty;
        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                _modelPath = fullPath;
                System.Diagnostics.Trace.WriteLine($"VetaleAIService: Found model at: {_modelPath}");
                break;
            }
        }
        
        if (string.IsNullOrEmpty(_modelPath))
        {
            // Use first path as default
            _modelPath = Path.GetFullPath(possiblePaths[0]);
            System.Diagnostics.Trace.WriteLine($"VetaleAIService: Model not found, will use: {_modelPath}");
            System.Diagnostics.Trace.WriteLine($"VetaleAIService: Searched in:");
            foreach (var path in possiblePaths)
            {
                System.Diagnostics.Trace.WriteLine($"  - {Path.GetFullPath(path)}");
            }
        }
    }

    /// <summary>
    /// Initialize the AI agent
    /// </summary>
    public async Task InitializeAsync()
    {
        await _instanceLock.WaitAsync();
        try
        {
            if (_agent != null)
                return;

            if (!File.Exists(_modelPath))
            {
                throw new FileNotFoundException($"AI model not found at: {_modelPath}");
            }

            _agent = new VetaleAIAgent(_modelPath);
            await _agent.InitializeAsync();
            
            System.Diagnostics.Trace.WriteLine("VetaleAIService: Initialized successfully");
        }
        finally
        {
            _instanceLock.Release();
        }
    }

    /// <summary>
    /// Generate AI response
    /// </summary>
    public async Task<string> GenerateResponseAsync(
        string userMessage,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Trace.WriteLine($"VetaleAIService: GenerateResponseAsync called with message: {userMessage.Substring(0, Math.Min(50, userMessage.Length))}...");
        
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(VetaleAIService));

        if (_agent == null)
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIService: Agent not initialized, initializing...");
            await InitializeAsync();
        }

        if (_agent == null)
        {
            throw new InvalidOperationException("AI agent failed to initialize");
        }

        try
        {
            var languageHint = GetLanguageHint(language);
            System.Diagnostics.Trace.WriteLine($"VetaleAIService: Language: {languageHint ?? "Auto"}");
            
            var response = await _agent.GenerateResponseAsync(userMessage, languageHint, cancellationToken);
            
            System.Diagnostics.Trace.WriteLine($"VetaleAIService: Response generated successfully, length: {response.Length}");
            return response;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIService: Error generating response: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Generate AI response with streaming token updates.
    /// </summary>
    public async Task<string> GenerateResponseStreamAsync(
        string userMessage,
        string? language = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Trace.WriteLine($"VetaleAIService: GenerateResponseStreamAsync called with message: {userMessage.Substring(0, Math.Min(50, userMessage.Length))}...");

        if (_isDisposed)
            throw new ObjectDisposedException(nameof(VetaleAIService));

        if (_agent == null)
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIService: Agent not initialized, initializing (stream)...");
            await InitializeAsync();
        }

        if (_agent == null)
        {
            throw new InvalidOperationException("AI agent failed to initialize");
        }

        try
        {
            var languageHint = GetLanguageHint(language);
            return await _agent.GenerateResponseStreamAsync(userMessage, languageHint, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIService: Error generating response (stream): {ex}");
            throw;
        }
    }

    /// <summary>
    /// Reset conversation context
    /// </summary>
    public async Task ResetContextAsync()
    {
        if (_agent != null)
        {
            await _agent.ResetContextAsync();
        }
    }

    /// <summary>
    /// Get language hint string
    /// </summary>
    private string? GetLanguageHint(string? language)
    {
        return language switch
        {
            "uk" or "Ukrainian" => "Ukrainian",
            "en" or "English" => "English",
            "ru" or "Russian" => "Russian",
            "de" or "German" => "German",
            "fr" or "French" => "French",
            "es" or "Spanish" => "Spanish",
            _ => null
        };
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _agent?.Dispose();
        _agent = null;
        _isDisposed = true;
        
        System.Diagnostics.Trace.WriteLine("VetaleAIService: Disposed");
    }
}
