using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class AddBookmarkPage : UserControl
{
    private TextBox? _nameTextBox;
    private TextBox? _urlTextBox;
    private ComboBox? _folderComboBox;

    public string BookmarkName { get; private set; } = string.Empty;
    public string BookmarkUrl { get; private set; } = string.Empty;
    public string BookmarkFolder { get; private set; } = "Закладки";
    public bool IsSaved { get; private set; }

    public event EventHandler? BookmarkSaved;
    public event EventHandler? Cancelled;

    public AddBookmarkPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public AddBookmarkPage(string url, string title = "") : this()
    {
        BookmarkUrl = url;
        BookmarkName = title;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _nameTextBox = this.FindControl<TextBox>("PART_NameTextBox");
        _urlTextBox = this.FindControl<TextBox>("PART_UrlTextBox");
        _folderComboBox = this.FindControl<ComboBox>("PART_FolderComboBox");

        // Set initial values
        if (_nameTextBox != null)
        {
            _nameTextBox.Text = BookmarkName;
            _nameTextBox.Focus();
            _nameTextBox.SelectAll();
        }

        if (_urlTextBox != null)
        {
            _urlTextBox.Text = BookmarkUrl;
        }

        // Enable Enter key to save
        if (_nameTextBox != null)
        {
            _nameTextBox.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    SaveBookmark();
                }
            };
        }
    }

    public void SetBookmarkData(string url, string title)
    {
        BookmarkUrl = url;
        BookmarkName = title;

        if (_nameTextBox != null)
            _nameTextBox.Text = title;
        if (_urlTextBox != null)
            _urlTextBox.Text = url;
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        IsSaved = false;
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        SaveBookmark();
    }

    private void SaveBookmark()
    {
        if (_nameTextBox != null && !string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            BookmarkName = _nameTextBox.Text;
            BookmarkUrl = _urlTextBox?.Text ?? string.Empty;
            
            if (_folderComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                BookmarkFolder = selectedItem.Content?.ToString() ?? "Закладки";
            }

            try
            {
                // Зберігаємо закладку в базу даних
                DatabaseManager.Instance.AddBookmark(
                    BookmarkUrl,
                    BookmarkName,
                    BookmarkFolder
                );

                IsSaved = true;
                BookmarkSaved?.Invoke(this, EventArgs.Empty);
                
                System.Diagnostics.Debug.WriteLine($"Bookmark saved: {BookmarkName} - {BookmarkUrl} in {BookmarkFolder}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving bookmark: {ex.Message}");
                // TODO: Show error message to user
            }
        }
        else
        {
            // Show validation message
            if (_nameTextBox != null)
            {
                _nameTextBox.Focus();
            }
        }
    }
}

