using System.Text;
using System.Windows.Media;
using System.Windows.Threading;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.App.Services;

public interface IBreakTonePlayer : IDisposable
{
    void Play(BreakSoundTheme theme, int volumePercent);
    void Stop();
}

public interface IBreakSoundService : IDisposable
{
    event EventHandler? StateChanged;

    bool IsSessionActive { get; }
    bool IsMuted { get; }
    BreakType? ActiveBreakType { get; }

    void StartBreak(BreakType breakType, AppSettings settings);
    void StopBreak();
    bool ToggleMute();
    void ApplySettings(AppSettings settings);
    void PauseForSystemTransition();
    void ResumeAfterSystemTransition(AppSettings settings);
    void Preview(BreakSoundTheme theme, int volumePercent);
    void StopPreview();
}

public sealed class BreakSoundService : IBreakSoundService
{
    private readonly Dispatcher _dispatcher;
    private readonly IAppLogger _logger;
    private readonly IBreakTonePlayer _player;
    private readonly DispatcherTimer _repeatTimer;

    private AppSettings _settings = AppSettings.CreateDefault();
    private bool _sessionActive;
    private bool _muted = true;
    private bool _sessionMuteOverridden;
    private bool _systemPaused;
    private bool _previewActive;
    private BreakType? _activeBreakType;
    private bool _disposed;

    public BreakSoundService(
        Dispatcher dispatcher,
        IAppLogger logger,
        IBreakTonePlayer? player = null)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _player = player ?? new MediaBreakTonePlayer(logger);
        _repeatTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(AppSettings.CreateDefault().BreakSoundRepeatSeconds)
        };
        _repeatTimer.Tick += (_, _) =>
        {
            if (_sessionActive && !_muted && !_systemPaused)
            {
                PlayCurrentTone();
            }
        };
    }

    public event EventHandler? StateChanged;

    public bool IsSessionActive => _sessionActive;
    public bool IsMuted => _muted;
    public BreakType? ActiveBreakType => _activeBreakType;

    public void StartBreak(BreakType breakType, AppSettings settings)
    {
        RunOnDispatcher(() =>
        {
            StopPreviewCore();
            StopPlaybackCore();

            _settings = settings.Copy();
            _sessionActive = true;
            _activeBreakType = breakType;
            _sessionMuteOverridden = false;
            _systemPaused = false;
            _muted = !IsSoundEnabledForBreak(settings, breakType);
            UpdateRepeatInterval(settings);

            if (!_muted)
            {
                PlayCurrentTone();
                _repeatTimer.Start();
            }

            RaiseStateChanged();
        });
    }

    public void StopBreak()
    {
        RunOnDispatcher(() =>
        {
            StopPlaybackCore();
            _sessionActive = false;
            _activeBreakType = null;
            _sessionMuteOverridden = false;
            _systemPaused = false;
            _muted = true;
            RaiseStateChanged();
        });
    }

    public bool ToggleMute()
    {
        var muted = true;
        RunOnDispatcher(() =>
        {
            if (!_sessionActive)
            {
                muted = true;
                return;
            }

            _sessionMuteOverridden = true;
            _muted = !_muted;
            muted = _muted;

            if (_muted || _systemPaused)
            {
                StopPlaybackCore();
            }
            else
            {
                PlayCurrentTone();
                _repeatTimer.Start();
            }

            RaiseStateChanged();
        });
        return muted;
    }

    public void ApplySettings(AppSettings settings)
    {
        RunOnDispatcher(() =>
        {
            _settings = settings.Copy();
            UpdateRepeatInterval(settings);

            if (_sessionActive && _activeBreakType is not null && !_sessionMuteOverridden)
            {
                _muted = !IsSoundEnabledForBreak(settings, _activeBreakType.Value);
            }

            if (_sessionActive && !_muted && !_systemPaused)
            {
                PlayCurrentTone();
                _repeatTimer.Start();
            }
            else if (_sessionActive)
            {
                StopPlaybackCore();
            }

            RaiseStateChanged();
        });
    }

    public void PauseForSystemTransition()
    {
        RunOnDispatcher(() =>
        {
            _systemPaused = true;
            StopPlaybackCore();
        });
    }

    public void ResumeAfterSystemTransition(AppSettings settings)
    {
        RunOnDispatcher(() =>
        {
            _settings = settings.Copy();
            UpdateRepeatInterval(settings);
            _systemPaused = false;

            if (_sessionActive && !_muted)
            {
                PlayCurrentTone();
                _repeatTimer.Start();
            }
        });
    }

    public void Preview(BreakSoundTheme theme, int volumePercent)
    {
        RunOnDispatcher(() =>
        {
            if (_sessionActive)
            {
                _logger.Info("Sound preview ignored while a break is active.");
                return;
            }

            _previewActive = true;
            _player.Stop();
            _player.Play(theme, Math.Clamp(volumePercent, 0, 100));
        });
    }

    public void StopPreview() => RunOnDispatcher(StopPreviewCore);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RunOnDispatcher(() =>
        {
            _repeatTimer.Stop();
            _player.Stop();
            _player.Dispose();
        });
    }

    private static bool IsSoundEnabledForBreak(AppSettings settings, BreakType breakType) =>
        breakType == BreakType.Eye
            ? settings.EyeBreakSoundEnabled
            : settings.MoveBreakSoundEnabled;

    private void UpdateRepeatInterval(AppSettings settings)
    {
        var seconds = Math.Clamp(
            settings.BreakSoundRepeatSeconds,
            SettingsValidator.MinBreakSoundRepeatSeconds,
            SettingsValidator.MaxBreakSoundRepeatSeconds);
        _repeatTimer.Interval = TimeSpan.FromSeconds(seconds);
    }

    private void PlayCurrentTone()
    {
        try
        {
            _player.Play(_settings.BreakSoundTheme, _settings.BreakSoundVolume);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Break sound playback failed: {ex.Message}");
            StopPlaybackCore();
        }
    }

    private void StopPlaybackCore()
    {
        _repeatTimer.Stop();
        _player.Stop();
    }

    private void StopPreviewCore()
    {
        if (!_previewActive)
        {
            return;
        }

        _previewActive = false;
        _player.Stop();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void RunOnDispatcher(Action action)
    {
        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.Invoke(action, DispatcherPriority.Background);
    }
}

