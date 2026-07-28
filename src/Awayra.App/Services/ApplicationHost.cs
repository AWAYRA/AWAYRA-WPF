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

    private AppSettings _settings = AppSettings.CreateDefault();
    private BreakScheduler _scheduler = null!;
    private StatisticsService _statistics = null!;
    private System.Timers.Timer? _tickTimer;
    private System.Timers.Timer? _idleTimer;
    private System.Timers.Timer? _diagnosticsTimer;
    private bool _isShuttingDown;
    private bool _configurationSessionActive;
    private bool _wasIdle;

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

    public async Task InitializeAsync()
    {
        AppPaths.EnsureDataRoot();
        _settings = await _settingsStore.LoadAsync().ConfigureAwait(false);
        if (!SettingsValidator.IsValid(_settings))
        {
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
        _scheduler.BreakEnded += OnBreakEnded;

        _tickTimer = new System.Timers.Timer(1000);
        _tickTimer.Elapsed += (_, _) => _scheduler.Tick();
        _tickTimer.AutoReset = true;
        _tickTimer.Start();

        _idleTimer = new System.Timers.Timer(UiTestMode.IsEnabled ? 1_000 : 5_000);
        _idleTimer.Elapsed += (_, _) => UpdateIdleState();
        _idleTimer.AutoReset = true;
        _idleTimer.Start();
        UpdateIdleState();

        if (UiTestMode.IsEnabled && UiTestMode.DataRoot is not null)
        {
            UiTestDiagnosticsWriter.Initialize(UiTestMode.DataRoot);
            _diagnosticsTimer = new System.Timers.Timer(1_000);
            _diagnosticsTimer.Elapsed += (_, _) => PublishUiTestDiagnostics();
            _diagnosticsTimer.AutoReset = true;
            _diagnosticsTimer.Start();
            PublishUiTestDiagnostics();
        }

        _logger.Info("Awayra initialized.");
        await PersistStateAsync().ConfigureAwait(false);
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
        _tickTimer?.Stop();
        _idleTimer?.Stop();
        _tickTimer?.Dispose();
        _idleTimer?.Dispose();
        _diagnosticsTimer?.Dispose();
        _logger.Info("Awayra shutting down.");
    }

    public void Dispose() => Shutdown();

    private async void UpdateIdleState()
    {
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
            await _statisticsStore.SaveAsync(_statistics.Data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to persist after break ended", ex);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
