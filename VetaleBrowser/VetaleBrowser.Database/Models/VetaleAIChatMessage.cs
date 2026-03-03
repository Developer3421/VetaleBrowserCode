using System;

namespace VetaleBrowser.VetaleBrowser.Database.Models;

/// <summary>
/// Vetale AI chat message model
/// </summary>
public class VetaleAIChatMessage
{
    public int Id { get; set; }
    
    /// <summary>
    /// Role: "user" or "assistant"
    /// </summary>
    public string Role { get; set; } = string.Empty;
    
    /// <summary>
    /// Message text (encrypted)
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Message creation date and time
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Chat session ID (for grouping messages)
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Tokens used for generation (optional)
    /// </summary>
    public int? TokensUsed { get; set; }
}