public sealed class MediaBreakTonePlayer : IBreakTonePlayer
{
    private readonly IAppLogger _logger;
    private readonly MediaPlayer _player = new();
    private BreakSoundTheme? _loadedTheme;

    public MediaBreakTonePlayer(IAppLogger logger)
    {
        _logger = logger;
        _player.MediaFailed += (_, args) =>
            _logger.Warning($"Break sound media failure: {args.ErrorException?.Message ?? "unknown error"}");
    }

    public void Play(BreakSoundTheme theme, int volumePercent)
    {
        try
        {
            if (_loadedTheme != theme)
            {
                var path = BreakToneGenerator.GetOrCreateWaveFile(theme);
                _player.Open(new Uri(path, UriKind.Absolute));
                _loadedTheme = theme;
            }

            _player.Volume = Math.Clamp(volumePercent / 100d, 0d, 1d);
            _player.Stop();
            _player.Position = TimeSpan.Zero;
            _player.Play();
        }
        catch (Exception ex)
        {
            _logger.Warning($"Unable to play break sound: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            _player.Stop();
            _player.Position = TimeSpan.Zero;
        }
        catch
        {
            // MediaPlayer can already be shutting down. Sound must never block app shutdown.
        }
    }

    public void Dispose()
    {
        Stop();
        _player.Close();
    }
}

public sealed class NullBreakSoundService : IBreakSoundService
{
    public static NullBreakSoundService Instance { get; } = new();

    private NullBreakSoundService()
    {
    }

    public event EventHandler? StateChanged
    {
        add { }
        remove { }
    }

    public bool IsSessionActive => false;
    public bool IsMuted => true;
    public BreakType? ActiveBreakType => null;

    public void StartBreak(BreakType breakType, AppSettings settings) { }
    public void StopBreak() { }
    public bool ToggleMute() => true;
    public void ApplySettings(AppSettings settings) { }
    public void PauseForSystemTransition() { }
    public void ResumeAfterSystemTransition(AppSettings settings) { }
    public void Preview(BreakSoundTheme theme, int volumePercent) { }
    public void StopPreview() { }
    public void Dispose() { }
}

public static class BreakToneGenerator
{
    private const int SampleRate = 44_100;
    private const short Channels = 1;
    private const short BitsPerSample = 16;
    private const string CacheVersion = "v3";

