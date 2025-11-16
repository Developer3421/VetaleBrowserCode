using System;

namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// AI-generated summary of search results
/// </summary>
public class AiSearchSummary
{
    /// <summary>
    /// Search query that was summarized
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// AI-generated summary text
    /// </summary>
    public string SummaryText { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Timestamp when summary was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current state of the summary
    /// </summary>
    public AiSummaryState State { get; set; } = AiSummaryState.Idle;

    /// <summary>
    /// Error message if any
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// State of AI summary generation
/// </summary>
public enum AiSummaryState
{
    /// <summary>
    /// No summary requested yet
    /// </summary>
    Idle,

    /// <summary>
    /// Summary is being generated
    /// </summary>
    Loading,

    /// <summary>
    /// Summary generated successfully
    /// </summary>
    Ready,

    /// <summary>
    /// No summary available for this query
    /// </summary>
    NoSummary,

    /// <summary>
    /// Error occurred during generation
    /// </summary>
    Error
}

