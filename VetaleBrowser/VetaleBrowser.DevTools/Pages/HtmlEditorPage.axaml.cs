using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.Database.Models;
using System.Diagnostics;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class HtmlEditorPage : UserControl
    {
        private TextBox? _codeEditor;
        private Border? _previewContainer;
        private Border? _previewPanel;
        private TextBlock? _statusText;
        private TextBlock? _lineColText;
        private TextBlock? _charCountText;
        private TextBlock? _linesCountText;
        private string? _currentFilePath;
        private DispatcherTimer? _previewUpdateTimer;
        private DispatcherTimer? _autoSaveTimer;
        
        // Database service
        private readonly IDevToolsDataService _devToolsDataService;
        private string _currentSessionKey;
        private int _currentStateId;

        public HtmlEditorPage()
        {
            _devToolsDataService = new DevToolsDataService();
            _currentSessionKey = $"html-editor-{Guid.NewGuid()}";
            _currentStateId = 0;
            
            InitializeComponent();
            InitializeEditor();
            LoadLastSession();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeEditor()
        {
            _codeEditor = this.FindControl<TextBox>("CodeEditor");
            _previewContainer = this.FindControl<Border>("PreviewContainer");
            _previewPanel = this.FindControl<Border>("PreviewPanel");
            _statusText = this.FindControl<TextBlock>("StatusText");
            _lineColText = this.FindControl<TextBlock>("LineColText");
            _charCountText = this.FindControl<TextBlock>("CharCountText");
            _linesCountText = this.FindControl<TextBlock>("LinesCountText");

            // Setup preview update timer (debounce)
            _previewUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _previewUpdateTimer.Tick += (s, e) =>
            {
                _previewUpdateTimer.Stop();
                UpdatePreview();
            };

            // Setup auto-save timer (every 10 seconds)
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _autoSaveTimer.Tick += async (s, e) => await AutoSaveAsync();
            _autoSaveTimer.Start();

            // Load default template
            LoadTemplate(GetBlankTemplate());
        }

        private async void LoadLastSession()
        {
            try
            {
                var lastState = await _devToolsDataService.GetActiveHtmlEditorStateAsync();
                if (lastState != null)
                {
                    _currentStateId = lastState.Id;
                    _currentSessionKey = lastState.SessionKey;
                    _currentFilePath = lastState.FilePath;
                    
                    if (_codeEditor != null)
                        _codeEditor.Text = lastState.EncryptedContent; // Already decrypted by service
                    
                    if (_statusText != null)
                        _statusText.Text = "Loaded last session";
                    
                    Debug.WriteLine("[HtmlEditorPage] Loaded last session");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HtmlEditorPage] Error loading last session: {ex.Message}");
            }
        }

        private async Task AutoSaveAsync()
        {
            try
            {
                if (_codeEditor == null || string.IsNullOrWhiteSpace(_codeEditor.Text))
                    return;

                var state = new HtmlEditorState
                {
                    Id = _currentStateId,
                    SessionKey = _currentSessionKey,
                    EncryptedContent = _codeEditor.Text, // Will be encrypted by service
                    FileName = string.IsNullOrEmpty(_currentFilePath) 
                        ? "Untitled" 
                        : Path.GetFileName(_currentFilePath),
                    FilePath = _currentFilePath,
                    TemplateType = "Custom", // TODO: track template type
                    IsActive = true
                };

                await _devToolsDataService.SaveHtmlEditorStateAsync(state);
                
                if (_currentStateId == 0)
                {
                    // After first save, get the ID
                    var saved = await _devToolsDataService.GetActiveHtmlEditorStateAsync();
                    if (saved != null)
                        _currentStateId = saved.Id;
                }
                
                Debug.WriteLine("[HtmlEditorPage] Auto-saved");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HtmlEditorPage] Auto-save error: {ex.Message}");
            }
        }

        private void OnCodeChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateStatusBar();
            
            // Restart timer for preview update (debouncing)
            _previewUpdateTimer?.Stop();
            _previewUpdateTimer?.Start();
        }

        private void UpdateStatusBar()
        {
            if (_codeEditor == null) return;

            var text = _codeEditor.Text ?? "";
            var lines = text.Split('\n');
            var lineCount = lines.Length;
            var charCount = text.Length;

            if (_charCountText != null)
                _charCountText.Text = $"{charCount} characters";
            
            if (_linesCountText != null)
                _linesCountText.Text = $"{lineCount} lines";

            // Update line/col position (simplified - would need caret position for real impl)
            if (_lineColText != null)
                _lineColText.Text = "Ln 1, Col 1";
        }

        private void UpdatePreview()
        {
            // TODO: Implement CefSharp WebView for preview
            // For now, just update status
            if (_statusText != null)
                _statusText.Text = "Preview updated";
        }

        private void TogglePreview(object? sender, RoutedEventArgs e)
        {
            if (_previewPanel == null) return;

            var toggle = sender as ToggleButton;
            if (toggle?.IsChecked == true)
            {
                _previewPanel.IsVisible = true;
            }
            else
            {
                _previewPanel.IsVisible = false;
            }
        }

        #region File Operations

        private async void NewFile(object? sender, RoutedEventArgs e)
        {
            _currentFilePath = null;
            LoadTemplate(GetBlankTemplate());
            if (_statusText != null)
                _statusText.Text = "New file created";
        }

        private async void OpenFile(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open HTML File",
                Filters = new System.Collections.Generic.List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "HTML Files", Extensions = { "html", "htm" } },
                    new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                }
            };

            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null) return;

            var result = await dialog.ShowAsync(window);
            if (result != null && result.Length > 0)
            {
                var filePath = result[0];
                try
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    if (_codeEditor != null)
                        _codeEditor.Text = content;
                    
                    _currentFilePath = filePath;
                    if (_statusText != null)
                        _statusText.Text = $"Opened: {Path.GetFileName(filePath)}";
                }
                catch (Exception ex)
                {
                    if (_statusText != null)
                        _statusText.Text = $"Error: {ex.Message}";
                }
            }
        }

        private async void SaveFile(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                await SaveFileAs();
            }
            else
            {
                await SaveToFile(_currentFilePath);
            }
        }

        private async Task SaveFileAs()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save HTML File",
                Filters = new System.Collections.Generic.List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "HTML Files", Extensions = { "html", "htm" } },
                    new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                },
                DefaultExtension = "html"
            };

            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null) return;

            var result = await dialog.ShowAsync(window);
            if (!string.IsNullOrEmpty(result))
            {
                await SaveToFile(result);
            }
        }

        private async Task SaveToFile(string filePath)
        {
            try
            {
                var content = _codeEditor?.Text ?? "";
                await File.WriteAllTextAsync(filePath, content);
                _currentFilePath = filePath;
                if (_statusText != null)
                    _statusText.Text = $"Saved: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                if (_statusText != null)
                    _statusText.Text = $"Error saving: {ex.Message}";
            }
        }

        #endregion

        #region Edit Operations

        private void UndoAction(object? sender, RoutedEventArgs e)
        {
            // TODO: Implement undo
            if (_statusText != null)
                _statusText.Text = "Undo - Not implemented yet";
        }

        private void RedoAction(object? sender, RoutedEventArgs e)
        {
            // TODO: Implement redo
            if (_statusText != null)
                _statusText.Text = "Redo - Not implemented yet";
        }

        private void FormatCode(object? sender, RoutedEventArgs e)
        {
            // TODO: Implement HTML formatting
            if (_statusText != null)
                _statusText.Text = "Format Code - Not implemented yet";
        }

        private void ValidateHtml(object? sender, RoutedEventArgs e)
        {
            // TODO: Implement HTML validation
            if (_statusText != null)
                _statusText.Text = "Validate HTML - Not implemented yet";
        }

        #endregion

        #region Templates

        private void OnTemplateSelected(object? sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo?.SelectedItem is ComboBoxItem item)
            {
                var templateName = item.Content?.ToString() ?? "";
                var template = templateName switch
                {
                    "HTML5 Boilerplate" => GetHtml5Boilerplate(),
                    "Bootstrap Template" => GetBootstrapTemplate(),
                    "Responsive Layout" => GetResponsiveTemplate(),
                    _ => GetBlankTemplate()
                };
                LoadTemplate(template);
            }
        }

        private void LoadTemplate(string template)
        {
            if (_codeEditor != null)
                _codeEditor.Text = template;
        }

        private string GetBlankTemplate()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <title>New Document</title>
</head>
<body>
    
</body>
</html>";
        }

        private string GetHtml5Boilerplate()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>HTML5 Boilerplate</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        body {
            font-family: Arial, sans-serif;
            line-height: 1.6;
            padding: 20px;
        }
    </style>
</head>
<body>
    <header>
        <h1>Welcome to HTML5</h1>
    </header>
    <main>
        <p>Start building your website here...</p>
    </main>
    <footer>
        <p>&copy; 2025</p>
    </footer>
</body>
</html>";
        }

        private string GetBootstrapTemplate()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Bootstrap Template</title>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
<body>
    <div class=""container mt-5"">
        <div class=""row"">
            <div class=""col-12"">
                <h1 class=""display-4"">Bootstrap Template</h1>
                <p class=""lead"">Start building with Bootstrap!</p>
            </div>
        </div>
    </div>
    <script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js""></script>
</body>
</html>";
        }

        private string GetResponsiveTemplate()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Responsive Layout</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        body {
            font-family: Arial, sans-serif;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }
        @media (max-width: 768px) {
            .container {
                padding: 10px;
            }
        }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>Responsive Layout</h1>
        <p>This layout adapts to different screen sizes.</p>
    </div>
</body>
</html>";
        }

        #endregion
    }
}

