using System.Windows.Threading;
using Awayra.App;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.App.Services;

public sealed class ApplicationHost : IDisposable
{
    private readonly IAppLogger _logger;
    private readonly IClock _clock;
    private readonly ISettingsStore _settingsStore;
    private readonly IStateStore _stateStore;
    private readonly IStatisticsStore _statisticsStore;
    private readonly IIdleMonitor _idleMonitor;
    private readonly IAutostartService _autostartService;
    private readonly LocalizationService _localization;
    private readonly Dispatcher _dispatcher;
    private readonly IBreakSoundService _breakSound;

    private AppSettings _settings = AppSettings.CreateDefault();
    private BreakScheduler _scheduler = null!;
    private StatisticsService _statistics = null!;
    private DispatcherTimer? _tickTimer;
    private DispatcherTimer? _idleTimer;
    private DispatcherTimer? _diagnosticsTimer;
    private bool _isShuttingDown;
    private bool _configurationSessionActive;
    private bool _wasIdle;
    private bool _systemTransitionPaused;

    public ApplicationHost(
        IAppLogger logger,
        IClock clock,
        ISettingsStore settingsStore,
        IStateStore stateStore,
        IStatisticsStore statisticsStore,
        IIdleMonitor idleMonitor,
        IAutostartService autostartService,
        LocalizationService localization,
        Dispatcher? dispatcher = null,
        IBreakSoundService? breakSound = null)
    {
        _logger = logger;
        _clock = clock;
        _settingsStore = settingsStore;
        _stateStore = stateStore;
        _statisticsStore = statisticsStore;
        _idleMonitor = idleMonitor;
        _autostartService = autostartService;
        _localization = localization;
        _dispatcher = dispatcher
            ?? System.Windows.Application.Current?.Dispatcher
            ?? Dispatcher.CurrentDispatcher;
        _breakSound = breakSound ?? new BreakSoundService(_dispatcher, logger);
    }

    public BreakScheduler Scheduler => _scheduler;
    public StatisticsService Statistics => _statistics;
    public AppSettings Settings => _settings;
    public LocalizationService Localization => _localization;
    public IAppLogger Logger => _logger;
    public IIdleMonitor IdleMonitor => _idleMonitor;
    public IBreakSoundService BreakSound => _breakSound;

    public event EventHandler? StateChanged;
    public event EventHandler<int>? GlassClarityPreviewChanged;

    public async Task InitializeAsync()
    {
        _settings = await _settingsStore.LoadAsync().ConfigureAwait(false);

        // A settings file can hold a value outside its range without being malformed JSON, so it
        // never reaches the recovery loader. Repair clamps each field on its own; falling back to
        // defaults here would throw away every other preference the user had set.
        _settings = SettingsRecovery.Repair(_settings, _logger);
        if (!SettingsValidator.IsValid(_settings))
        {
            _logger.Error("Settings could not be repaired; falling back to defaults.");
            _settings = AppSettings.CreateDefault();
        }

        if (UiTestMode.IsEnabled)
        {
            _settings = UiTestMode.ApplyDefaults(_settings);
            await _settingsStore.SaveAsync(_settings).ConfigureAwait(false);
        }

        _localization.Apply();
        var state = await _stateStore.LoadAsync().ConfigureAwait(false);
        _scheduler = new BreakScheduler(_clock, _settings, state);
        var statsData = await _statisticsStore.LoadAsync().ConfigureAwait(false);
        _statistics = new StatisticsService(_clock, statsData);

        _scheduler.SnapshotChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
        _scheduler.BreakStarted += OnBreakStarted;
        _scheduler.BreakEnded += OnBreakEnded;
        _breakSound.ApplySettings(_settings);

        RunOnDispatcher(() =>
        {
            _tickTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _tickTimer.Tick += (_, _) =>
            {
                if (!_isShuttingDown && !_systemTransitionPaused)
                {
                    _scheduler.Tick();
                }
            };
            _tickTimer.Start();

            _idleTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(UiTestMode.IsEnabled ? 1_000 : 5_000)
            };
            _idleTimer.Tick += (_, _) => UpdateIdleState();
            _idleTimer.Start();
            UpdateIdleState();

            if (UiTestMode.IsEnabled && UiTestMode.DataRoot is not null)
            {
                UiTestDiagnosticsWriter.Initialize(UiTestMode.DataRoot);
                _diagnosticsTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _diagnosticsTimer.Tick += (_, _) => PublishUiTestDiagnostics();
                _diagnosticsTimer.Start();
                PublishUiTestDiagnostics();
            }
        });

