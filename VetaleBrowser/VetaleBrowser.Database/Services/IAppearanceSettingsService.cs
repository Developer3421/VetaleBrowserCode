using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Інтерфейс для роботи з налаштуваннями вигляду браузера
/// </summary>
public interface IAppearanceSettingsService
{
    // Tab Appearance Settings
    /// <summary>
    /// Отримує ширину вкладки
    /// </summary>
    Task<double> GetTabWidthAsync();
    
    /// <summary>
    /// Встановлює ширину вкладки
    /// </summary>
    Task SetTabWidthAsync(double width);
    
    /// <summary>
    /// Отримує розмір елементів вкладки
    /// </summary>
    Task<double> GetTabElementSizeAsync();
    
    /// <summary>
    /// Встановлює розмір елементів вкладки
    /// </summary>
    Task SetTabElementSizeAsync(double size);
    
    /// <summary>
    /// Отримує колір фону вкладки
    /// </summary>
    Task<string> GetTabBackgroundColorAsync();
    
    /// <summary>
    /// Встановлює колір фону вкладки
    /// </summary>
    Task SetTabBackgroundColorAsync(string color);
    
    /// <summary>
    /// Отримує колір тексту вкладки
    /// </summary>
    Task<string> GetTabTextColorAsync();
    
    /// <summary>
    /// Встановлює колір тексту вкладки
    /// </summary>
    Task SetTabTextColorAsync(string color);
    
    /// <summary>
    /// Отримує колір активної вкладки
    /// </summary>
    Task<string> GetTabActiveColorAsync();
    
    /// <summary>
    /// Встановлює колір активної вкладки
    /// </summary>
    Task SetTabActiveColorAsync(string color);
    
    /// <summary>
    /// Отримує розмір іконки вкладки
    /// </summary>
    Task<double> GetTabIconSizeAsync();
    
    /// <summary>
    /// Встановлює розмір іконки вкладки
    /// </summary>
    Task SetTabIconSizeAsync(double size);
    
    // Main Window Appearance Settings
    /// <summary>
    /// Отримує колір навігаційного бару
    /// </summary>
    Task<string> GetNavigationBarColorAsync();
    
    /// <summary>
    /// Встановлює колір навігаційного бару
    /// </summary>
    Task SetNavigationBarColorAsync(string color);
    
    /// <summary>
    /// Отримує висоту навігаційного бару
    /// </summary>
    Task<double> GetNavigationBarHeightAsync();
    
    /// <summary>
    /// Встановлює висоту навігаційного бару
    /// </summary>
    Task SetNavigationBarHeightAsync(double height);
    
    /// <summary>
    /// Отримує ширину головного вікна
    /// </summary>
    Task<double> GetMainWindowWidthAsync();
    
    /// <summary>
    /// Встановлює ширину головного вікна
    /// </summary>
    Task SetMainWindowWidthAsync(double width);
    
    /// <summary>
    /// Отримує висоту головного вікна
    /// </summary>
    Task<double> GetMainWindowHeightAsync();
    
    /// <summary>
    /// Встановлює висоту головного вікна
    /// </summary>
    Task SetMainWindowHeightAsync(double height);
    
    /// <summary>
    /// Отримує колір фону верхньої лінії
    /// </summary>
    Task<string> GetTopBarBackgroundColorAsync();
    
    /// <summary>
    /// Встановлює колір фону верхньої лінії
    /// </summary>
    Task SetTopBarBackgroundColorAsync(string color);
    
    /// <summary>
    /// Отримує розмір кнопок
    /// </summary>
    Task<double> GetButtonSizeAsync();
    
    /// <summary>
    /// Встановлює розмір кнопок
    /// </summary>
    Task SetButtonSizeAsync(double size);
    
    /// <summary>
    /// Отримує розмір зображень всередині кнопок
    /// </summary>
    Task<double> GetButtonIconSizeAsync();
    
    /// <summary>
    /// Встановлює розмір зображень всередині кнопок
    /// </summary>
    Task SetButtonIconSizeAsync(double size);
    
    // Other Windows Appearance Settings
    /// <summary>
    /// Отримує колір фону інших вікон
    /// </summary>
    Task<string> GetOtherWindowsBackgroundColorAsync();
    
    /// <summary>
    /// Встановлює колір фону інших вікон
    /// </summary>
    Task SetOtherWindowsBackgroundColorAsync(string color);
    
    /// <summary>
    /// Отримує колір верхньої панелі інших вікон
    /// </summary>
    Task<string> GetOtherWindowsTopBarColorAsync();
    
    /// <summary>
    /// Встановлює колір верхньої панелі інших вікон
    /// </summary>
    Task SetOtherWindowsTopBarColorAsync(string color);
}

