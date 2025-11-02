using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Інтерфейс для роботи з налаштуваннями браузера
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Отримує URL пошукової системи
    /// </summary>
    Task<string> GetSearchEngineUrlAsync();
    
    /// <summary>
    /// Встановлює URL пошукової системи
    /// </summary>
    Task SetSearchEngineUrlAsync(string url);
    
    /// <summary>
    /// Отримує назву пошукової системи
    /// </summary>
    Task<string> GetSearchEngineNameAsync();
    
    /// <summary>
    /// Встановлює назву пошукової системи
    /// </summary>
    Task SetSearchEngineNameAsync(string name);
    
    /// <summary>
    /// Встановлює пошукову систему (назва + URL)
    /// </summary>
    Task SetSearchEngineAsync(string name, string url);
}