        _logger.Info("Awayra initialized.");
        await PersistStateAsync().ConfigureAwait(false);
    }

    public void BeginSystemTransition()
    {
        RunOnDispatcher(() =>
        {
            if (!_isShuttingDown)
            {
                _systemTransitionPaused = true;
                _breakSound.PauseForSystemTransition();
            }
        });
    }

    public void CompleteSystemTransition()
    {
        RunOnDispatcher(() =>
        {
            if (_isShuttingDown)
            {
                return;
            }

            var wasPaused = _systemTransitionPaused;
            _systemTransitionPaused = false;
            if (wasPaused)
            {
                // Ticks were suspended for the whole lock or sleep. Anything that fell due in that
                // window belongs to time the user was away, so it is rebased rather than fired the
                // instant the session comes back.
                _scheduler.RebaseOverdueSchedules();
                _scheduler.Tick();
            }

            if (_scheduler.GetSnapshot().ActiveBreak is not null)
            {
                _breakSound.ResumeAfterSystemTransition(_settings);
            }
            else
            {
                _breakSound.StopBreak();
            }
        });
    }

    public void BeginConfigurationSession()
    {
        _scheduler.EnterConfigurationPause();
        _configurationSessionActive = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveConfigurationAsync(AppSettings settings)
    {
        if (!SettingsValidator.IsValid(settings))
        {
            throw new InvalidOperationException("Invalid settings.");
        }

        var saveTime = _clock.Now;
        _settings = settings;
        _scheduler.ApplyConfigurationSave(settings, saveTime);
        _breakSound.ApplySettings(settings);
        _configurationSessionActive = false;
        _localization.Apply();
        await _settingsStore.SaveAsync(settings).ConfigureAwait(false);
        await PersistStateAsync().ConfigureAwait(false);
        ApplyAutostartSetting();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EndConfigurationSession(bool saved)
    {
        if (!_configurationSessionActive)
        {
            return;
        }

        if (!saved)
        {
            _scheduler.CancelConfigurationPause();
        }

        _configurationSessionActive = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PreviewGlassClarity(int glassClarity) =>
        GlassClarityPreviewChanged?.Invoke(this, OverlayGlassSettings.NormalizeGlassClarity(glassClarity));

    public async Task UpdateSettingsAsync(AppSettings settings)
    {
        if (!SettingsValidator.IsValid(settings))
        {
            throw new InvalidOperationException("Invalid settings.");
        }

        _settings = settings;
        _scheduler.UpdateSettings(settings);
        _breakSound.ApplySettings(settings);
        _localization.Apply();
        await _settingsStore.SaveAsync(settings).ConfigureAwait(false);
        await PersistStateAsync().ConfigureAwait(false);
        ApplyAutostartSetting();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task PersistAllAsync()
    {
        await _settingsStore.SaveAsync(_settings).ConfigureAwait(false);
        await PersistStateAsync().ConfigureAwait(false);
        await _statisticsStore.SaveAsync(_statistics.Data).ConfigureAwait(false);
        await _logger.FlushAsync().ConfigureAwait(false);
    }

    public async Task PersistStateAsync() =>
        await _stateStore.SaveAsync(_scheduler.State).ConfigureAwait(false);

    public void ApplyAutostartSetting(string? executablePath = null)
    {
        try
        {
            var path = executablePath ?? Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (_settings.RunAtStartup)
            {
                _autostartService.Enable(path);
            }
            else
            {
                _autostartService.Disable();
            }

            _autostartService.RepairIfStale(path);
        }
        catch (Exception ex)
        {
            _logger.Error("Autostart update failed", ex);
        }
    }

    public void Shutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        RunOnDispatcher(() =>
        {
            _tickTimer?.Stop();
            _idleTimer?.Stop();
            _diagnosticsTimer?.Stop();
            _tickTimer = null;
            _idleTimer = null;
            _diagnosticsTimer = null;
            _systemTransitionPaused = false;
            _breakSound.Dispose();
        });

        // The file-backed stores each hold a semaphore that serialises writes.
        (_settingsStore as IDisposable)?.Dispose();
        (_stateStore as IDisposable)?.Dispose();
        (_statisticsStore as IDisposable)?.Dispose();

        _logger.Info("Awayra shutting down.");
    }

    public void Dispose() => Shutdown();

    private async void UpdateIdleState()
    {
        if (_isShuttingDown)
        {
            return;
        }

        if (!_settings.PauseWhileIdle)
        {
            if (_wasIdle)
            {
                _wasIdle = false;
            }

            _scheduler.SetIdle(false);
            return;
        }

        var threshold = TimeSpan.FromMinutes(_settings.IdleThresholdMinutes);
        var isIdle = _idleMonitor.IsIdle(threshold);
        var wasIdle = _wasIdle;
        _wasIdle = isIdle;
        _scheduler.SetIdle(isIdle);

        if (wasIdle && !isIdle)
        {
            try
            {
                await PersistStateAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to persist after idle return", ex);
            }
        }
    }

    private void PublishUiTestDiagnostics()
    {
        if (_isShuttingDown)
        {
            return;
        }

        var diagnostics = _scheduler.GetDiagnostics(_idleMonitor.GetIdleTime().TotalSeconds);
        diagnostics.SnapshotCaptured = false;
        var today = _statistics.GetToday();
        diagnostics.EyeCompleted = today.EyeCompleted;
        diagnostics.MoveCompleted = today.MoveCompleted;
        diagnostics.Skipped = today.Skipped;
        diagnostics.Snoozed = today.Snoozed;
        UiTestDiagnosticsWriter.Write(diagnostics);
    }

    private void OnBreakStarted(object? sender, BreakStartedEventArgs e) =>
        _breakSound.StartBreak(e.BreakType, _settings);

    private async void OnBreakEnded(object? sender, BreakEndedEventArgs e)
    {
        _breakSound.StopBreak();

        if (e.Completed)
        {
            _statistics.RecordCompletion(e.BreakType);
        }
        else if (e.Skipped)
        {
            _statistics.RecordSkip();
        }
        else if (e.Snoozed)
        {
            _statistics.RecordSnooze();
        }

        try
        {
            await PersistStateAsync().ConfigureAwait(false);
            await _statisticsStore.SaveAsync(_statistics.Data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to persist after break ended", ex);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

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
