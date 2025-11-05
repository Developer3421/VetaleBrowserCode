using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.Text.Json;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.DevTools.Services;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    // DevTools Performance Page using Playwright
    public partial class PlaywrightPerformancePage : UserControl
    {
        private TextBlock? _loadTimeText;
        private TextBlock? _domLoadTimeText;
        private TextBlock? _firstPaintText;
        private TextBlock? _memoryText;
        private ListBox? _resourceTimingsList;
        private StackPanel? _webVitalsPanel;
        private Button? _btnCapturePerformance;
        private Button? _btnGetWebVitals;

        private readonly WebViewWorkerService _webViewService;
        private readonly PlaywrightDevToolsService _playwrightService;

        public PlaywrightPerformancePage()
        {
            // Use singleton instances to prevent multiple Chromium windows
            _webViewService = WebViewWorkerService.GetInstance(new DevToolsDataService());
            _playwrightService = PlaywrightDevToolsService.GetInstance(new DevToolsDataService());
            _playwrightService.PerformanceSnapshotCaptured += OnPerformanceSnapshotCaptured;

            InitializeComponent();
            InitializeControls();
            AttachToLocalWebView();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeControls()
        {
            _loadTimeText = this.FindControl<TextBlock>("LoadTimeText");
            _domLoadTimeText = this.FindControl<TextBlock>("DomLoadTimeText");
            _firstPaintText = this.FindControl<TextBlock>("FirstPaintText");
            _memoryText = this.FindControl<TextBlock>("MemoryText");
            _resourceTimingsList = this.FindControl<ListBox>("ResourceTimingsList");
            _webVitalsPanel = this.FindControl<StackPanel>("WebVitalsPanel");
            _btnCapturePerformance = this.FindControl<Button>("CapturePerformanceButton");
            _btnGetWebVitals = this.FindControl<Button>("GetWebVitalsButton");

            if (_btnCapturePerformance != null) _btnCapturePerformance.Click += OnCapturePerformance;
            if (_btnGetWebVitals != null) _btnGetWebVitals.Click += OnGetWebVitals;
        }

        private void AttachToLocalWebView()
        {
            // No longer needed - service automatically uses current tab URL
            Debug.WriteLine("[PlaywrightPerformancePage] Using current tab URL automatically via WebViewWorkerService");
        }

        private async void OnCapturePerformance(object? sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var originalContent = button?.Content;
            
            try
            {
                // Show loading state
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "⏳ Analyzing...";
                }
                
                // Clear previous data
                ClearPerformanceData();
                
                // Automatically sync with current active tab
                _webViewService?.SyncWithMainWindow();
                
                // Get URL from WebView Worker
                var url = _webViewService.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[PlaywrightPerformancePage] No URL available from current tab");
                    ShowMessage("No active tab found. Please open a webpage first.");
                    return;
                }

                Debug.WriteLine($"[PlaywrightPerformancePage] Capturing performance from URL: {url} (headless Playwright)");
                
                // Use Playwright to analyze the URL
                await _playwrightService.NavigateAsync(url);
                var snapshot = await _playwrightService.CapturePerformanceSnapshotAsync();
                
                if (snapshot != null)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        DisplaySnapshot(snapshot);
                    });
                    ShowMessage($"✓ Performance analysis complete for {url}");
                }
                else
                {
                    ShowMessage("⚠ No performance data captured");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightPerformancePage] Error capturing performance: {ex.Message}");
                ShowMessage($"❌ Error: {ex.Message}");
            }
            finally
            {
                // Restore button state
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = originalContent;
                }
            }
        }
        
        private void ClearPerformanceData()
        {
            if (_loadTimeText != null) _loadTimeText.Text = "---";
            if (_domLoadTimeText != null) _domLoadTimeText.Text = "---";
            if (_firstPaintText != null) _firstPaintText.Text = "---";
            if (_memoryText != null) _memoryText.Text = "---";
            if (_resourceTimingsList != null) _resourceTimingsList.Items.Clear();
        }
        
        private void ShowMessage(string message)
        {
            Debug.WriteLine($"[PlaywrightPerformancePage] {message}");
        }

        private async void OnGetWebVitals(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Get URL from WebView Worker
                var url = _webViewService.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[PlaywrightPerformancePage] No URL available from WebView Worker");
                    return;
                }

                
                // Use Playwright to get web vitals
                await _playwrightService.NavigateAsync(url);
                var vitals = await _playwrightService.GetCoreWebVitalsAsync();
                
                if (vitals != null && vitals.Count > 0)
                {
                    DisplayWebVitals(vitals);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightPerformancePage] Error getting web vitals: {ex.Message}");
            }
        }

        private void DisplaySnapshot(PerformanceSnapshot snapshot)
        {
            if (_loadTimeText != null)
                _loadTimeText.Text = $"{snapshot.LoadTime} ms";

            if (_domLoadTimeText != null)
                _domLoadTimeText.Text = $"{snapshot.DomContentLoadedTime} ms";

            if (_firstPaintText != null)
                _firstPaintText.Text = $"{snapshot.FirstPaintTime:F2} ms";

            if (_memoryText != null)
            {
                var memoryMb = snapshot.MemoryUsed / (1024.0 * 1024.0);
                _memoryText.Text = $"{memoryMb:F2} MB";
            }

            // Resource timings
            if (_resourceTimingsList != null)
            {
                _resourceTimingsList.Items.Clear();
                if (!string.IsNullOrWhiteSpace(snapshot.ResourceTimings))
                {
                    try
                    {
                        var resources = JsonSerializer.Deserialize<JsonElement>(snapshot.ResourceTimings);
                        if (resources.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var resource in resources.EnumerateArray())
                            {
                                var name = resource.TryGetProperty("name", out var n) ? n.GetString() : "";
                                var duration = resource.TryGetProperty("duration", out var d) ? d.GetDouble() : 0;
                                var type = resource.TryGetProperty("type", out var t) ? t.GetString() : "";
                                
                                var shortName = name?.Length > 50 ? "..." + name.Substring(name.Length - 50) : name;
                                _resourceTimingsList.Items.Add($"{type}: {shortName} ({duration:F2}ms)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PlaywrightPerformancePage] Error parsing resource timings: {ex.Message}");
                    }
                }
            }
        }

        private void DisplayWebVitals(System.Collections.Generic.Dictionary<string, double> vitals)
        {
            if (_webVitalsPanel == null) return;

            _webVitalsPanel.Children.Clear();

            foreach (var vital in vitals)
            {
                var border = new Border
                {
                    BorderBrush = Avalonia.Media.Brushes.Gray,
                    BorderThickness = new Avalonia.Thickness(1),
                    Padding = new Avalonia.Thickness(10),
                    Margin = new Avalonia.Thickness(0, 5, 0, 0)
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = vital.Key, FontWeight = Avalonia.Media.FontWeight.Bold });
                
                var valueText = new TextBlock 
                { 
                    Text = $"{vital.Value:F2} ms",
                    FontSize = 18,
                    Margin = new Avalonia.Thickness(0, 5, 0, 0)
                };

                // Визначаємо колір на основі значень
                if (vital.Key == "LCP")
                {
                    valueText.Foreground = vital.Value < 2500 ? Avalonia.Media.Brushes.Green :
                                          vital.Value < 4000 ? Avalonia.Media.Brushes.Orange :
                                          Avalonia.Media.Brushes.Red;
                }
                else if (vital.Key == "FID")
                {
                    valueText.Foreground = vital.Value < 100 ? Avalonia.Media.Brushes.Green :
                                          vital.Value < 300 ? Avalonia.Media.Brushes.Orange :
                                          Avalonia.Media.Brushes.Red;
                }
                else if (vital.Key == "CLS")
                {
                    valueText.Foreground = vital.Value < 0.1 ? Avalonia.Media.Brushes.Green :
                                          vital.Value < 0.25 ? Avalonia.Media.Brushes.Orange :
                                          Avalonia.Media.Brushes.Red;
                }

                stack.Children.Add(valueText);
                border.Child = stack;
                _webVitalsPanel.Children.Add(border);
            }
        }

        private void OnPerformanceSnapshotCaptured(object? sender, PerformanceSnapshot snapshot)
        {
            Debug.WriteLine($"[PlaywrightPerformancePage] Performance snapshot captured");
        }
    }
}
