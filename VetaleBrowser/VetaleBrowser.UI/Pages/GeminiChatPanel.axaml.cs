using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.Search.Services;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class GeminiChatPanel : UserControl, IDisposable
{
    private ScrollViewer? _messageScrollViewer;
    private StackPanel? _messagesPanel;
    private TextBox? _messageInput;
    private Button? _sendButton;
    private Button? _stopButton;
    private Button? _scrollToBottomButton;
    private Button? _clearButton;
    
    private bool _autoScroll = true;
    private bool _isProcessing;
    private GeminiAiSummaryService? _geminiService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isDisposed;

    public GeminiChatPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
        _stopButton = this.FindControl<Button>("StopButton");
        _scrollToBottomButton = this.FindControl<Button>("ScrollToBottomButton");
        _clearButton = this.FindControl<Button>("ClearButton");

        if (_messageScrollViewer != null)
        {
            _messageScrollViewer.ScrollChanged += MessageScrollViewer_ScrollChanged;
        }

        // Initialize Gemini service
        _geminiService = new GeminiAiSummaryService();
        
        System.Diagnostics.Debug.WriteLine("[GeminiChat] Panel loaded and initialized");
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Dispose();
    }

    private void MessageScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_messageScrollViewer == null || _scrollToBottomButton == null)
            return;

        var extent = _messageScrollViewer.Extent.Height;
        var viewport = _messageScrollViewer.Viewport.Height;
        var offset = _messageScrollViewer.Offset.Y;
        var atBottom = (extent - (offset + viewport)) <= 10;

        _autoScroll = atBottom;
        _scrollToBottomButton.IsVisible = !atBottom && _messagesPanel?.Children.Count > 1;
    }

    private void ScrollToBottomButton_Click(object? sender, RoutedEventArgs e)
    {
        _autoScroll = true;
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        if (_messageScrollViewer != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _messageScrollViewer.ScrollToEnd();
                
                var maxOffset = Math.Max(0, _messageScrollViewer.Extent.Height - _messageScrollViewer.Viewport.Height);
                _messageScrollViewer.Offset = new Vector(0, maxOffset);
                
                if (_scrollToBottomButton != null)
                {
                    _scrollToBottomButton.IsVisible = false;
                }
            }, DispatcherPriority.Background);
        }
    }

    private void MessageInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateSendButtonState();
    }

    private void MessageInput_KeyDown(object? sender, KeyEventArgs e)
    {
        // Enter sends; Shift+Enter = newline
        if (e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                // allow newline
                return;
            }

            e.Handled = true;
            SendMessage();
        }
    }

    private void SendButton_Click(object? sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        StopGeneration();
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        ClearChat();
    }

    private void UpdateSendButtonState()
    {
        if (_sendButton != null && _messageInput != null)
        {
            _sendButton.IsEnabled = !string.IsNullOrWhiteSpace(_messageInput.Text) && !_isProcessing;
        }

        if (_stopButton != null)
        {
            _stopButton.IsVisible = _isProcessing;
        }
    }

    private void SendMessage()
    {
        if (_messageInput == null || string.IsNullOrWhiteSpace(_messageInput.Text) || _isProcessing)
            return;

        var userMessage = _messageInput.Text.Trim();
        _messageInput.Text = string.Empty;

        System.Diagnostics.Debug.WriteLine($"[GeminiChat] User message: {userMessage}");

        AddUserMessage(userMessage);
        _ = GenerateResponseAsync(userMessage);
    }

    private async Task GenerateResponseAsync(string userMessage)
    {
        if (_geminiService == null)
        {
            AddAssistantMessage("❌ Помилка: сервіс Gemini не ініціалізовано");
            return;
        }

        _isProcessing = true;
        UpdateSendButtonState();

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        // Add loading message
        var loadingBorder = AddLoadingMessage();

        try
        {
            System.Diagnostics.Debug.WriteLine("[GeminiChat] Sending request to Gemini...");

            // Use GeminiAiSummaryService to generate chat response
            var summary = await _geminiService.GenerateChatResponseAsync(userMessage, token);

            // Remove loading message
            if (loadingBorder != null && _messagesPanel != null)
            {
                _messagesPanel.Children.Remove(loadingBorder);
            }

            if (token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine("[GeminiChat] Request cancelled");
                return;
            }

            if (summary.State == AiSummaryState.Ready && !string.IsNullOrEmpty(summary.SummaryText))
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiChat] Response received: {summary.SummaryText.Length} chars");
                AddAssistantMessage(summary.SummaryText);
            }
            else if (summary.State == AiSummaryState.Error)
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiChat] Error: {summary.ErrorMessage}");
                AddAssistantMessage($"❌ Помилка: {summary.ErrorMessage}");
            }
            else
            {
                AddAssistantMessage("❌ Не вдалося отримати відповідь від Gemini");
            }
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[GeminiChat] Task cancelled");
            if (loadingBorder != null && _messagesPanel != null)
            {
                _messagesPanel.Children.Remove(loadingBorder);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiChat] Exception: {ex}");
            if (loadingBorder != null && _messagesPanel != null)
            {
                _messagesPanel.Children.Remove(loadingBorder);
            }
            AddAssistantMessage($"❌ Помилка: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            UpdateSendButtonState();
        }
    }

    private void StopGeneration()
    {
        System.Diagnostics.Debug.WriteLine("[GeminiChat] Stopping generation...");
        _cancellationTokenSource?.Cancel();
        _isProcessing = false;
        UpdateSendButtonState();
    }

    private void ClearChat()
    {
        System.Diagnostics.Debug.WriteLine("[GeminiChat] Clearing chat...");
        
        if (_messagesPanel == null)
            return;

        // Keep only the welcome message (first child)
        while (_messagesPanel.Children.Count > 1)
        {
            _messagesPanel.Children.RemoveAt(_messagesPanel.Children.Count - 1);
        }

        ScrollToBottom();
    }

    private void AddUserMessage(string text)
    {
        if (_messagesPanel == null)
            return;

        var contentStack = new StackPanel
        {
            Classes = { "message-content" },
            Children =
            {
                new TextBlock
                {
                    Classes = { "message-header", "user-header" },
                    Text = "Ви"
                },
                new TextBlock
                {
                    Classes = { "message-text" },
                    Text = text
                }
            }
        };

        var messageBorder = new Border
        {
            Classes = { "message-container", "user-message" },
            Child = contentStack
        };

        _messagesPanel.Children.Add(messageBorder);
        
        if (_autoScroll)
            ScrollToBottom();
    }

    private void AddAssistantMessage(string text)
    {
        if (_messagesPanel == null)
            return;

        var contentStack = new StackPanel
        {
            Classes = { "message-content" },
            Children =
            {
                new TextBlock
                {
                    Classes = { "message-header", "assistant-header" },
                    Text = "Gemini"
                },
                new TextBlock
                {
                    Classes = { "message-text" },
                    Text = text
                }
            }
        };

        var messageBorder = new Border
        {
            Classes = { "message-container", "assistant-message" },
            Child = contentStack
        };

        _messagesPanel.Children.Add(messageBorder);
        
        if (_autoScroll)
            ScrollToBottom();
    }

    private Border? AddLoadingMessage()
    {
        if (_messagesPanel == null)
            return null;

        var loadingStack = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "✨",
                    FontSize = 16
                },
                new TextBlock
                {
                    Text = "Gemini думає...",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.Parse("#666666"))
                }
            }
        };

        var loadingBorder = new Border
        {
            Classes = { "loading-indicator" },
            Child = loadingStack
        };

        _messagesPanel.Children.Add(loadingBorder);
        
        if (_autoScroll)
            ScrollToBottom();

        return loadingBorder;
    }

    // Public API to trigger chat from outside
    public async Task AskAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            AddUserMessage(message.Trim());
        });

        await GenerateResponseAsync(message.Trim());
    }

    public void FocusInput()
    {
        _messageInput?.Focus();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _geminiService?.Dispose();
        
        _isDisposed = true;
        
        System.Diagnostics.Debug.WriteLine("[GeminiChat] Panel disposed");
    }
}

