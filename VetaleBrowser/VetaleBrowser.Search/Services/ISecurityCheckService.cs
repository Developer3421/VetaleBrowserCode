using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Інтерфейс сервісу перевірки безпеки URL
/// </summary>
public interface ISecurityCheckService
{
    /// <summary>
    /// Перевірити безпеку URL
    /// </summary>
    /// <param name="url">URL для перевірки</param>
    /// <returns>Результат перевірки безпеки</returns>
    Task<SecurityCheckResult> CheckUrlAsync(string url);
    
    /// <summary>
    /// Очистити кеш перевірок
    /// </summary>
    void ClearCache();
}