    public static string GetOrCreateWaveFile(BreakSoundTheme theme)
    {
        // Routed through AppPaths so --ui-test-data-root really isolates a test run; the hard-coded
        // profile path meant UI tests wrote their sound cache into the real user profile.
        var directory = Path.Combine(AppPaths.DataRoot, "SoundCache", CacheVersion);
        Directory.CreateDirectory(directory);

        var fileName = theme switch
        {
            BreakSoundTheme.GentleChime => "gentle-chime.wav",
            BreakSoundTheme.CalmDrop => "calm-drop.wav",
            BreakSoundTheme.CalmPiano => "calm-piano.wav",
            BreakSoundTheme.MorningDew => "morning-dew.wav",
            BreakSoundTheme.StillWater => "still-water.wav",
            _ => "soft-bell.wav"
        };
        var path = Path.Combine(directory, fileName);

        if (!File.Exists(path) || new FileInfo(path).Length < 44)
        {
            File.WriteAllBytes(path, GenerateWaveBytes(theme));
        }

        return path;
    }

    public static byte[] GenerateWaveBytes(BreakSoundTheme theme)
    {
        var durationSeconds = theme switch
        {
            BreakSoundTheme.GentleChime => 1.15,
            BreakSoundTheme.CalmDrop => 0.95,
            BreakSoundTheme.CalmPiano => 1.8,
            BreakSoundTheme.MorningDew => 3.2,
            BreakSoundTheme.StillWater => 4.0,
            _ => 0.9
        };
        var sampleCount = (int)(SampleRate * durationSeconds);
        var samples = new double[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)SampleRate;
            samples[i] = theme switch
            {
                BreakSoundTheme.GentleChime =>
                    Bell(t, 0.00, 523.25, 4.8) +
                    Bell(t, 0.18, 659.25, 5.2) +
                    Bell(t, 0.36, 783.99, 5.6),
                BreakSoundTheme.CalmDrop =>
                    Drop(t, 0.00, 980, 520, 0.48) +
                    0.65 * Drop(t, 0.28, 760, 440, 0.42),
                BreakSoundTheme.CalmPiano =>
                    PianoNote(t, 0.00, 261.63) +
                    0.90 * PianoNote(t, 0.42, 329.63) +
                    0.84 * PianoNote(t, 0.84, 392.00) +
                    0.76 * PianoNote(t, 1.26, 329.63),

                // C major pentatonic, out and back: C - E - G - E - C.
                BreakSoundTheme.MorningDew =>
                    PhraseEnvelope(t, 3.2) * (
                        SoftVoice(t, 0.00, 523.25, 1.10) +
                        SoftVoice(t, 0.52, 659.25, 1.10) +
                        SoftVoice(t, 1.04, 783.99, 1.30) +
                        SoftVoice(t, 1.66, 659.25, 1.20) +
                        SoftVoice(t, 2.18, 523.25, 1.40)),

                // Same shape an octave lower and slower: A - C - E - C - A.
                BreakSoundTheme.StillWater =>
                    PhraseEnvelope(t, 4.0) * (
                        SoftVoice(t, 0.00, 220.00, 1.50) +
                        SoftVoice(t, 0.68, 261.63, 1.50) +
                        SoftVoice(t, 1.36, 329.63, 1.70) +
                        SoftVoice(t, 2.14, 261.63, 1.60) +
                        SoftVoice(t, 2.82, 220.00, 1.90)),
                _ =>
                    Bell(t, 0.00, 659.25, 5.4) +
                    0.82 * Bell(t, 0.24, 880.00, 6.0)
            };
        }

        // The melodic themes sit deliberately lower than the alert themes. They are meant to be
        // noticed, not to make anyone jump.
        var targetPeak = theme is BreakSoundTheme.MorningDew or BreakSoundTheme.StillWater ? 0.46 : 0.68;
        var peak = samples.Select(Math.Abs).DefaultIfEmpty(1d).Max();
        var scale = peak > 0 ? targetPeak / peak : 1d;
        var dataLength = sampleCount * sizeof(short);

        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(Channels);
        writer.Write(SampleRate);
        writer.Write(SampleRate * Channels * BitsPerSample / 8);
        writer.Write((short)(Channels * BitsPerSample / 8));
        writer.Write(BitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);

