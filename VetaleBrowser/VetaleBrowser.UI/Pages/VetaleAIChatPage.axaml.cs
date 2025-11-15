using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.AI;
using System.Text.RegularExpressions;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VetaleAIChatPage : UserControl, IDisposable
{
    private ScrollViewer? _messageScrollViewer;
    private StackPanel? _messagesPanel;
    private TextBox? _messageInput;
    private Button? _sendButton;
    private ComboBox? _languageSelector;
    private Button? _scrollToBottomButton;
    private Button? _stopButton;
    private bool _autoScroll = true;

    private bool _isProcessing;
    private VetaleAIService? _aiService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isDisposed;
    private Task? _ongoingResponseTask;

    private static readonly Regex TrailingUserCueRegex = new(@"(?:\s|:|#|\[|\])*(\*{0,2}\s*)?(User|Human|Assistant|AI|Q|A)(\s*\*{0,2})?(?:\s*:)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlCodeBlockRegex = new(@"```html[\r\n]+([\s\S]*?)```", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public VetaleAIChatPage()
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
        _languageSelector = this.FindControl<ComboBox>("LanguageSelector");
        _scrollToBottomButton = this.FindControl<Button>("ScrollToBottomButton");
        _stopButton = this.FindControl<Button>("StopButton");

        if (_messageInput != null)
        {
            _messageInput.TextChanged += (_, _) => UpdateSendButtonState();
            _messageInput.KeyDown += MessageInput_KeyDown; // Enter sends, Shift+Enter newline
        }

        if (_messageScrollViewer != null)
        {
            _messageScrollViewer.ScrollChanged += MessageScrollViewer_ScrollChanged;
        }

        if (_scrollToBottomButton != null)
        {
            _scrollToBottomButton.Click += ScrollToBottomButton_Click;
        }

        // Initialize LlamaSharp model
        InitializeAIModel();
    }

    private void MessageScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_messageScrollViewer == null || _scrollToBottomButton == null)
            return;

        // Determine if user is near bottom (within 8px tolerance)
        var extent = _messageScrollViewer.Extent.Height;
        var viewport = _messageScrollViewer.Viewport.Height;
        var offset = _messageScrollViewer.Offset.Y;
        var atBottom = (extent - (offset + viewport)) <= 8;

        _autoScroll = atBottom;
        _scrollToBottomButton.IsVisible = !atBottom;
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
                // Scroll to end and ensure we're at the very bottom
                _messageScrollViewer.ScrollToEnd();
                
                // Force scroll to maximum offset to ensure complete scroll
                var maxOffset = Math.Max(0, _messageScrollViewer.Extent.Height - _messageScrollViewer.Viewport.Height);
                _messageScrollViewer.Offset = new Vector(0, maxOffset);
                
                if (_scrollToBottomButton != null)
                {
                    // Hide the button once we're at bottom
                    _scrollToBottomButton.IsVisible = false;
                }
            }, DispatcherPriority.Background);
        }
    }

    private void UpdateSendButtonState()
    {
        if (_sendButton != null && _messageInput != null)
        {
            _sendButton.IsEnabled = !string.IsNullOrWhiteSpace(_messageInput.Text) && !_isProcessing;
        }

        if (_stopButton != null)
        {
            _stopButton.IsEnabled = _isProcessing; // enabled only while processing
            _stopButton.IsVisible = _isProcessing; // visible only while processing
        }
    }

    private void AddUserMessage(string text)
    {
        if (_messagesPanel == null) return;

        var contentStack = new StackPanel
        {
            Classes = { "message-content" },
            Spacing = 6,
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
                    FontSize = 15,
                    LineHeight = 24
                }
            }
        };

        var messageBorder = new Border
        {
            Classes = { "message-container", "user-message" },
            Child = contentStack
        };

        _messagesPanel.Children.Add(messageBorder);
        if (_autoScroll) ScrollToBottom();
    }

    private void AddAssistantMessage(string text)
    {
        if (_messagesPanel == null) return;

        // Парсимо HTML-код із markdown-блоку ```html ... ```
        string? codePart = null;
        string descriptionPart = text;

        var match = HtmlCodeBlockRegex.Match(text);
        if (match.Success && match.Groups.Count > 1)
        {
            codePart = match.Groups[1].Value.Trim('\r', '\n');
            // Все, що до і після блока коду
            var before = text.Substring(0, match.Index).Trim();
            var after = text[(match.Index + match.Length)..].Trim();
            descriptionPart = string.Join("\n\n", new[] { before, after }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        var contentStack = new StackPanel
        {
            Classes = { "message-content" },
            Spacing = 6
        };

        // Заголовок "Vetale AI"
        contentStack.Children.Add(new TextBlock
        {
            Text = Application.Current?.FindResource("VetaleAI.Assistant") as string ?? "Vetale AI",
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#4CAF50"))
        });

        // Описова частина (якщо є)
        if (!string.IsNullOrWhiteSpace(descriptionPart))
        {
            contentStack.Children.Add(new TextBlock
            {
                Text = descriptionPart,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                LineHeight = 24
            });
        }

        // HTML-код у окремому контейнері (якщо є)
        if (!string.IsNullOrWhiteSpace(codePart))
        {
            var codeTextBlock = new TextBlock
            {
                Text = codePart,
                Classes = { "code-text" }
            };

            var codeBorder = new Border
            {
                Classes = { "code-container" },
                Child = codeTextBlock
            };

            contentStack.Children.Add(codeBorder);
        }

        var messageBorder = new Border
        {
            Classes = { "message-container", "assistant-message" },
            Child = contentStack
        };

        _messagesPanel.Children.Add(messageBorder);
        if (_autoScroll) ScrollToBottom();
    }

    private void AddThinkingMessage()
    {
        if (_messagesPanel == null) return;

        var contentStack = new StackPanel
        {
            Classes = { "message-content" },
            Spacing = 6,
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Children =
            {
                new TextBlock
                {
                    Text = "💭",
                    FontSize = 18
                },
                new TextBlock
                {
                    Text = Application.Current?.FindResource("VetaleAI.Thinking") as string ?? "Thinking...",
                    FontStyle = FontStyle.Italic,
                    Foreground = new SolidColorBrush(Color.Parse("#999999")),
                    FontSize = 15,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                }
            }
        };

        var thinkingBorder = new Border
        {
            Name = "ThinkingMessage",
            Classes = { "message-container", "assistant-message" },
            Child = contentStack
        };

        _messagesPanel.Children.Add(thinkingBorder);
        if (_autoScroll) ScrollToBottom();
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

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        if (_messageInput == null || string.IsNullOrWhiteSpace(_messageInput.Text))
            return;

        if (_isProcessing)
            return; // prevent double send

        var userMessage = _messageInput.Text.Trim();
        _messageInput.Text = string.Empty;
        _isProcessing = true;
        UpdateSendButtonState();

        // Add user message
        AddUserMessage(userMessage);

        // Get AI response
        _ongoingResponseTask = GetAIResponseInternal(userMessage);
        await _ongoingResponseTask;
        _ongoingResponseTask = null;

        _isProcessing = false;
        UpdateSendButtonState();
    }

    private void Stop_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isProcessing)
            return;
        System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: Stop requested");
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch { }
        finally
        {
            _isProcessing = false; // will be flipped when cancellation observed
            UpdateSendButtonState();
        }
    }

    private async Task GetAIResponseInternal(string userMessage)
    {
        AddThinkingMessage();

        try
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Getting AI response for: {userMessage}");

            if (_aiService == null)
            {
                RemoveThinkingMessage();
                AddAssistantMessage("AI service is not initialized. Please wait a moment and try again.");
                return;
            }

            var languageIndex = _languageSelector?.SelectedIndex ?? 0;
            string? language = languageIndex switch
            {
                1 => "Ukrainian",
                2 => "English",
                3 => "Russian",
                4 => "German",
                5 => "French",
                6 => "Spanish",
                _ => null // Auto-detect
            };

            // Streaming setup
            TextBlock? streamingTextBlock = null;
            string currentResponse = string.Empty;
            bool firstTokenSeen = false;

            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;
            // Ensure stop button state reflects processing
            if (_stopButton != null)
            {
                _stopButton.IsVisible = true;
                _stopButton.IsEnabled = true;
            }

            var progress = new Progress<string>(token =>
            {
                // Accumulate and clean trailing User cues
                currentResponse += token;
                currentResponse = StripTrailingUserCue(currentResponse);

                Dispatcher.UIThread.Post(() =>
                {
                    if (!firstTokenSeen)
                    {
                        // Replace thinking bubble with a real assistant bubble
                        RemoveThinkingMessage();
                        streamingTextBlock = CreateAssistantMessageBubble(string.Empty);
                        firstTokenSeen = true;
                    }

                    if (streamingTextBlock != null)
                    {
                        streamingTextBlock.Text = currentResponse;
                    }

                    if (_autoScroll)
                    {
                        ScrollToBottom();
                    }
                }, DispatcherPriority.Background);
            });

            string finalText;
            try
            {
                finalText = await _aiService.GenerateResponseStreamAsync(
                    userMessage,
                    language,
                    progress,
                    ct);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                if (_stopButton != null)
                {
                    _stopButton.IsEnabled = false; // disable while finishing cleanup
                }
            }

            finalText = StripTrailingUserCue(finalText);

            if (!firstTokenSeen)
            {
                RemoveThinkingMessage();
                AddAssistantMessage(finalText ?? "No response generated");
            }
            else if (streamingTextBlock != null)
            {
                streamingTextBlock.Text = string.IsNullOrWhiteSpace(finalText) ? "No response generated" : finalText;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled: remove thinking and show cancelled info
            RemoveThinkingMessage();
            AddAssistantMessage("Response generation was cancelled.");
            System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: Response cancelled");
        }
        catch (Exception ex)
        {
            RemoveThinkingMessage();
            AddAssistantMessage($"Error: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Error getting AI response: {ex}");
        }
        finally
        {
            if (_stopButton != null)
            {
                _stopButton.IsVisible = false;
                _stopButton.IsEnabled = false;
            }
        }
    }

    private async Task GetAIResponse(string userMessage)
    {
        // Backward compatibility wrapper
        await GetAIResponseInternal(userMessage);
    }

    private async Task<string> GenerateResponseAsync(string prompt)
    {
        System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: GenerateResponseAsync called with: {prompt}");
        
        if (_aiService == null)
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: AI service is NULL!");
            return "AI service is not initialized. Please wait a moment and try again.";
        }

        var maxRetries = 2;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var languageIndex = _languageSelector?.SelectedIndex ?? 0;

                string? language = languageIndex switch
                {
                    1 => "Ukrainian",
                    2 => "English",
                    3 => "Russian",
                    4 => "German",
                    5 => "French",
                    6 => "Spanish",
                    _ => null // Auto-detect
                };

                System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Calling AI service (attempt {attempt + 1}) with language={language}");

                _cancellationTokenSource = new CancellationTokenSource();
                if (_stopButton != null)
                {
                    _stopButton.IsVisible = true;
                    _stopButton.IsEnabled = true;
                }
                var response = await _aiService.GenerateResponseAsync(
                    prompt, 
                    language, 
                    _cancellationTokenSource.Token);

                System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Response received, length={response.Length}");
                
                return response ?? "No response generated";
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: Generation was cancelled");
                return "Response generation was cancelled.";
            }
            catch (Exception ex) when (ex.Message.Contains("InvalidInputBatch") || ex.Message.Contains("context") || ex.Message.Contains("batch"))
            {
                System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Context error on attempt {attempt + 1}: {ex.Message}");
                
                if (attempt < maxRetries - 1)
                {
                    // Reset context and retry
                    System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: Resetting context and retrying...");
                    try
                    {
                        await _aiService.ResetContextAsync();
                        await Task.Delay(500); // Small delay before retry
                        continue;
                    }
                    catch (Exception resetEx)
                    {
                        System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Failed to reset context: {resetEx.Message}");
                        return $"Error: Failed to reset AI context. Please try 'New Chat'.";
                    }
                }
                else
                {
                    return $"Error: AI context error. Please click 'New Chat' to reset. Details: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: EXCEPTION in GenerateResponseAsync: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Stack trace: {ex.StackTrace}");
                return $"Error: {ex.Message}";
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                if (_stopButton != null)
                {
                    _stopButton.IsVisible = false;
                    _stopButton.IsEnabled = false;
                }
            }
        }
        
        return "Error: Maximum retries exceeded.";
    }

    private async void InitializeAIModel()
    {
        System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: InitializeAIModel started");
        
        try
        {
            _aiService = new VetaleAIService();
            System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: VetaleAIService created");
            
            // Initialize asynchronously
            await _aiService.InitializeAsync();
            
            System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: AI model initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: ERROR initializing AI model: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Stack trace: {ex.StackTrace}");
            
            if (_messagesPanel != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AddAssistantMessage($"⚠️ Failed to initialize AI model: {ex.Message}");
                });
            }
        }
    }

    private async void NewChat_Click(object? sender, RoutedEventArgs e)
    {
        if (_messagesPanel == null) return;

        try
        {
            System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: New chat requested");
            
            // Cancel any ongoing generation
            _cancellationTokenSource?.Cancel();

            // Wait briefly for ongoing task to observe cancellation to avoid resetting during generation
            if (_ongoingResponseTask != null)
            {
                var completed = await Task.WhenAny(_ongoingResponseTask, Task.Delay(1500)) == _ongoingResponseTask;
                System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Ongoing task completed before reset: {completed}");
            }

            // Clear all messages except welcome message
            var childrenToRemove = _messagesPanel.Children
                .Skip(1) // Skip welcome message
                .ToList();

            foreach (var child in childrenToRemove)
            {
                _messagesPanel.Children.Remove(child);
            }

            // Ensure thinking bubble is removed if it was shown
            RemoveThinkingMessage();

            // Reset AI context
            if (_aiService != null)
            {
                System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: Resetting AI context...");
                await _aiService.ResetContextAsync();
                System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: AI context reset complete");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIChatPage: Error in NewChat: {ex}");
            AddAssistantMessage($"Error resetting chat: {ex.Message}");
        }
    }

    private void ClearHistory_Click(object? sender, RoutedEventArgs e)
    {
        NewChat_Click(sender, e);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // Unsubscribe handlers to avoid leaks
        if (_messageScrollViewer != null)
        {
            _messageScrollViewer.ScrollChanged -= MessageScrollViewer_ScrollChanged;
        }
        if (_messageInput != null)
        {
            _messageInput.KeyDown -= MessageInput_KeyDown;
        }
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _aiService?.Dispose();
        
        _isDisposed = true;
        
        System.Diagnostics.Trace.WriteLine("VetaleAIChatPage: Disposed");
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

    private string StripTrailingUserCue(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // Iteratively strip trailing "User" / "**User**" (with optional colon) cues at the end
        string prev;
        do
        {
            prev = text;
            text = TrailingUserCueRegex.Replace(text, string.Empty);
            text = text.TrimEnd();
        } while (text.Length > 0 && text != prev && TrailingUserCueRegex.IsMatch(text));
        return text;
    }

    private TextBlock CreateAssistantMessageBubble(string initialText)
    {
        if (_messagesPanel == null)
        {
            return new TextBlock { Text = initialText };
        }

        // Для стрімінгу використовуємо простий текст без спец-контейнера коду
        var contentTextBlock = new TextBlock
        {
            Text = initialText,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15,
            LineHeight = 24
        };

        var contentStack = new StackPanel
        {
            Classes = { "message-content" },
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = Application.Current?.FindResource("VetaleAI.Assistant") as string ?? "Vetale AI",
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.Parse("#4CAF50"))
                },
                contentTextBlock
            }
        };

        var messageBorder = new Border
        {
            Classes = { "message-container", "assistant-message" },
            Child = contentStack
        };

        _messagesPanel.Children.Add(messageBorder);
        if (_autoScroll) ScrollToBottom();
        return contentTextBlock;
    }

    private async void CreateWebPage_Click(object? sender, RoutedEventArgs e)
    {
        if (_isProcessing)
            return;

        if (_messageInput == null)
            return;

        var userPrompt = _messageInput.Text;
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            userPrompt = "Створи повну HTML-сторінку з базовою версткою (doctype, html, head, body) за власним сюжетом.";
        }

        _isProcessing = true;
        UpdateSendButtonState();

        // Додаємо в чат повідомлення користувача з позначкою режиму
        AddUserMessage(userPrompt + "\n\n[Режим: створення веб-сторінки]");

        // Очищаємо поле вводу
        _messageInput.Text = string.Empty;

        try
        {
            if (_aiService == null)
            {
                AddAssistantMessage("AI service is not initialized. Please wait a moment and try again.");
                return;
            }

            AddThinkingMessage();

            var languageIndex = _languageSelector?.SelectedIndex ?? 0;
            string? language = languageIndex switch
            {
                1 => "Ukrainian",
                2 => "English",
                3 => "Russian",
                4 => "German",
                5 => "French",
                6 => "Spanish",
                _ => null
            };

            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;

            // Промпт для генерації гарної HTML-сторінки зі стилями та markdown-контейнером коду
            var webPagePrompt = userPrompt +
                                "\n\nСтвори повну HTML5 веб-сторінку з такою структурою:" +
                                "\n- doctype, <html>, <head>, <body>." +
                                "\n- Використовуй семантичні теги (<header>, <main>, <section>, <footer> за потреби)." +
                                "\n- Для всього тексту використовуй теги <p> (або заголовки <h1>-<h3> там, де доречно)." +
                                "\n- Задай фон сторінки і колір тексту через inline-стилі або атрибути class (наприклад, світлий фон і темний текст)." +
                                "\n- Для ключових блоків додай 'властивості' у вигляді атрибутів class та data-* (наприклад, class='hero-section', data-section='features')." +
                                "\nПоверни результат у форматі Markdown з блоком коду, як у великих моделях (наприклад, Claude):" +
                                "\n```html" +
                                "\n...повний HTML-код сторінки..." +
                                "\n```" +
                                "\nПісля блоку коду додай коротке пояснення структури сторінки (2–4 речення звичайним текстом).";

            var htmlResponse = await _aiService.GenerateResponseAsync(
                webPagePrompt,
                language,
                ct);

            RemoveThinkingMessage();
            AddAssistantMessage(htmlResponse);
        }
        catch (OperationCanceledException)
        {
            RemoveThinkingMessage();
            AddAssistantMessage("Створення веб-сторінки було скасовано.");
        }
        catch (Exception ex)
        {
            RemoveThinkingMessage();
            AddAssistantMessage($"Помилка при створенні веб-сторінки: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            UpdateSendButtonState();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
