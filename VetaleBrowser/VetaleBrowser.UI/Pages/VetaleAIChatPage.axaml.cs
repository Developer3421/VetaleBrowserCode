using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VetaleAIChatPage : UserControl
{
    private ScrollViewer? _messageScrollViewer;
    private StackPanel? _messagesPanel;
    private TextBox? _messageInput;
    private Button? _sendButton;
    private ToggleButton? _reasoningToggle;
    private ComboBox? _languageSelector;

    private bool _isProcessing;

    public VetaleAIChatPage()
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
        _messageScrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
        _messagesPanel = this.FindControl<StackPanel>("MessagesPanel");
        _messageInput = this.FindControl<TextBox>("MessageInput");
        _sendButton = this.FindControl<Button>("SendButton");
        _reasoningToggle = this.FindControl<ToggleButton>("ReasoningToggle");
        _languageSelector = this.FindControl<ComboBox>("LanguageSelector");

        if (_messageInput != null)
        {
            _messageInput.TextChanged += (_, _) => UpdateSendButtonState();
        }

        // Initialize LlamaSharp model
        InitializeAIModel();
    }

    private void UpdateSendButtonState()
    {
        if (_sendButton != null && _messageInput != null)
        {
            _sendButton.IsEnabled = !string.IsNullOrWhiteSpace(_messageInput.Text) && !_isProcessing;
        }
    }

    private void MessageInput_KeyDown(object? sender, KeyEventArgs e)
    {
        // Send on Enter, new line on Shift+Enter
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            Send_Click(sender, new RoutedEventArgs());
        }
    }

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        if (_messageInput == null || string.IsNullOrWhiteSpace(_messageInput.Text))
            return;

        var userMessage = _messageInput.Text.Trim();
        _messageInput.Text = string.Empty;
        _isProcessing = true;
        UpdateSendButtonState();

        // Add user message
        AddUserMessage(userMessage);

        // Get AI response
        await GetAIResponse(userMessage);

        _isProcessing = false;
        UpdateSendButtonState();
    }

    private void AddUserMessage(string text)
    {
        if (_messagesPanel == null) return;

        var messageBorder = new Border
        {
            Classes = { "message-bubble", "user-message" },
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = Application.Current?.FindResource("VetaleAI.User") as string ?? "You",
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.Parse("#666666"))
                    },
                    new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14
                    }
                }
            }
        };

        _messagesPanel.Children.Add(messageBorder);
        ScrollToBottom();
    }

    private void AddAssistantMessage(string text)
    {
        if (_messagesPanel == null) return;

        var messageBorder = new Border
        {
            Classes = { "message-bubble", "assistant-message" },
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = Application.Current?.FindResource("VetaleAI.Assistant") as string ?? "Vetale AI",
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.Parse("#4CAF50"))
                    },
                    new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14
                    }
                }
            }
        };

        _messagesPanel.Children.Add(messageBorder);
        ScrollToBottom();
    }

    private void AddThinkingMessage()
    {
        if (_messagesPanel == null) return;

        var thinkingBorder = new Border
        {
            Name = "ThinkingMessage",
            Classes = { "message-bubble", "assistant-message" },
            Child = new StackPanel
            {
                Spacing = 4,
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Children =
                {
                    new TextBlock
                    {
                        Text = "💭",
                        FontSize = 16
                    },
                    new TextBlock
                    {
                        Text = Application.Current?.FindResource("VetaleAI.Thinking") as string ?? "Thinking...",
                        FontStyle = FontStyle.Italic,
                        Foreground = new SolidColorBrush(Color.Parse("#999999"))
                    }
                }
            }
        };

        _messagesPanel.Children.Add(thinkingBorder);
        ScrollToBottom();
    }

    private void RemoveThinkingMessage()
    {
        if (_messagesPanel == null) return;

        var thinkingMessage = _messagesPanel.Children
            .OfType<Border>()
            .FirstOrDefault(b => b.Name == "ThinkingMessage");

        if (thinkingMessage != null)
        {
            _messagesPanel.Children.Remove(thinkingMessage);
        }
    }

    private void ScrollToBottom()
    {
        if (_messageScrollViewer != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _messageScrollViewer.ScrollToEnd();
            }, DispatcherPriority.Background);
        }
    }

    private async Task GetAIResponse(string userMessage)
    {
        AddThinkingMessage();

        try
        {
            // TODO: Implement LlamaSharp integration here
            await Task.Delay(1000); // Simulate processing

            var response = await GenerateResponseAsync(userMessage);

            RemoveThinkingMessage();
            AddAssistantMessage(response);
        }
        catch (Exception ex)
        {
            RemoveThinkingMessage();
            AddAssistantMessage($"Error: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Error getting AI response: {ex}");
        }
    }

    private async Task<string> GenerateResponseAsync(string prompt)
    {
        // TODO: Replace with actual LlamaSharp implementation
        // This is a placeholder
        await Task.Delay(500);

        var enableReasoning = _reasoningToggle?.IsChecked ?? false;
        var languageIndex = _languageSelector?.SelectedIndex ?? 0;

        string languageInstruction = languageIndex switch
        {
            1 => "Please respond in Ukrainian.",
            2 => "Please respond in English.",
            3 => "Please respond in Russian.",
            4 => "Please respond in German.",
            5 => "Please respond in French.",
            6 => "Please respond in Spanish.",
            _ => ""
        };

        // Placeholder response
        return $"[Placeholder response]\n\nYou said: {prompt}\n\nReasoning enabled: {enableReasoning}\n{languageInstruction}\n\nNote: LlamaSharp integration is pending. This is a demo response.";
    }

    private void InitializeAIModel()
    {
        try
        {
            // TODO: Initialize LlamaSharp model
            // Path to model: VetaleBrowser.AI/Model/gemma-3-1b-it-UD-Q2_K_XL.gguf
            System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: AI model initialization placeholder");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Error initializing AI model: {ex}");
        }
    }

    private void NewChat_Click(object? sender, RoutedEventArgs e)
    {
        if (_messagesPanel == null) return;

        // Clear all messages except welcome message
        var childrenToRemove = _messagesPanel.Children
            .Skip(1) // Skip welcome message
            .ToList();

        foreach (var child in childrenToRemove)
        {
            _messagesPanel.Children.Remove(child);
        }
    }

    private void ClearHistory_Click(object? sender, RoutedEventArgs e)
    {
        NewChat_Click(sender, e);
    }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

