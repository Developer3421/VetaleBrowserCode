using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace VetaleBrowser.VetaleBrowser.UI.Еlements;

public class Tab : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<Tab, string>(nameof(Title), "New Tab");

    public static readonly StyledProperty<string> IconPathProperty =
        AvaloniaProperty.Register<Tab, string>(nameof(IconPath), "");

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<Tab, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsCloseButtonVisibleProperty =
        AvaloniaProperty.Register<Tab, bool>(nameof(IsCloseButtonVisible), true);

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<Tab, bool>(nameof(IsMuted), false);

    // Added: Favicon image source to be shown in the template
    public static readonly StyledProperty<IImage?> FaviconSourceProperty =
        AvaloniaProperty.Register<Tab, IImage?>(nameof(FaviconSource));

    // Events for interaction
    public event System.EventHandler? Clicked;
    public event System.EventHandler? CloseRequested;
    public event System.EventHandler? MuteToggled;

    private Border? _border;
    private Button? _closeButton;
    private Button? _muteButton;

    static Tab()
    {
        IsActiveProperty.Changed.AddClassHandler<Tab>((tab, _) =>
        {
            tab.PseudoClasses.Set(":active", tab.IsActive);
        });
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_border != null)
        {
            _border.PointerPressed -= OnBorderPointerPressed;
        }
        if (_closeButton != null)
        {
            _closeButton.Click -= OnCloseButtonClick;
        }
        if (_muteButton != null)
        {
            _muteButton.Click -= OnMuteButtonClick;
        }

        _border = e.NameScope.Find<Border>("PART_Border");
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");
        _muteButton = e.NameScope.Find<Button>("PART_MuteButton");

        if (_border != null)
        {
            _border.PointerPressed += OnBorderPointerPressed;
        }
        if (_closeButton != null)
        {
            _closeButton.Click += OnCloseButtonClick;
        }
        if (_muteButton != null)
        {
            _muteButton.Click += OnMuteButtonClick;
        }
    }

    private void OnBorderPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        Clicked?.Invoke(this, System.EventArgs.Empty);
    }

    private void OnCloseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, System.EventArgs.Empty);
        e.Handled = true;
    }

    private void OnMuteButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MuteToggled?.Invoke(this, System.EventArgs.Empty);
        e.Handled = true;
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string IconPath
    {
        get => GetValue(IconPathProperty);
        set => SetValue(IconPathProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsCloseButtonVisible
    {
        get => GetValue(IsCloseButtonVisibleProperty);
        set => SetValue(IsCloseButtonVisibleProperty, value);
    }

    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    // Added: property wrapper for favicon image
    public IImage? FaviconSource
    {
        get => GetValue(FaviconSourceProperty);
        set => SetValue(FaviconSourceProperty, value);
    }
}