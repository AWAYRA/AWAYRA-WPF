using Awayra.App;
using Awayra.Core.Abstractions;
using Awayra.Core.Coordination;
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

    private AppSettings _settings = AppSettings.CreateDefault();
    private BreakScheduler _scheduler = null!;
    private StatisticsService _statistics = null!;
    private System.Timers.Timer? _tickTimer;
    private System.Timers.Timer? _idleTimer;
    private System.Timers.Timer? _diagnosticsTimer;
    private SynchronizationContext? _schedulerContext;
    private bool _isShuttingDown;
    private bool _configurationSessionActive;
    private bool _wasIdle;
    private int _tickPending;
    private int _idleUpdatePending;
    private int _diagnosticsPending;

    public ApplicationHost(
        IAppLogger logger,
        IClock clock,
        ISettingsStore settingsStore,
        IStateStore stateStore,
        IStatisticsStore statisticsStore,
        IIdleMonitor idleMonitor,
        IAutostartService autostartService,
        LocalizationService localization)
    {
        _logger = logger;
        _clock = clock;
        _settingsStore = settingsStore;
        _stateStore = stateStore;
        _statisticsStore = statisticsStore;
        _idleMonitor = idleMonitor;
        _autostartService = autostartService;
        _localization = localization;
    }

    public BreakScheduler Scheduler => _scheduler;
    public StatisticsService Statistics => _statistics;
    public AppSettings Settings => _settings;
    public LocalizationService Localization => _localization;
    public IAppLogger Logger => _logger;
    public IIdleMonitor IdleMonitor => _idleMonitor;

    public event EventHandler? StateChanged;
    public event EventHandler<int>? GlassClarityPreviewChanged;

    public async Task InitializeAsync(DateTimeOffset? currentBootStartedAtUtc = null)
    {
        // Capture the UI context before any ConfigureAwait(false). Timer callbacks
        // are posted back to this context so scheduler state has one runtime writer.
        _schedulerContext ??= SynchronizationContext.Current;

        AppPaths.EnsureDataRoot();
        _settings = SettingsRecovery.Normalize(
            await _settingsStore.LoadAsync().ConfigureAwait(false));

        if (UiTestMode.IsEnabled)
        {
            _settings = UiTestMode.ApplyDefaults(_settings);
            await _settingsStore.SaveAsync(_settings).ConfigureAwait(false);
        }

        _localization.Apply();
        var state = await _stateStore.LoadAsync().ConfigureAwait(false);
        var shouldResetForNewBoot = currentBootStartedAtUtc is { } currentBoot &&
            SystemBootSessionPolicy.ShouldResetTimers(state, currentBoot);
        _scheduler = new BreakScheduler(_clock, _settings, state);
        if (currentBootStartedAtUtc is { } bootStartedAtUtc)
        {
            if (shouldResetForNewBoot)
            {
                _scheduler.ResetForFreshStart();
                _logger.Info("Reminder timers reset for a new Windows boot.");
            }

            _scheduler.State.SystemBootStartedAtUtc = bootStartedAtUtc;
        }

        var statsData = await _statisticsStore.LoadAsync().ConfigureAwait(false);
        _statistics = new StatisticsService(_clock, statsData);

        _scheduler.SnapshotChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
        _scheduler.BreakEnded += OnBreakEnded;

        // Initialize deterministic state before any periodic callback can run.
        UpdateIdleState();
        if (UiTestMode.IsEnabled && UiTestMode.DataRoot is not null)
        {
            UiTestDiagnosticsWriter.Initialize(UiTestMode.DataRoot);
            PublishUiTestDiagnostics();
        }

        await PersistStateAsync().ConfigureAwait(false);

        _tickTimer = new System.Timers.Timer(1_000);
        _tickTimer.Elapsed += (_, _) => QueueTick();
        _tickTimer.AutoReset = true;
        _tickTimer.Start();

        _idleTimer = new System.Timers.Timer(UiTestMode.IsEnabled ? 1_000 : 5_000);
        _idleTimer.Elapsed += (_, _) => QueueIdleUpdate();
        _idleTimer.AutoReset = true;
        _idleTimer.Start();

        if (UiTestMode.IsEnabled && UiTestMode.DataRoot is not null)
        {
            _diagnosticsTimer = new System.Timers.Timer(1_000);
            _diagnosticsTimer.Elapsed += (_, _) => QueueDiagnosticsPublish();
            _diagnosticsTimer.AutoReset = true;
            _diagnosticsTimer.Start();
        }

        _logger.Info("Awayra initialized.");
    }

    public async Task ResetReminderTimersAsync()
    {
        _scheduler.ResetForFreshStart();
        await PersistStateAsync().ConfigureAwait(false);
        StateChanged?.Invoke(this, EventArgs.Empty);
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
        await _statisticsStore.SaveAsync(CloneStatistics(_statistics.Data)).ConfigureAwait(false);
        await _logger.FlushAsync().ConfigureAwait(false);
    }

    public async Task PersistStateAsync()
    {
        var snapshot = CloneState(_scheduler.State);
        await _stateStore.SaveAsync(snapshot).ConfigureAwait(false);
    }

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
        _tickTimer?.Stop();
        _idleTimer?.Stop();
        _diagnosticsTimer?.Stop();
        _tickTimer?.Dispose();
        _idleTimer?.Dispose();
        _diagnosticsTimer?.Dispose();
        _logger.Info("Awayra shutting down.");
    }

    public void Dispose() => Shutdown();

    private void QueueTick()
    {
        if (Interlocked.Exchange(ref _tickPending, 1) != 0)
        {
            return;
        }

        PostSchedulerAction(() =>
        {
            try
            {
                if (!_isShuttingDown)
                {
                    _scheduler.Tick();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Scheduler tick failed", ex);
            }
            finally
            {
                Volatile.Write(ref _tickPending, 0);
            }
        });
    }

    private void QueueIdleUpdate()
    {
        if (Interlocked.Exchange(ref _idleUpdatePending, 1) != 0)
        {
            return;
        }

        PostSchedulerAction(() =>
        {
            try
            {
                if (!_isShuttingDown)
                {
                    UpdateIdleState();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Idle-state update failed", ex);
            }
            finally
            {
                Volatile.Write(ref _idleUpdatePending, 0);
            }
        });
    }

    private void QueueDiagnosticsPublish()
    {
        if (Interlocked.Exchange(ref _diagnosticsPending, 1) != 0)
        {
            return;
        }

        PostSchedulerAction(() =>
        {
            try
            {
                if (!_isShuttingDown)
                {
                    PublishUiTestDiagnostics();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("UI test diagnostics publication failed", ex);
            }
            finally
            {
                Volatile.Write(ref _diagnosticsPending, 0);
            }
        });
    }

    private void PostSchedulerAction(Action action)
    {
        var context = _schedulerContext;
        if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
        {
            action();
            return;
        }

        context.Post(static state => ((Action)state!).Invoke(), action);
    }

    private void UpdateIdleState()
    {
        if (!_settings.PauseWhileIdle)
        {
            _wasIdle = false;
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
            _ = PersistAfterIdleReturnAsync();
        }
    }

    private async Task PersistAfterIdleReturnAsync()
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

    private void PublishUiTestDiagnostics()
    {
        var diagnostics = _scheduler.GetDiagnostics(_idleMonitor.GetIdleTime().TotalSeconds);
        diagnostics.SnapshotCaptured = false;
        var today = _statistics.GetToday();
        diagnostics.EyeCompleted = today.EyeCompleted;
        diagnostics.MoveCompleted = today.MoveCompleted;
        diagnostics.Skipped = today.Skipped;
        diagnostics.Snoozed = today.Snoozed;
        UiTestDiagnosticsWriter.Write(diagnostics);
    }

    private async void OnBreakEnded(object? sender, BreakEndedEventArgs e)
    {
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
            await _statisticsStore.SaveAsync(CloneStatistics(_statistics.Data)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to persist after break ended", ex);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static SchedulerState CloneState(SchedulerState state) => new()
    {
        SchemaVersion = state.SchemaVersion,
        EyeNextDue = state.EyeNextDue,
        MoveNextDue = state.MoveNextDue,
        IsPausedManual = state.IsPausedManual,
        ActiveBreak = state.ActiveBreak,
        QueuedBreak = state.QueuedBreak,
        BreakEndsAt = state.BreakEndsAt,
        SnoozeUntil = state.SnoozeUntil,
        EyeSnoozeUntil = state.EyeSnoozeUntil,
        MoveSnoozeUntil = state.MoveSnoozeUntil,
        LastClockCheck = state.LastClockCheck,
        SystemBootStartedAtUtc = state.SystemBootStartedAtUtc,
        EyeLastCompleted = state.EyeLastCompleted,
        MoveLastCompleted = state.MoveLastCompleted
    };

    private static StatisticsData CloneStatistics(StatisticsData data) => new()
    {
        SchemaVersion = data.SchemaVersion,
        Days = data.Days.ToDictionary(
            pair => pair.Key,
            pair => new DailyStatistics
            {
                EyeCompleted = pair.Value.EyeCompleted,
                MoveCompleted = pair.Value.MoveCompleted,
                Skipped = pair.Value.Skipped,
                Snoozed = pair.Value.Snoozed
            },
            StringComparer.Ordinal)
    };
}
