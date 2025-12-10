using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class BookmarksWindow : Window
{
    private ContentControl? _contentHost;
    private BookmarksPage? _bookmarksPage;
    private string? _initialUrl;
    private string? _initialTitle;

    public BookmarksWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public BookmarksWindow(string url, string title) : this()
    {
        _initialUrl = url;
        _initialTitle = title;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _contentHost = this.FindControl<ContentControl>("PART_ContentHost");
        
        if (!string.IsNullOrEmpty(_initialUrl))
        {
            // Якщо передано URL, показуємо сторінку додавання
            ShowAddBookmarkPage(_initialUrl, _initialTitle ?? string.Empty);
        }
        else
        {
            // Інакше показуємо список закладок
            ShowBookmarksPage();
        }
    }

    private void ShowAddBookmarkPage(string url, string title)
    {
        var addBookmarkPage = new AddBookmarkPage(url, title);
        
        addBookmarkPage.BookmarkSaved += (s, e) =>
        {
            ShowBookmarksPage();
        };
        
        addBookmarkPage.Cancelled += (s, e) =>
        {
            ShowBookmarksPage();
        };
        
        if (_contentHost != null)
        {
            _contentHost.Content = addBookmarkPage;
            
            // Зменшуємо розмір вікна для форми додавання
            Width = 600;
            Height = 480;
            MinWidth = 600;
            MinHeight = 480;
        }
    }

    public void ShowBookmarksPage()
    {
        _bookmarksPage = new BookmarksPage();
        _bookmarksPage.SetDatabaseService(DatabaseManager.Instance);
        
        if (_contentHost != null)
        {
            _contentHost.Content = _bookmarksPage;
            
            // Збільшуємо розмір вікна для списку закладок
            Width = 900;
            Height = 700;
            MinWidth = 700;
            MinHeight = 500;
        }
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void TopBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // Maximize disabled for secondary windows
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenMainWindow(object? sender, RoutedEventArgs e)
    {
        // Знаходимо головне вікно
        foreach (var window in ((Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            Avalonia.Application.Current!.ApplicationLifetime!).Windows)
        {
            if (window is MainWindow mainWindow)
            {
                mainWindow.Activate();
                return;
            }
        }
    }
}

