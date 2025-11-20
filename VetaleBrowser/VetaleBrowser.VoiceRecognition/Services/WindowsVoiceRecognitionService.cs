using System;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VetaleBrowser.VetaleBrowser.VoiceRecognition.Services;

/// <summary>
/// Реалізація розпізнавання голосу для Windows з використанням Whisper.NET та NAudio
/// Працює повністю офлайн з багатомовною підтримкою
/// </summary>
public class WindowsVoiceRecognitionService : IVoiceRecognitionService, IDisposable
{
    private WhisperProcessor? _processor;
    private WaveInEvent? _waveIn;
    private VoiceRecognitionState _currentState;
    private bool _isDisposed;
    private string? _modelPath;
    private MemoryStream? _audioStream;
    private List<byte> _audioBuffer = new();

    public event EventHandler<string>? TextRecognized;
    public event EventHandler<VoiceRecognitionState>? StateChanged;
    public event EventHandler<string>? ErrorOccurred;

    public VoiceRecognitionState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState != value)
            {
                _currentState = value;
                StateChanged?.Invoke(this, value);
                System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] State changed to: {value}");
            }
        }
    }

    public WindowsVoiceRecognitionService(string? modelPath = null)
    {
        _currentState = VoiceRecognitionState.Idle;
        _modelPath = modelPath ?? GetDefaultModelPath();
        System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Initialized with model path: {_modelPath}");
    }

    private string GetDefaultModelPath()
    {
        // Шукаємо модель Whisper в декількох стандартних місцях
        var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // 1. Папка моделей всередині проєкту: VetaleBrowser.VoiceRecognition/Models
        //    (файли звідти копіюються в вихідну директорію завдяки налаштуванню в csproj)
        var projectModelsPath = Path.Combine(appDir ?? string.Empty, "Models", "whisper", "ggml-base.bin");

        var localPaths = new[]
        {
            projectModelsPath,
            Path.Combine(appDir ?? "", "Models", "whisper", "ggml-small.bin"),
            Path.Combine(appDir ?? "", "Models", "whisper", "ggml-tiny.bin"),
            Path.Combine(appDir ?? "", "VoiceModels", "ggml-base.bin"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VetaleBrowser", "Models", "ggml-base.bin"),
            "ggml-base.bin" // поточна директорія
        };

        foreach (var path in localPaths)
        {
            if (File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Found local Whisper model at: {path}");
                return path;
            }
        }

        // Якщо не знайдено жодної локальної моделі, повертаємо порожній шлях
        System.Diagnostics.Debug.WriteLine("[VoiceRecognition] No local model found in project or AppData, will rely on runtime/built-in model if available");
        return string.Empty;
    }

    public bool IsAvailable()
    {
        try
        {
            Console.WriteLine($"[VOICE][SERVICE] IsAvailable called. modelPath={_modelPath}");
            System.Diagnostics.Debug.WriteLine($"[VOICE][SERVICE] IsAvailable called. modelPath={_modelPath}");
            
            // Якщо шлях порожній - використовуємо вбудовану модель (завжди доступна)
            var modelExists = string.IsNullOrEmpty(_modelPath) || File.Exists(_modelPath);
            var deviceCount = WaveInEvent.DeviceCount;
            
            Console.WriteLine($"[VOICE][SERVICE] ModelExists={modelExists}, DeviceCount={deviceCount}");
            System.Diagnostics.Debug.WriteLine($"[VOICE][SERVICE] ModelExists={modelExists}, DeviceCount={deviceCount}");

            if (!modelExists)
            {
                Console.WriteLine($"[VoiceRecognition] Whisper model not found at: {_modelPath}");
                System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Whisper model not found at: {_modelPath}");
                return false;
            }

            if (deviceCount == 0)
            {
                Console.WriteLine("[VoiceRecognition] No microphone devices found");
                System.Diagnostics.Debug.WriteLine("[VoiceRecognition] No microphone devices found");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VoiceRecognition] Availability check failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Availability check failed: {ex.Message}");
            return false;
        }
    }

    public async Task StartListeningAsync()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(WindowsVoiceRecognitionService));

        try
        {
            Console.WriteLine("[VOICE][SERVICE] ⚡⚡⚡ StartListeningAsync called ⚡⚡⚡");
            System.Diagnostics.Debug.WriteLine("[VOICE][SERVICE] StartListeningAsync called");
            CurrentState = VoiceRecognitionState.Processing;

            await Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("[VOICE][SERVICE] Initializing recognizer...");
                    System.Diagnostics.Debug.WriteLine("[VOICE][SERVICE] Initializing recognizer...");
                    InitializeRecognizer();
                    
                    Console.WriteLine("[VOICE][SERVICE] Starting recording...");
                    System.Diagnostics.Debug.WriteLine("[VOICE][SERVICE] Starting recording...");
                    StartRecording();
                    
                    CurrentState = VoiceRecognitionState.Listening;
                    Console.WriteLine("[VoiceRecognition] ✓ Started listening...");
                    System.Diagnostics.Debug.WriteLine("[VoiceRecognition] Started listening...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VoiceRecognition] ✗ Failed to start: {ex.Message}");
                    Console.WriteLine($"Stack: {ex.StackTrace}");
                    System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Failed to start: {ex.Message}");
                    CurrentState = VoiceRecognitionState.Error;
                    ErrorOccurred?.Invoke(this, $"Не вдалося запустити розпізнавання: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VoiceRecognition] ✗ StartListeningAsync error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] StartListeningAsync error: {ex.Message}");
            CurrentState = VoiceRecognitionState.Error;
            ErrorOccurred?.Invoke(this, $"Помилка: {ex.Message}");
        }
    }

    public void StopListening()
    {
        try
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                System.Diagnostics.Debug.WriteLine("[VoiceRecognition] Stopped listening");
            }
            CurrentState = VoiceRecognitionState.Idle;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Stop error: {ex.Message}");
        }
    }

    private void InitializeRecognizer()
    {
        if (_processor == null)
        {
            Console.WriteLine($"[VoiceRecognition] Initializing Whisper processor...");
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Initializing Whisper processor...");
            
            WhisperFactory factory;
            
            // Якщо вказано шлях до моделі - використовуємо його
            if (!string.IsNullOrEmpty(_modelPath) && File.Exists(_modelPath))
            {
                Console.WriteLine($"[VoiceRecognition] Loading custom Whisper model from: {_modelPath}");
                System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Loading custom Whisper model from: {_modelPath}");
                factory = WhisperFactory.FromPath(_modelPath);
            }
            else
            {
                // Використовуємо вбудовану модель з Whisper.net.Runtime
                Console.WriteLine("[VoiceRecognition] Using built-in Whisper model from runtime");
                System.Diagnostics.Debug.WriteLine("[VoiceRecognition] Using built-in Whisper model from runtime");
                
                try
                {
                    // Whisper.NET автоматично знайде вбудовану модель
                    factory = WhisperFactory.FromPath("ggml-base.bin");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VoiceRecognition] ✗ Failed to load model: {ex.Message}");
                    throw new InvalidOperationException(
                        "Модель Whisper не знайдена.\n" +
                        "Можливі рішення:\n" +
                        "1. Помістіть ggml-base.bin у папку програми\n" +
                        "2. Помістіть модель у %AppData%/VetaleBrowser/Models/\n" +
                        "3. Переконайтеся, що Whisper.net.Runtime встановлено");
                }
            }
            
            Console.WriteLine("[VoiceRecognition] Creating processor with auto language detection...");
            // Створюємо процесор з багатомовною підтримкою
            _processor = factory.CreateBuilder()
                .WithLanguage("auto") // Автоматичне визначення мови
                .Build();
            
            Console.WriteLine("[VoiceRecognition] ✓ Whisper processor initialized successfully");
            System.Diagnostics.Debug.WriteLine("[VoiceRecognition] Whisper processor initialized successfully");
        }
        
        // Ініціалізуємо буфер для аудіо
        _audioBuffer = new List<byte>();
    }

    private void StartRecording()
    {
        try
        {
            Console.WriteLine("[VoiceRecognition] 🔧 Setting up microphone...");
            
            if (_waveIn != null)
            {
                Console.WriteLine("[VoiceRecognition] Disposing previous WaveIn...");
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }

            // Виводимо список доступних пристроїв
            Console.WriteLine($"[VoiceRecognition] Available audio devices: {WaveInEvent.DeviceCount}");
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                Console.WriteLine($"[VoiceRecognition]   Device {i}: {caps.ProductName} (Channels: {caps.Channels})");
            }

            Console.WriteLine("[VoiceRecognition] Creating WaveInEvent...");
            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0, // Використовуємо перший пристрій
                WaveFormat = new WaveFormat(16000, 1), // 16 кГц, моно
                BufferMilliseconds = 100 // Буфер 100мс для швидшого реагування
            };

            Console.WriteLine($"[VoiceRecognition] WaveIn configured: Format={_waveIn.WaveFormat}, Device={_waveIn.DeviceNumber}");

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            Console.WriteLine("[VoiceRecognition] Starting recording...");
            _waveIn.StartRecording();
            
            Console.WriteLine("[VoiceRecognition] 🎙️ Recording started! Speak now...");
            System.Diagnostics.Debug.WriteLine("[VoiceRecognition] Recording started");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VoiceRecognition] ✗✗✗ StartRecording FAILED: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            throw new InvalidOperationException($"Не вдалося запустити мікрофон: {ex.Message}", ex);
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            Console.WriteLine($"[VoiceRecognition] 📥 OnDataAvailable called! BytesRecorded={e.BytesRecorded}");
            
            if (e.BytesRecorded > 0)
            {
                // Показуємо перші байти для діагностики
                if (_audioBuffer.Count == 0 && e.BytesRecorded >= 10)
                {
                    var sample = string.Join(", ", e.Buffer.Take(10).Select(b => b.ToString()));
                    Console.WriteLine($"[VoiceRecognition] First 10 bytes: {sample}");
                }
                
                // Додаємо дані до буфера
                for (int i = 0; i < e.BytesRecorded; i++)
                {
                    _audioBuffer.Add(e.Buffer[i]);
                }
                
                Console.WriteLine($"[VoiceRecognition] 📊 Audio buffer: {_audioBuffer.Count} bytes (just added {e.BytesRecorded})");
                
                // Логуємо кожні 10000 байтів щоб не заспамити
                if (_audioBuffer.Count % 10000 < e.BytesRecorded)
                {
                    Console.WriteLine($"[VoiceRecognition] 📊 Total buffer: {_audioBuffer.Count} bytes");
                }
                System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Audio buffer size: {_audioBuffer.Count} bytes");
            }
            else
            {
                Console.WriteLine("[VoiceRecognition] ⚠️ OnDataAvailable called but BytesRecorded=0!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VoiceRecognition] ✗ OnDataAvailable error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] OnDataAvailable error: {ex.Message}");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            Console.WriteLine("[VoiceRecognition] 🛑 Recording stopped, processing audio...");
            System.Diagnostics.Debug.WriteLine("[VoiceRecognition] Recording stopped, processing audio...");
            
            if (_processor != null && _audioBuffer.Count > 0)
            {
                CurrentState = VoiceRecognitionState.Processing;
                
                // Запускаємо асинхронну обробку в фоновому потоці
                _ = Task.Run(async () => await ProcessAudioBufferAsync());
            }
            else if (_audioBuffer.Count == 0)
            {
                Console.WriteLine("[VoiceRecognition] ⚠️ Audio buffer is empty!");
            }

            if (e.Exception != null)
            {
                Console.WriteLine($"[VoiceRecognition] ✗ Recording stopped with error: {e.Exception.Message}");
                System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Recording stopped with error: {e.Exception.Message}");
                CurrentState = VoiceRecognitionState.Error;
                ErrorOccurred?.Invoke(this, e.Exception.Message);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[VoiceRecognition] Recording stopped normally");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VoiceRecognition] ✗ OnRecordingStopped error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] OnRecordingStopped error: {ex.Message}");
            CurrentState = VoiceRecognitionState.Error;
            ErrorOccurred?.Invoke(this, $"Помилка обробки: {ex.Message}");
        }
    }

    private async Task ProcessAudioBufferAsync()
    {
        try
        {
            if (_processor == null || _audioBuffer.Count == 0)
            {
                Console.WriteLine("[VoiceRecognition] No audio data to process");
                System.Diagnostics.Debug.WriteLine("[VoiceRecognition] No audio data to process");
                CurrentState = VoiceRecognitionState.Idle;
                return;
            }

            // Whisper потребує мінімум ~1 секунду аудіо (16000 Hz * 1 сек * 2 bytes = 32000 bytes)
            const int minAudioBytes = 32000;
            if (_audioBuffer.Count < minAudioBytes)
            {
                Console.WriteLine($"[VoiceRecognition] ⚠️ Too little audio data ({_audioBuffer.Count} bytes < {minAudioBytes} bytes)");
                Console.WriteLine("[VoiceRecognition] ℹ️ Speak for at least 1-2 seconds for better recognition");
                CurrentState = VoiceRecognitionState.Idle;
                _audioBuffer.Clear();
                return;
            }

            Console.WriteLine($"[VoiceRecognition] 🔄 Processing {_audioBuffer.Count} bytes of audio...");
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Processing {_audioBuffer.Count} bytes of audio");

            // Конвертуємо byte[] в float[] для Whisper
            var audioData = new float[_audioBuffer.Count / 2];
            for (int i = 0; i < audioData.Length; i++)
            {
                short sample = BitConverter.ToInt16(_audioBuffer.ToArray(), i * 2);
                audioData[i] = sample / 32768f; // Нормалізуємо до [-1.0, 1.0]
            }

            Console.WriteLine($"[VoiceRecognition] Converted to {audioData.Length} float samples");
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Converted to {audioData.Length} float samples");

            Console.WriteLine("[VoiceRecognition] 🤖 Running Whisper.NET processing...");
            
            // Обробляємо аудіо через Whisper асинхронно
            var textBuilder = new System.Text.StringBuilder();
            
            await foreach (var segment in _processor.ProcessAsync(audioData))
            {
                var segmentText = segment.Text;
                textBuilder.Append(segmentText);
                textBuilder.Append(" ");
                Console.WriteLine($"[VoiceRecognition] 📝 Segment: '{segmentText}'");
                System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Segment: {segmentText}");
            }

            var fullText = textBuilder.ToString().Trim();

            // Фільтруємо службові маркери Whisper
            if (fullText.Contains("[BLANK_AUDIO]") || fullText.Contains("(BLANK_AUDIO)") || 
                fullText.Contains("[MUSIC]") || fullText.Contains("(music)"))
            {
                Console.WriteLine($"[VoiceRecognition] ⚠️ Detected blank/noise audio, ignoring: '{fullText}'");
                CurrentState = VoiceRecognitionState.Idle;
                return;
            }

            if (!string.IsNullOrWhiteSpace(fullText))
            {
                Console.WriteLine($"[VoiceRecognition] ✅ RECOGNIZED TEXT: '{fullText}'");
                System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Recognized text: {fullText}");
                TextRecognized?.Invoke(this, fullText);
            }
            else
            {
                Console.WriteLine("[VoiceRecognition] ⚠️ No text recognized (silence or unclear audio)");
                System.Diagnostics.Debug.WriteLine("[VoiceRecognition] No text recognized");
            }

            // Очищаємо буфер
            _audioBuffer.Clear();
            CurrentState = VoiceRecognitionState.Idle;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VoiceRecognition] ✗✗✗ ProcessAudioBuffer ERROR: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] ProcessAudioBuffer error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[VoiceRecognition] Stack trace: {ex.StackTrace}");
            CurrentState = VoiceRecognitionState.Error;
            ErrorOccurred?.Invoke(this, $"Помилка розпізнавання: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        StopListening();

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        if (_processor != null)
        {
            _processor.Dispose();
            _processor = null;
        }

        if (_audioStream != null)
        {
            _audioStream.Dispose();
            _audioStream = null;
        }

        _audioBuffer.Clear();

        System.Diagnostics.Debug.WriteLine("[VoiceRecognition] Disposed");
    }
}
