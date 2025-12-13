using System;

namespace VetaleBrowser.VetaleBrowser.Database.Models;

/// <summary>
/// Модель повідомлення в чаті Vetale AI
/// </summary>
public class VetaleAIChatMessage
{
    public int Id { get; set; }
    
    /// <summary>
    /// Роль: "user" або "assistant"
    /// </summary>
    public string Role { get; set; } = string.Empty;
    
    /// <summary>
    /// Текст повідомлення (зашифрований)
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Дата та час створення повідомлення
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// ID сесії чату (для групування повідомлень)
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Токени використані для генерації (опціонально)
    /// </summary>
    public int? TokensUsed { get; set; }
}

