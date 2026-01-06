using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;

namespace VetaleBrowser.VetaleBrowser.VoiceRecognition.Services;

/// <summary>
/// Voice recognition service using Whisper.NET.
/// 
/// Windows capture backend: NAudio (WinMM).
/// NOTE: user will rework Linux separately.
/// </summary>
public class WindowsVoiceRecognitionService : IVoiceRecognitionService, IDisposable
{
    private WhisperProcessor? _processor;

    private WaveInEvent? _waveIn;
    private readonly List<byte> _audioPcmBytes = new();
    private readonly object _lock = new();

    private VoiceRecognitionState _currentState;
    private bool _isDisposed;
    private string? _modelPath;

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

    public bool IsAvailable()
    {
        try
        {
            var modelExists = string.IsNullOrEmpty(_modelPath) || File.Exists(_modelPath);
            if (!modelExists)
                return false;

            // NAudio is Windows-oriented; simplest availability check: at least one recording device.
            return WaveInEvent.DeviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task StartListeningAsync()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(WindowsVoiceRecognitionService));

        if (CurrentState == VoiceRecognitionState.Listening || CurrentState == VoiceRecognitionState.Processing)
            return;

        if (!IsAvailable())
        {
            CurrentState = VoiceRecognitionState.Error;
            ErrorOccurred?.Invoke(this, "Розпізнавання голосу недоступне: немає доступного пристрою запису");
            return;
        }

        CurrentState = VoiceRecognitionState.Processing;

        await Task.Run(() =>
        {
            try
            {
                InitializeRecognizer();

                lock (_lock) _audioPcmBytes.Clear();

                _waveIn?.Dispose();
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = 0,
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 50
                };

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _waveIn.StartRecording();
                CurrentState = VoiceRecognitionState.Listening;
            }
            catch (Exception ex)
            {
                CurrentState = VoiceRecognitionState.Error;
                ErrorOccurred?.Invoke(this, $"Не вдалося запустити запис: {ex.Message}");
            }
        });
    }

    public void StopListening()
    {
        try
        {
            if (CurrentState == VoiceRecognitionState.Idle)
                return;

            _waveIn?.StopRecording();
        }
        catch (Exception ex)
        {
            CurrentState = VoiceRecognitionState.Error;
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
            return;

        lock (_lock)
        {
            for (int i = 0; i < e.BytesRecorded; i++)
                _audioPcmBytes.Add(e.Buffer[i]);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _waveIn!.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;

        if (e.Exception != null)
        {
            CurrentState = VoiceRecognitionState.Error;
            ErrorOccurred?.Invoke(this, e.Exception.Message);
            return;
        }

        var pcm = GetAndClearCapturedPcm();
        if (pcm.Length == 0)
        {
            CurrentState = VoiceRecognitionState.Idle;
            return;
        }

        _ = Task.Run(() => RecognizePcmAsync(pcm));
    }

    private byte[] GetAndClearCapturedPcm()
    {
        lock (_lock)
        {
            var arr = _audioPcmBytes.ToArray();
            _audioPcmBytes.Clear();
            return arr;
        }
    }

    private async Task RecognizePcmAsync(byte[] pcm16le)
    {
        try
        {
            CurrentState = VoiceRecognitionState.Processing;

            if (_processor == null)
                throw new InvalidOperationException("Whisper processor not initialized.");

            using var ms = new MemoryStream(pcm16le, writable: false);

            string? lastText = null;
            await foreach (var segment in _processor.ProcessAsync(ms))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                    lastText = segment.Text;
            }

            if (!string.IsNullOrWhiteSpace(lastText))
                TextRecognized?.Invoke(this, lastText.Trim());

            CurrentState = VoiceRecognitionState.Idle;
        }
        catch (Exception ex)
        {
            CurrentState = VoiceRecognitionState.Error;
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }

    private void InitializeRecognizer()
    {
        if (_processor != null)
            return;

        WhisperFactory factory;

        if (string.IsNullOrEmpty(_modelPath))
            _modelPath = FindModelPath();

        if (!string.IsNullOrEmpty(_modelPath) && File.Exists(_modelPath))
        {
            factory = WhisperFactory.FromPath(_modelPath);
        }
        else
        {
            throw new InvalidOperationException("Whisper model not found. Please place ggml-*.bin in output directory.");
        }

        _processor = factory.CreateBuilder()
            .WithLanguage("uk")
            .Build();
    }

    private string GetDefaultModelPath()
    {
        var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        var localPaths = new[]
        {
            Path.Combine(appDir ?? "", "ggml-base.bin"),
            Path.Combine(appDir ?? "", "ggml-small.bin"),
            Path.Combine(appDir ?? "", "ggml-tiny.bin"),
            Path.Combine(appDir ?? "", "Models", "whisper", "ggml-base.bin"),
            Path.Combine(appDir ?? "", "Models", "ggml-base.bin"),
            Path.Combine(appDir ?? "", "VoiceModels", "ggml-base.bin"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VetaleBrowser", "Models", "ggml-base.bin"),
            Path.Combine(Environment.CurrentDirectory, "ggml-base.bin")
        };

        foreach (var path in localPaths)
            if (File.Exists(path))
                return path;

        return string.Empty;
    }

    private string FindModelPath()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        var searchPaths = new[]
        {
            Path.Combine(appDir, "ggml-base.bin"),
            Path.Combine(appDir, "ggml-small.bin"),
            Path.Combine(appDir, "ggml-tiny.bin"),
            Path.Combine(exeDir ?? "", "ggml-base.bin"),
            Path.Combine(appDir, "Models", "ggml-base.bin"),
            Path.Combine(appDir, "VetaleBrowser.VoiceRecognition", "Models", "ggml-base.bin"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VetaleBrowser", "Models", "ggml-base.bin"),
            Path.Combine(Environment.CurrentDirectory, "ggml-base.bin"),
        };

        foreach (var path in searchPaths)
            if (File.Exists(path))
                return path;

        return string.Empty;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.Dispose();
                _waveIn = null;
            }

            _processor?.Dispose();
        }
        catch
        {
            // ignored
        }
        finally
        {
            _processor = null;
        }
    }
}
