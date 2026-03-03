using System;
using System.Diagnostics;
using System.Threading;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Services;

/// <summary>
/// Global console logger.
/// Intercepts all Debug.WriteLine and System.Console calls (via TraceListener).
/// </summary>
public static class ConsoleLogger
{
    private static bool _isInitialized = false;
    private static readonly object _lock = new object();

    // Recursion protection when logging from Debug/Trace handlers
    private static readonly AsyncLocal<bool> _inLog = new AsyncLocal<bool>();

    /// <summary>
    /// Custom TraceListener that redirects Debug/Trace to our log database
    /// </summary>
    private sealed class DebugToDbTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            // Ignore partial Write without line break
            // Main processing in WriteLine
        }

        public override void WriteLine(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            var msg = message!.Trim();

            string src = "Debug";
            string text = msg;

            // Format 1: [Source] Message
            if (msg.StartsWith("[") && msg.IndexOf(']') > 1)
            {
                var end = msg.IndexOf(']');
                src = msg.Substring(1, end - 1);
                text = msg[(end + 1)..].TrimStart(' ', ':');
            }
            else
            {
                // Format 2: Source: Message (e.g., "NavigationBar: Back clicked")
                var colon = msg.IndexOf(':');
                if (colon > 0 && colon < 40) // reasonable limit for source name
                {
                    var left = msg.Substring(0, colon).Trim();
                    var right = msg[(colon + 1)..].TrimStart();
                    if (!string.IsNullOrEmpty(left) && right.Length > 0)
                    {
                        src = left;
                        text = right;
                    }
                }
            }

            Log("Debug", text, src);
        }
    }

    /// <summary>
    /// Initializes the global console logger
    /// </summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_isInitialized) return;

            // Connect Debug/Trace message interception
            try
            {
                // AutoFlush for immediate output
                Trace.AutoFlush = true;

                var listener = new DebugToDbTraceListener();
                // Add if not already added (only through Trace.Listeners)
                bool hasListener = false;
                foreach (TraceListener l in Trace.Listeners)
                {
                    if (l is DebugToDbTraceListener) { hasListener = true; break; }
                }
                if (!hasListener)
                {
                    Trace.Listeners.Add(listener);
                    // Debug.Listeners may not be available in the target environment — don't use it
                }
            }
            catch
            {
                // Safe ignore: if failed, just don't intercept Trace
            }
            
            _isInitialized = true;
            
            // Don't write initialization message to DB, so console only has a banner when the window opens
            // LogInfo("Console Logger initialized", "ConsoleLogger");
        }
    }

    /// <summary>
    /// Logs an informational message
    /// </summary>
    public static void LogInfo(string message, string? source = null)
    {
        Log("Info", message, source);
    }

    /// <summary>
    /// Logs a warning
    /// </summary>
    public static void LogWarning(string message, string? source = null)
    {
        Log("Warning", message, source);
    }

    /// <summary>
    /// Logs an error
    /// </summary>
    public static void LogError(string message, string? source = null, Exception? exception = null)
    {
        var fullMessage = exception != null 
            ? $"{message}\nException: {exception.Message}" 
            : message;
        var stackTrace = exception?.StackTrace;
        
        Log("Error", fullMessage, source, stackTrace);
    }

    /// <summary>
    /// Logs a debug message
    /// </summary>
    public static void LogDebug(string message, string? source = null)
    {
        Log("Debug", message, source);
    }

    /// <summary>
    /// Internal logging method
    /// </summary>
    private static void Log(string level, string message, string? source = null, string? stackTrace = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        // Recursion protection: if we're already inside logging (possible through TraceListener), skip
        if (_inLog.Value) return;

        try
        {
            _inLog.Value = true;

            // Save to database
            var consoleService = DatabaseManager.ConsoleInstance;
            consoleService?.AddLog(level, message, source, stackTrace);
        }
        catch (Exception ex)
        {
            // If logging failed, output to Debug (this won't recurse thanks to _inLog)
            System.Diagnostics.Debug.WriteLine($"[ConsoleLogger] Failed to log: {ex.Message}");
        }
        finally
        {
            _inLog.Value = false;
        }
    }
}
