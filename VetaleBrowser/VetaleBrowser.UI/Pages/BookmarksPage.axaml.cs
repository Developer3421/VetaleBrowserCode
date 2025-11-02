using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class BookmarksPage : UserControl
{
    private ItemsControl? _bookmarksItemsControl;
    private ITabDatabaseService? _databaseService;
    private string? _currentFolder;

    public BookmarksPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _bookmarksItemsControl = this.FindControl<ItemsControl>("PART_BookmarksItemsControl");
        LoadBookmarks();
    }

    public void SetDatabaseService(ITabDatabaseService databaseService)
    {
        _databaseService = databaseService;
        LoadBookmarks();
    }

    private void LoadBookmarks()
    {
        if (_bookmarksItemsControl == null || _databaseService == null)
            return;

        try
        {
            var bookmarks = _databaseService.GetBookmarks(_currentFolder);
            _bookmarksItemsControl.ItemsSource = bookmarks;
        }
        catch (Exception ex)
        {
            // TODO: Show error message
            Console.WriteLine($"Error loading bookmarks: {ex.Message}");
        }
    }

    private void AddBookmark_Click(object? sender, RoutedEventArgs e)
    {
        var window = new Windows.AddBookmarkWindow("", "");
        window.ShowDialog(GetParentWindow());

        if (window.IsSaved && _databaseService != null)
        {
            try
            {
                _databaseService.AddBookmark(
                    window.BookmarkUrl,
                    window.BookmarkName,
                    window.BookmarkFolder
                );
                LoadBookmarks();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding bookmark: {ex.Message}");
            }
        }
    }

    private void FolderFilter_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            // Update selected folder
            _currentFolder = button.Tag?.ToString();
            
            // Update button styles
            var parent = button.Parent as StackPanel;
            if (parent != null)
            {
                foreach (var child in parent.Children)
                {
                    if (child is Button btn)
                    {
                        btn.Background = btn == button 
                            ? Brush.Parse("#9A1CE8")
                            : Brush.Parse("#E0E0E0");
                        btn.Foreground = btn == button
                            ? Brushes.White
                            : Brush.Parse("#333333");
                    }
                }
            }

            LoadBookmarks();
        }
    }

    private void OpenBookmark_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Bookmark bookmark)
        {
            try
            {
                // Find the main window and navigate to the bookmark URL
                var appLifetime = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                if (appLifetime != null)
                {
                    foreach (var window in appLifetime.Windows)
                    {
                        if (window is MainWindow mainWindow)
                        {
                            // Navigate to the bookmark URL in the active tab
                            mainWindow.NavigateUrlInActiveTab(bookmark.Url);
                            
                            // Activate the main window
                            mainWindow.Activate();
                            
                            // Close the bookmarks window
                            GetParentWindow()?.Close();
                            
                            System.Diagnostics.Debug.WriteLine($"[BookmarksPage] Navigating to: {bookmark.Url}");
                            return;
                        }
                    }
                }
                
                Console.WriteLine($"[BookmarksPage] Could not find MainWindow to navigate to: {bookmark.Url}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BookmarksPage] Error opening bookmark: {ex.Message}");
            }
        }
    }

    private void DeleteBookmark_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Bookmark bookmark && _databaseService != null)
        {
            try
            {
                _databaseService.DeleteBookmark(bookmark.Id);
                LoadBookmarks();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting bookmark: {ex.Message}");
            }
        }
    }

    private Window GetParentWindow()
    {
        var parent = this.Parent;
        while (parent != null)
        {
            if (parent is Window window)
                return window;
            parent = parent.Parent;
        }
        throw new InvalidOperationException("Could not find parent window");
    }
}

