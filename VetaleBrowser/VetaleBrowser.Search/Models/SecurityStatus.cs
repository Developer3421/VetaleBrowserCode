namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// Статус безпеки веб-сайту
/// </summary>
public enum SecurityStatus
{
    /// <summary>Невідомий статус (перевірка не виконана)</summary>
    Unknown,
    
    /// <summary>Безпечний сайт</summary>
    Safe,
    
    /// <summary>Перевірка в процесі</summary>
    Checking,
    
    /// <summary>Небезпечний сайт (фішинг)</summary>
    Dangerous,
    
    /// <summary>Помилка перевірки</summary>
    Error
}

