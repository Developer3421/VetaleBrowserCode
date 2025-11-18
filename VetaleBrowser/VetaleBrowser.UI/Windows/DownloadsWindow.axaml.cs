using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.UI.Pages;

namespace VetaleBrowser.VetaleBrowser.UI.Windows
{
    public partial class DownloadsWindow : Window
    {
        private ContentControl? _contentHost;
        private DownloadsHistoryPage? _downloadsPage;

        public DownloadsWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnWindowClosing;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            _contentHost = this.FindControl<ContentControl>("PART_ContentHost");
            ShowDownloadsPage();
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Cleanup();
        }

        private void ShowDownloadsPage()
        {
            _downloadsPage = new DownloadsHistoryPage();
            if (_contentHost != null)
            {
                _contentHost.Content = _downloadsPage;
            }
        }

        private void Cleanup()
        {
            Loaded -= OnLoaded;
            Closing -= OnWindowClosing;
            if (_contentHost != null)
                _contentHost.Content = null;
            _downloadsPage = null;
            _contentHost = null;
        }

        private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void TopBar_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            // Максимізація для додаткових вікон не використовується
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
            var lifetime = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            if (lifetime == null) return;
            foreach (var window in lifetime.Windows)
            {
                if (window is MainWindow main)
                {
                    main.Activate();
                    return;
                }
            }
        }
    }
}
