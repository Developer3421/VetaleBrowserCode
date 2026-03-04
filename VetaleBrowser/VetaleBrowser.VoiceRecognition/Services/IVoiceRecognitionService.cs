using System;
using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.VoiceRecognition.Services;

/// <summary>
/// Voice recognition service
/// </summary>
public interface IVoiceRecognitionService
{
    /// <summary>
    /// Event raised when text is recognized
    /// </summary>
    event EventHandler<string>? TextRecognized;

    /// <summary>
    /// Event raised when the recognition state changes
    /// </summary>
    event EventHandler<VoiceRecognitionState>? StateChanged;

    /// <summary>
    /// Event raised when an error occurs
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Starts listening for voice input
    /// </summary>
    Task StartListeningAsync();

    /// <summary>
    /// Stops listening for voice input
    /// </summary>
    void StopListening();

    /// <summary>
    /// Checks whether voice recognition is available
    /// </summary>
    bool IsAvailable();

    /// <summary>
    /// Current recognition state
    /// </summary>
    VoiceRecognitionState CurrentState { get; }
}

/// <summary>
/// Voice recognition state
/// </summary>
public enum VoiceRecognitionState
{
    Idle,
    Listening,
    Processing,
    Error
}