        foreach (var sample in samples)
        {
            var value = (short)Math.Clamp(sample * scale * short.MaxValue, short.MinValue, short.MaxValue);
            writer.Write(value);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// A single sung note with no percussive onset. The attack is an exponential approach rather
    /// than a ramp, so the waveform leaves silence smoothly and there is no click or transient to
    /// startle anyone. Neighbouring notes overlap and blend instead of cutting each other off.
    /// </summary>
    private static double SoftVoice(double t, double start, double frequency, double duration)
    {
        var x = t - start;
        if (x < 0 || x > duration)
        {
            return 0;
        }

        var attack = 1d - Math.Exp(-x / 0.11);
        var sustain = duration * 0.30;
        var release = x <= sustain ? 1d : Math.Exp(-(x - sustain) / (duration * 0.40));

        // Only two quiet harmonics. Anything brighter reads as an alert rather than a melody.
        var body =
            Math.Sin(2 * Math.PI * frequency * x) +
            0.26 * Math.Sin(2 * Math.PI * frequency * 2 * x) +
            0.07 * Math.Sin(2 * Math.PI * frequency * 3 * x);

        return attack * release * body;
    }

    /// <summary>
    /// Swells the whole phrase in and lets it recede, so the melody arrives from silence and
    /// returns to it. This is the amplitude half of the out-and-back shape; the pitch contour of
    /// each melodic theme is the other half.
    /// </summary>
    private static double PhraseEnvelope(double t, double totalSeconds)
    {
        if (t <= 0 || t >= totalSeconds)
        {
            return 0;
        }

        var fadeIn = Math.Min(1d, t / (totalSeconds * 0.16));
        var fadeOut = Math.Min(1d, (totalSeconds - t) / (totalSeconds * 0.34));
        return fadeIn * fadeOut;
    }

    private static double Bell(double t, double start, double frequency, double decay)
    {
        var x = t - start;
        if (x < 0)
        {
            return 0;
        }

        var attack = Math.Min(1d, x / 0.012);
        var envelope = attack * Math.Exp(-decay * x);
        return envelope *
               (Math.Sin(2 * Math.PI * frequency * x) +
                0.28 * Math.Sin(2 * Math.PI * frequency * 2.01 * x) +
                0.12 * Math.Sin(2 * Math.PI * frequency * 3.97 * x));
    }

    private static double PianoNote(double t, double start, double frequency)
    {
        var x = t - start;
        if (x < 0)
        {
            return 0;
        }

        var attack = Math.Min(1d, x / 0.006);
        var hammer = Math.Exp(-36d * x) * Math.Sin(2 * Math.PI * frequency * 4.02 * x);
        var body =
            Math.Sin(2 * Math.PI * frequency * x) +
            0.48 * Math.Sin(2 * Math.PI * frequency * 2.002 * x) +
            0.20 * Math.Sin(2 * Math.PI * frequency * 3.006 * x) +
            0.08 * Math.Sin(2 * Math.PI * frequency * 4.011 * x);
        var envelope = attack * Math.Exp(-3.5d * x);
        return envelope * (0.92 * body + 0.08 * hammer);
    }

    private static double Drop(double t, double start, double startFrequency, double endFrequency, double glideSeconds)
    {
        var x = t - start;
        if (x < 0)
        {
            return 0;
        }

        var glideTime = Math.Min(x, glideSeconds);
        var frequencySlope = (startFrequency - endFrequency) / glideSeconds;
        var cyclesDuringGlide = startFrequency * glideTime - 0.5 * frequencySlope * glideTime * glideTime;
        var cyclesAfterGlide = endFrequency * Math.Max(0d, x - glideSeconds);
        var phase = 2 * Math.PI * (cyclesDuringGlide + cyclesAfterGlide);
        var progress = Math.Min(1d, x / glideSeconds);
        var attack = Math.Min(1d, x / 0.008);
        var envelope = attack * Math.Exp(-5.8 * x) * (1d - 0.18 * progress);
        return envelope * (Math.Sin(phase) + 0.2 * Math.Sin(phase * 2.03));
    }
}