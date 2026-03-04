using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Interface for working with browser appearance settings
/// </summary>
public interface IAppearanceSettingsService
{
    // Tab Appearance Settings
    /// <summary>
    /// Gets the tab width
    /// </summary>
    Task<double> GetTabWidthAsync();
    
    /// <summary>
    /// Sets the tab width
    /// </summary>
    Task SetTabWidthAsync(double width);
    
    /// <summary>
    /// Gets the tab element size
    /// </summary>
    Task<double> GetTabElementSizeAsync();
    
    /// <summary>
    /// Sets the tab element size
    /// </summary>
    Task SetTabElementSizeAsync(double size);
    
    /// <summary>
    /// Gets the tab background color
    /// </summary>
    Task<string> GetTabBackgroundColorAsync();
    
    /// <summary>
    /// Sets the tab background color
    /// </summary>
    Task SetTabBackgroundColorAsync(string color);
    
    /// <summary>
    /// Gets the tab text color
    /// </summary>
    Task<string> GetTabTextColorAsync();
    
    /// <summary>
    /// Sets the tab text color
    /// </summary>
    Task SetTabTextColorAsync(string color);
    
    /// <summary>
    /// Gets the active tab color
    /// </summary>
    Task<string> GetTabActiveColorAsync();
    
    /// <summary>
    /// Sets the active tab color
    /// </summary>
    Task SetTabActiveColorAsync(string color);
    
    /// <summary>
    /// Gets the tab icon size
    /// </summary>
    Task<double> GetTabIconSizeAsync();
    
    /// <summary>
    /// Sets the tab icon size
    /// </summary>
    Task SetTabIconSizeAsync(double size);
    
    // Main Window Appearance Settings
    /// <summary>
    /// Gets the navigation bar color
    /// </summary>
    Task<string> GetNavigationBarColorAsync();
    
    /// <summary>
    /// Sets the navigation bar color
    /// </summary>
    Task SetNavigationBarColorAsync(string color);
    
    /// <summary>
    /// Gets the navigation bar height
    /// </summary>
    Task<double> GetNavigationBarHeightAsync();
    
    /// <summary>
    /// Sets the navigation bar height
    /// </summary>
    Task SetNavigationBarHeightAsync(double height);
    
    /// <summary>
    /// Gets the main window width
    /// </summary>
    Task<double> GetMainWindowWidthAsync();
    
    /// <summary>
    /// Sets the main window width
    /// </summary>
    Task SetMainWindowWidthAsync(double width);
    
    /// <summary>
    /// Gets the main window height
    /// </summary>
    Task<double> GetMainWindowHeightAsync();
    
    /// <summary>
    /// Sets the main window height
    /// </summary>
    Task SetMainWindowHeightAsync(double height);
    
    /// <summary>
    /// Gets the top bar background color
    /// </summary>
    Task<string> GetTopBarBackgroundColorAsync();
    
    /// <summary>
    /// Sets the top bar background color
    /// </summary>
    Task SetTopBarBackgroundColorAsync(string color);
    
    /// <summary>
    /// Gets the button size
    /// </summary>
    Task<double> GetButtonSizeAsync();
    
    /// <summary>
    /// Sets the button size
    /// </summary>
    Task SetButtonSizeAsync(double size);
    
    /// <summary>
    /// Gets the icon size inside buttons
    /// </summary>
    Task<double> GetButtonIconSizeAsync();
    
    /// <summary>
    /// Sets the icon size inside buttons
    /// </summary>
    Task SetButtonIconSizeAsync(double size);
    
    // Other Windows Appearance Settings
    /// <summary>
    /// Gets the background color of other windows
    /// </summary>
    Task<string> GetOtherWindowsBackgroundColorAsync();
    
    /// <summary>
    /// Sets the background color of other windows
    /// </summary>
    Task SetOtherWindowsBackgroundColorAsync(string color);
    
    /// <summary>
    /// Gets the top bar color of other windows
    /// </summary>
    Task<string> GetOtherWindowsTopBarColorAsync();
    
    /// <summary>
    /// Sets the top bar color of other windows
    /// </summary>
    Task SetOtherWindowsTopBarColorAsync(string color);
}

