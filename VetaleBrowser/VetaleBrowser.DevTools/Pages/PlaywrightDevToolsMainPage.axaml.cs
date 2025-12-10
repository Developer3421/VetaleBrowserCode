using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Diagnostics;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class PlaywrightDevToolsMainPage : UserControl
    {
        private ContentControl? _contentHost;
        private Button? _elementsTab;
        private Button? _performanceTab;
        private Button? _applicationTab;
        private Button? _sourcesTab;

        private PlaywrightElementsPage? _elementsPage;
        private PlaywrightPerformancePage? _performancePage;
        private PlaywrightApplicationPage? _applicationPage;
        private PlaywrightSourcesPage? _sourcesPage;

        public PlaywrightDevToolsMainPage()
        {
            InitializeComponent();
            InitializeControls();
            
            // Показати Elements за замовчуванням
            ShowElementsPage();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeControls()
        {
            _contentHost = this.FindControl<ContentControl>("ContentHost");
            _elementsTab = this.FindControl<Button>("ElementsTab");
            _performanceTab = this.FindControl<Button>("PerformanceTab");
            _applicationTab = this.FindControl<Button>("ApplicationTab");
            _sourcesTab = this.FindControl<Button>("SourcesTab");
        }

        private void OnTabClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string tag)
                return;

            ResetTabStyles();

            switch (tag)
            {
                case "elements":
                    ShowElementsPage();
                    HighlightTab(_elementsTab);
                    break;
                case "performance":
                    ShowPerformancePage();
                    HighlightTab(_performanceTab);
                    break;
                case "application":
                    ShowApplicationPage();
                    HighlightTab(_applicationTab);
                    break;
                case "sources":
                    ShowSourcesPage();
                    HighlightTab(_sourcesTab);
                    break;
            }

            Debug.WriteLine($"[PlaywrightDevToolsMainPage] Switched to {tag} tab");
        }

        private void ShowElementsPage()
        {
            if (_elementsPage == null)
                _elementsPage = new PlaywrightElementsPage();

            if (_contentHost != null)
                _contentHost.Content = _elementsPage;

            HighlightTab(_elementsTab);
        }

        private void ShowPerformancePage()
        {
            if (_performancePage == null)
                _performancePage = new PlaywrightPerformancePage();

            if (_contentHost != null)
                _contentHost.Content = _performancePage;
        }

        private void ShowApplicationPage()
        {
            if (_applicationPage == null)
                _applicationPage = new PlaywrightApplicationPage();

            if (_contentHost != null)
                _contentHost.Content = _applicationPage;
        }

        private void ShowSourcesPage()
        {
            if (_sourcesPage == null)
                _sourcesPage = new PlaywrightSourcesPage();

            if (_contentHost != null)
                _contentHost.Content = _sourcesPage;
        }

        private void ResetTabStyles()
        {
            var defaultBg = new SolidColorBrush(Color.Parse("#2D2D30"));
            var defaultFg = Brushes.White;

            if (_elementsTab != null)
            {
                _elementsTab.Background = defaultBg;
                _elementsTab.Foreground = defaultFg;
            }
            if (_performanceTab != null)
            {
                _performanceTab.Background = defaultBg;
                _performanceTab.Foreground = defaultFg;
            }
            if (_applicationTab != null)
            {
                _applicationTab.Background = defaultBg;
                _applicationTab.Foreground = defaultFg;
            }
            if (_sourcesTab != null)
            {
                _sourcesTab.Background = defaultBg;
                _sourcesTab.Foreground = defaultFg;
            }
        }

        private void HighlightTab(Button? tab)
        {
            if (tab == null) return;

            tab.Background = new SolidColorBrush(Color.Parse("#007ACC"));
            tab.Foreground = Brushes.White;
        }
    }
}

