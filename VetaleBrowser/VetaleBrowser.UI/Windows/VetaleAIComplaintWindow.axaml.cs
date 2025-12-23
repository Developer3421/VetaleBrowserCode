using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class VetaleAIComplaintWindow : Window
{
    private TextBox? _complaintTextBox;
    private TextBlock? _statusTextBlock;

    public string? UserMessageContext { get; set; }
    public string? AssistantMessageContext { get; set; }

    public VetaleAIComplaintWindow()
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
        _complaintTextBox = this.FindControl<TextBox>("ComplaintTextBox");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

    private async void SaveToFile_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var complaintText = _complaintTextBox?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(complaintText))
            {
                ShowStatus(TryGetString("VetaleAI.Complaint.ErrorEmpty", "Please write a complaint before saving."), isError: true);
                return;
            }

            var suggestedName = $"vetale_ai_complaint_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = TryGetString("VetaleAI.Complaint.SavePickerTitle", "Save complaint"),
                DefaultExtension = "txt",
                SuggestedFileName = suggestedName
            });

            if (file == null)
                return;

            var filePath = file.Path.LocalPath;
            var payload = BuildComplaintFileText(complaintText);

            await File.WriteAllTextAsync(filePath, payload, Encoding.UTF8);

            ShowStatus(string.Format(TryGetString("VetaleAI.Complaint.Saved", "File saved: {0}"), filePath), isError: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[VetaleAIComplaintWindow] SaveToFile error: {ex}");
            ShowStatus(string.Format(TryGetString("VetaleAI.Complaint.SaveError", "Save error: {0}"), ex.Message), isError: true);
        }
    }

    private string BuildComplaintFileText(string complaintText)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Vetale Browser: Скарга на відповідь AI ===");
        sb.AppendLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(UserMessageContext))
        {
            sb.AppendLine("--- Повідомлення користувача (контекст) ---");
            sb.AppendLine(UserMessageContext);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(AssistantMessageContext))
        {
            sb.AppendLine("--- Відповідь AI (контекст) ---");
            sb.AppendLine(AssistantMessageContext);
            sb.AppendLine();
        }

        sb.AppendLine("--- Скарга користувача ---");
        sb.AppendLine(complaintText);
        sb.AppendLine();

        sb.AppendLine("Відправте будь ласка цей файл на vetalebrowser01@gmail.com");
        return sb.ToString();
    }

    private void ShowStatus(string message, bool isError)
    {
        if (_statusTextBlock == null)
            return;

        _statusTextBlock.IsVisible = true;
        _statusTextBlock.Text = message;
        _statusTextBlock.Foreground = isError
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#C62828"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2E7D32"));
    }

    private static string TryGetString(string key, string fallback)
    {
        try
        {
            if (Application.Current != null && Application.Current.TryFindResource(key, out var value) && value is string s)
                return s;
        }
        catch { }

        return fallback;
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
