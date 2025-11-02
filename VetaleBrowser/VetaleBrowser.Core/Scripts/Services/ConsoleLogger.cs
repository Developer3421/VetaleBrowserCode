using System;
using System.Diagnostics;
using System.Threading;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Services;

/// <summary>
/// Глобальний логер для консолі
/// Перехоплює всі Debug.WriteLine та System.Console виклики (через TraceListener)
/// </summary>
public static class ConsoleLogger
{
    private static bool _isInitialized = false;
    private static readonly object _lock = new object();

    // Захист від рекурсії при логуванні з обробників Debug/Trace
    private static readonly AsyncLocal<bool> _inLog = new AsyncLocal<bool>();

    /// <summary>
    /// Кастомний TraceListener, який перенаправляє Debug/Trace у нашу базу логів
    /// </summary>
    private sealed class DebugToDbTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            // Ігноруємо часткові Write без переносу рядка
            // Основна обробка у WriteLine
        }

        public override void WriteLine(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            var msg = message!.Trim();

            string src = "Debug";
            string text = msg;

            // Формат 1: [Source] Message
            if (msg.StartsWith("[") && msg.IndexOf(']') > 1)
            {
                var end = msg.IndexOf(']');
                src = msg.Substring(1, end - 1);
                text = msg[(end + 1)..].TrimStart(' ', ':');
            }
            else
            {
                // Формат 2: Source: Message (наприклад, "NavigationBar: Back clicked")
                var colon = msg.IndexOf(':');
                if (colon > 0 && colon < 40) // розумна межа для назви джерела
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
    /// Ініціалізує глобальний логер консолі
    /// </summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_isInitialized) return;

            // Підключаємо перехоплення Debug/Trace повідомлень
            try
            {
                // AutoFlush для негайного виводу
                Trace.AutoFlush = true;

                var listener = new DebugToDbTraceListener();
                // Додаємо якщо ще не додано (лише через Trace.Listeners)
                bool hasListener = false;
                foreach (TraceListener l in Trace.Listeners)
                {
                    if (l is DebugToDbTraceListener) { hasListener = true; break; }
                }
                if (!hasListener)
                {
                    Trace.Listeners.Add(listener);
                    // Debug.Listeners може бути недоступний у цільовому середовищі — не використовуємо його
                }
            }
            catch
            {
                // Безпечне ігнорування: якщо не вдалося, просто не перехоплюємо Trace
            }
            
            _isInitialized = true;
            
            // Не пишемо повідомлення про ініціалізацію у БД, щоб у консолі був лише банер при відкритті вікна
            // LogInfo("Console Logger initialized", "ConsoleLogger");
        }
    }

    /// <summary>
    /// Логує інформаційне повідомлення
    /// </summary>
    public static void LogInfo(string message, string? source = null)
    {
        Log("Info", message, source);
    }

    /// <summary>
    /// Логує попередження
    /// </summary>
    public static void LogWarning(string message, string? source = null)
    {
        Log("Warning", message, source);
    }

    /// <summary>
    /// Логує помилку
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
    /// Логує дебаг повідомлення
    /// </summary>
    public static void LogDebug(string message, string? source = null)
    {
        Log("Debug", message, source);
    }

    /// <summary>
    /// Внутрішній метод логування
    /// </summary>
    private static void Log(string level, string message, string? source = null, string? stackTrace = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        // Захист від рекурсії: якщо ми вже всередині логування (можливе через TraceListener), пропускаємо
        if (_inLog.Value) return;

        try
        {
            _inLog.Value = true;

            // Зберігаємо в базу даних
            var consoleService = DatabaseManager.ConsoleInstance;
            consoleService?.AddLog(level, message, source, stackTrace);
        }
        catch (Exception ex)
        {
            // Якщо не вдалося залогувати, виводимо у Debug (це не буде рекурсувати завдяки _inLog)
            System.Diagnostics.Debug.WriteLine($"[ConsoleLogger] Failed to log: {ex.Message}");
        }
        finally
        {
            _inLog.Value = false;
        }
    }
}
