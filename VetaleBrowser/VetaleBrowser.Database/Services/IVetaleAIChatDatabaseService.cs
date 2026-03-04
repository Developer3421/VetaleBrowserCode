using System;
using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Interface for the Vetale AI chat history service
/// </summary>
public interface IVetaleAIChatDatabaseService : IDisposable
{
    /// <summary>
    /// Adds a message to the chat history
    /// </summary>
    void AddMessage(string role, string message, string sessionId, int? tokensUsed = null);
    
    /// <summary>
    /// Gets messages with pagination
    /// </summary>
    List<VetaleAIChatMessage> GetMessages(string? sessionId = null, int page = 0, int pageSize = 100);
    
    /// <summary>
    /// Gets the last N messages for context
    /// </summary>
    List<VetaleAIChatMessage> GetLastMessages(int count, string? sessionId = null);
    
    /// <summary>
    /// Gets all chat sessions
    /// </summary>
    List<string> GetSessions();
    
    /// <summary>
    /// Deletes a message
    /// </summary>
    void DeleteMessage(int id);
    
    /// <summary>
    /// Clears the entire chat history
    /// </summary>
    void ClearHistory();
    
    /// <summary>
    /// Clears the session history
    /// </summary>
    void ClearSession(string sessionId);
    
    /// <summary>
    /// Clears history older than the specified date
    /// </summary>
    void ClearHistoryOlderThan(DateTime date);
    
    /// <summary>
    /// Searches in the chat history
    /// </summary>
    List<VetaleAIChatMessage> SearchMessages(string query, int page = 0, int pageSize = 100);
}

