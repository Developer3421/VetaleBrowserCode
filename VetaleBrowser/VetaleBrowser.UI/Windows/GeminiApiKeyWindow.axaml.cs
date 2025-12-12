using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// Window for configuring Gemini API key
/// </summary>
public partial class GeminiApiKeyWindow : Window
{
    private TextBox? _apiKeyTextBox;
    private TaskCompletionSource<GeminiApiKeyResult>? _resultTcs;

    public GeminiApiKeyWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _apiKeyTextBox = this.FindControl<TextBox>("ApiKeyTextBox");
        _apiKeyTextBox?.Focus();
    }

    /// <summary>
    /// Shows the dialog and returns the result
    /// </summary>
    public Task<GeminiApiKeyResult> ShowDialogAsync(Window? parent = null)
    {
        _resultTcs = new TaskCompletionSource<GeminiApiKeyResult>();
        
        Closed += (s, e) =>
        {
            if (!_resultTcs.Task.IsCompleted)
            {
                _resultTcs.SetResult(new GeminiApiKeyResult { Cancelled = true });
            }
        };

        if (parent != null)
        {
            _ = ShowDialog(parent);
        }
        else
        {
            Show();
        }

        return _resultTcs.Task;
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void CloseWindow_Click(object? sender, RoutedEventArgs e)
    {
        _resultTcs?.TrySetResult(new GeminiApiKeyResult { Cancelled = true });
        Close();
    }

    private void UseCustomKey_Click(object? sender, RoutedEventArgs e)
    {
        var apiKey = _apiKeyTextBox?.Text?.Trim();
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Show error - empty key
            System.Diagnostics.Debug.WriteLine("[GeminiApiKeyWindow] Empty API key entered");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[GeminiApiKeyWindow] Custom API key provided: {apiKey.Substring(0, Math.Min(10, apiKey.Length))}...");
        
        _resultTcs?.TrySetResult(new GeminiApiKeyResult 
        { 
            UseDefaultKey = false, 
            CustomApiKey = apiKey,
            Cancelled = false
        });
        
        Close();
    }

    private void UseDefaultKey_Click(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[GeminiApiKeyWindow] Using default API key");
        
        _resultTcs?.TrySetResult(new GeminiApiKeyResult 
        { 
            UseDefaultKey = true,
            Cancelled = false
        });
        
        Close();
    }

    private void OpenAIStudio_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "https://aistudio.google.com/app/apikey",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiApiKeyWindow] Failed to open AI Studio: {ex.Message}");
        }
    }
}

/// <summary>
/// Result from the Gemini API key dialog
/// </summary>
public class GeminiApiKeyResult
{
    public bool Cancelled { get; set; }
    public bool UseDefaultKey { get; set; }
    public string? CustomApiKey { get; set; }
}

