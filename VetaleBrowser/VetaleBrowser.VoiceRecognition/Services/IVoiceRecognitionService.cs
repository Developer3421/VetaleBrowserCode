using System;
using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.VoiceRecognition.Services;

/// <summary>
/// Сервіс для розпізнавання голосу
/// </summary>
public interface IVoiceRecognitionService
{
    /// <summary>
    /// Подія, що викликається при розпізнаванні тексту
    /// </summary>
    event EventHandler<string>? TextRecognized;

    /// <summary>
    /// Подія, що викликається при зміні стану розпізнавання
    /// </summary>
    event EventHandler<VoiceRecognitionState>? StateChanged;

    /// <summary>
    /// Подія, що викликається при виникненні помилки
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Починає прослуховування голосу
    /// </summary>
    Task StartListeningAsync();

    /// <summary>
    /// Зупиняє прослуховування голосу
    /// </summary>
    void StopListening();

    /// <summary>
    /// Перевіряє, чи доступне розпізнавання голосу
    /// </summary>
    bool IsAvailable();

    /// <summary>
    /// Поточний стан розпізнавання
    /// </summary>
    VoiceRecognitionState CurrentState { get; }
}

/// <summary>
/// Стан розпізнавання голосу
/// </summary>
public enum VoiceRecognitionState
{
    Idle,
    Listening,
    Processing,
    Error
}

