using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class AddBookmarkWindow : Window
{
    private TextBox? _nameTextBox;
    private TextBox? _urlTextBox;
    private ComboBox? _folderComboBox;

    public string BookmarkName { get; private set; } = string.Empty;
    public string BookmarkUrl { get; private set; } = string.Empty;
    public string BookmarkFolder { get; private set; } = "Закладки";
    public bool IsSaved { get; private set; }

    public AddBookmarkWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public AddBookmarkWindow(string url, string title = "") : this()
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

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        IsSaved = false;
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        IsSaved = false;
        Close();
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

            IsSaved = true;
            Close();
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
