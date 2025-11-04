using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.DevTools.Pages;
using System.Linq;

namespace VetaleBrowser.VetaleBrowser.UI.Windows
{
    public partial class DevToolsWindow : Window
    {
        private readonly ContentControl? _contentHost;
        private Button? _currentActiveTab;
        private static ConsoleWindow? _consoleWindow;

        public DevToolsWindow()
        {
            InitializeComponent();
            _contentHost = this.FindControl<ContentControl>("PART_ContentHost");
            _currentActiveTab = this.FindControl<Button>("TabMain");
            
            // Show main page by default
            ShowMainPage(null, null!);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #region Window Controls

        private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void TopBar_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }

        private void MinimizeWindow(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseWindow(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenMainWindow(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.Windows.FirstOrDefault(w => w is MainWindow);
                if (mainWindow != null)
                {
                    mainWindow.Activate();
                }
            }
        }

        #endregion

        #region Tab Navigation

        private void SetActiveTab(Button? tabButton)
        {
            // Remove active class from previous tab
            if (_currentActiveTab != null && _currentActiveTab.Classes.Contains("active"))
            {
                _currentActiveTab.Classes.Remove("active");
            }

            // Add active class to new tab
            if (tabButton != null && !tabButton.Classes.Contains("active"))
            {
                tabButton.Classes.Add("active");
            }

            _currentActiveTab = tabButton;
        }

        private void ShowMainPage(object? sender, RoutedEventArgs e)
        {
            if (_contentHost != null)
            {
                _contentHost.Content = new DevToolsMainPage();
                SetActiveTab(this.FindControl<Button>("TabMain"));
            }
        }

        private void ShowElementsPage(object? sender, RoutedEventArgs e)
        {
            if (_contentHost != null)
            {
                _contentHost.Content = new ElementsPage();
                SetActiveTab(this.FindControl<Button>("TabElements"));
            }
        }

        private void ShowNetworkPage(object? sender, RoutedEventArgs e)
        {
            if (_contentHost != null)
            {
                _contentHost.Content = new NetworkPage();
                SetActiveTab(this.FindControl<Button>("TabNetwork"));
            }
        }

        private void ShowSourcesPage(object? sender, RoutedEventArgs e)
        {
            if (_contentHost != null)
            {
                _contentHost.Content = new SourcesPage();
                SetActiveTab(this.FindControl<Button>("TabSources"));
            }
        }

        private void ShowHtmlEditorPage(object? sender, RoutedEventArgs e)
        {
            if (_contentHost != null)
            {
                _contentHost.Content = new HtmlEditorPage();
                SetActiveTab(this.FindControl<Button>("TabHtmlEditor"));
            }
        }

        private void ShowApplicationPage(object? sender, RoutedEventArgs e)
        {
            if (_contentHost != null)
            {
                _contentHost.Content = new ApplicationPage();
                SetActiveTab(this.FindControl<Button>("TabApplication"));
            }
        }

        private void ShowPerformancePage(object? sender, RoutedEventArgs e)
        {
            if (_contentHost != null)
            {
                _contentHost.Content = new PerformancePage();
                SetActiveTab(this.FindControl<Button>("TabPerformance"));
            }
        }

        private void ShowWebViewWorkerPage(object? sender, RoutedEventArgs e)
        {
            if (_contentHost != null)
            {
                _contentHost.Content = new WebViewWorkerPage();
                SetActiveTab(this.FindControl<Button>("TabWebViewWorker"));
            }
        }

        private void ShowConsoleWindow(object? sender, RoutedEventArgs e)
        {
            // Open Console as separate window (already exists)
            if (_consoleWindow == null || !_consoleWindow.IsVisible)
            {
                _consoleWindow = new ConsoleWindow();
                _consoleWindow.Closed += (s, args) => _consoleWindow = null;
                _consoleWindow.Show();
            }
            else
            {
                _consoleWindow.Activate();
            }

            // Don't change active tab for console since it's a separate window
        }

        #endregion
    }
}

