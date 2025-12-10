using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.UI.Windows;
using Avalonia.Interactivity;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class DevToolsMainPage : UserControl
    {
        public DevToolsMainPage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private DevToolsWindow? GetParentWindow()
        {
            return this.VisualRoot as DevToolsWindow;
        }

        private void OpenElementsPage(object? sender, PointerPressedEventArgs e)
        {
            var window = GetParentWindow();
            var tabButton = window?.FindControl<Button>("TabElements");
            if (tabButton != null)
            {
                // Simulate tab button click
                tabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void OpenConsole(object? sender, PointerPressedEventArgs e)
        {
            var window = GetParentWindow();
            var tabButton = window?.FindControl<Button>("TabConsole");
            if (tabButton != null)
            {
                tabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void OpenNetworkPage(object? sender, PointerPressedEventArgs e)
        {
            var window = GetParentWindow();
            var tabButton = window?.FindControl<Button>("TabNetwork");
            if (tabButton != null)
            {
                tabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void OpenSourcesPage(object? sender, PointerPressedEventArgs e)
        {
            var window = GetParentWindow();
            var tabButton = window?.FindControl<Button>("TabSources");
            if (tabButton != null)
            {
                tabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void OpenHtmlEditorPage(object? sender, PointerPressedEventArgs e)
        {
            var window = GetParentWindow();
            var tabButton = window?.FindControl<Button>("TabHtmlEditor");
            if (tabButton != null)
            {
                tabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void OpenApplicationPage(object? sender, PointerPressedEventArgs e)
        {
            var window = GetParentWindow();
            var tabButton = window?.FindControl<Button>("TabApplication");
            if (tabButton != null)
            {
                tabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void OpenPerformancePage(object? sender, PointerPressedEventArgs e)
        {
            var window = GetParentWindow();
            var tabButton = window?.FindControl<Button>("TabPerformance");
            if (tabButton != null)
            {
                tabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }
    }
}

