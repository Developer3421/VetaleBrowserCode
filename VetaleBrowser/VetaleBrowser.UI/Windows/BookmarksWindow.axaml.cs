using System;
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
            // If a URL is provided, show the add page
            ShowAddBookmarkPage(_initialUrl, _initialTitle ?? string.Empty);
        }
        else
        {
            // Otherwise show the bookmarks list
            ShowBookmarksPage();
        }
    }

    private void ShowAddBookmarkPage(string url, string title)
    {
        var addBookmarkPage = new AddBookmarkPage(url, title);
        
        addBookmarkPage.BookmarkSaved += (s, e) =>
        {
            ShowBookmarksPage();
            // Reload the bookmarks list
            _bookmarksPage?.RefreshBookmarks();
        };
        
        addBookmarkPage.Cancelled += (s, e) =>
        {
            ShowBookmarksPage();
        };
        
        if (_contentHost != null)
        {
            _contentHost.Content = addBookmarkPage;
            
            // Reduce window size for the add form
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
        
        // Subscribe to the add bookmark request event
        _bookmarksPage.AddBookmarkRequested += OnAddBookmarkRequested;
        
        if (_contentHost != null)
        {
            _contentHost.Content = _bookmarksPage;
            
            // Enlarge window size for the bookmarks list
            Width = 900;
            Height = 700;
            MinWidth = 700;
            MinHeight = 500;
        }
    }
    
    /// <summary>
    /// Handler for add bookmark request from BookmarksPage
    /// </summary>
    private void OnAddBookmarkRequested(object? sender, EventArgs e)
    {
        // Show the add bookmark form in the same window
        ShowAddBookmarkPage(string.Empty, string.Empty);
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
        // Find the main window
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

