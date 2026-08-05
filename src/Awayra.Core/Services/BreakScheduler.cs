using Awayra.Core.Abstractions;
using Awayra.Core.Models;

namespace Awayra.Core.Services;

public sealed class BreakScheduler
{
    public const int MoveActivityCount = 5;

    /// <summary>
    /// Grace period applied to the *other* reminder immediately after a snooze, so that dismissing
    /// one break does not instantly replace it with a second fullscreen overlay. It deliberately
    /// does not last for the whole snooze duration: each reminder keeps its own schedule.
    /// </summary>
    public static readonly TimeSpan SnoozeHandoffGrace = TimeSpan.FromSeconds(60);

    private readonly IClock _clock;
    private AppSettings _settings;
    private SchedulerState _state;
    private bool _isIdle;
    private bool _isConfigurationPaused;
    private TimeSpan? _configFrozenEyeRemaining;
    private TimeSpan? _configFrozenMoveRemaining;
    private TimeSpan? _configFrozenEyeSnoozeRemaining;
    private TimeSpan? _configFrozenMoveSnoozeRemaining;
    private TimeSpan? _manualFrozenEyeRemaining;
    private TimeSpan? _manualFrozenMoveRemaining;
    private bool _outsideWorkHours;
    private TimeSpan? _workHoursFrozenEyeRemaining;
    private TimeSpan? _workHoursFrozenMoveRemaining;
    private TimeSpan? _idleFrozenEyeRemaining;
    private TimeSpan? _idleFrozenMoveRemaining;
    private int _moveActivityIndex;
    private bool _snoozeInProgress;
    private BreakType? _lastSnoozedBreak;
    private DateTimeOffset? _snoozeHandoffUntil;

    public BreakScheduler(IClock clock, AppSettings settings, SchedulerState? persistedState = null)
    {
        _clock = clock;
        _settings = settings;
        _state = persistedState ?? SchedulerState.CreateDefault(clock.Now);
        _state.LastClockCheck = clock.Now;
        NormalizeStateOnLoad();
    }

    public event EventHandler<SchedulerSnapshot>? SnapshotChanged;
    public event EventHandler<BreakStartedEventArgs>? BreakStarted;
    public event EventHandler<BreakEndedEventArgs>? BreakEnded;

    public AppSettings Settings => _settings;
    public SchedulerState State => _state;
    public int MoveActivityIndex => _moveActivityIndex;

    public SchedulerSnapshot GetSnapshot()
    {
        var now = _clock.Now;
        var status = ComputeStatus(now);
        var eyeRemaining = GetRemaining(BreakType.Eye, now);
        var moveRemaining = GetRemaining(BreakType.Move, now);

        TimeSpan? activeRemaining = null;
        if (_state.ActiveBreak is not null && _state.BreakEndsAt is not null)
        {
            activeRemaining = _state.BreakEndsAt.Value - now;
            if (activeRemaining < TimeSpan.Zero)
            {
                activeRemaining = TimeSpan.Zero;
            }
        }

        DateTimeOffset? nextDue = null;
        if (_settings.EyeResetEnabled && _settings.MoveBreakEnabled)
        {
            nextDue = _state.EyeNextDue <= _state.MoveNextDue ? _state.EyeNextDue : _state.MoveNextDue;
        }
        else if (_settings.EyeResetEnabled)
        {
            nextDue = _state.EyeNextDue;
        }
        else if (_settings.MoveBreakEnabled)
        {
            nextDue = _state.MoveNextDue;
        }

        return new SchedulerSnapshot
        {
            Status = status,
            IsPausedManual = _state.IsPausedManual,
            EyeRemaining = eyeRemaining,
            MoveRemaining = moveRemaining,
            EyeEnabled = _settings.EyeResetEnabled,
            MoveEnabled = _settings.MoveBreakEnabled,
            ActiveBreak = _state.ActiveBreak,
            QueuedBreak = _state.QueuedBreak,
            ActiveBreakRemaining = activeRemaining,
            NextBreakDue = nextDue
        };
    }

    public void Tick()
    {
        var now = _clock.Now;
        HandleClockJump(now);
        _state.LastClockCheck = now;
        UpdateWorkHoursFreeze(now);

        if (_state.ActiveBreak is not null)
        {
            if (_state.BreakEndsAt is not null && now >= _state.BreakEndsAt.Value)
            {
                CompleteActiveBreak();
            }

            PublishSnapshot();
            return;
        }

        if (!CanDeliverReminders(now))
        {
            PublishSnapshot();
            return;
        }

        TryStartDueBreak(now);
        PublishSnapshot();
    }

    public void UpdateSettings(AppSettings settings)
    {
        if (!SettingsValidator.IsValid(settings))
        {
            throw new InvalidOperationException("Invalid settings cannot be applied to scheduler.");
        }

        var now = _clock.Now;
        var wasEyeEnabled = _settings.EyeResetEnabled;
        var wasMoveEnabled = _settings.MoveBreakEnabled;
        var oldEyeInterval = _settings.EyeResetIntervalMinutes;
        var oldMoveInterval = _settings.MoveBreakIntervalMinutes;
        _settings = settings;

        if (!wasEyeEnabled && settings.EyeResetEnabled)
        {
            _state.EyeNextDue = now.AddMinutes(settings.EyeResetIntervalMinutes);
            ClearEyeFreezeState();
        }
        else if (oldEyeInterval != settings.EyeResetIntervalMinutes || !settings.EyeResetEnabled)
        {
            RescheduleOnIntervalChange(BreakType.Eye, now, settings.EyeResetIntervalMinutes, settings.EyeResetEnabled);
        }

        if (!wasMoveEnabled && settings.MoveBreakEnabled)
        {
            _state.MoveNextDue = now.AddMinutes(settings.MoveBreakIntervalMinutes);
            ClearMoveFreezeState();
        }
        else if (oldMoveInterval != settings.MoveBreakIntervalMinutes || !settings.MoveBreakEnabled)
        {
            RescheduleOnIntervalChange(BreakType.Move, now, settings.MoveBreakIntervalMinutes, settings.MoveBreakEnabled);
        }

        PublishSnapshot();
    }

    public SchedulerDiagnostics GetDiagnostics(double idleSeconds = 0)
    {
        var now = _clock.Now;
        return new SchedulerDiagnostics
        {
            Status = ComputeStatus(now),
            EyeRemainingSeconds = (int)GetRemaining(BreakType.Eye, now).TotalSeconds,
            MoveRemainingSeconds = (int)GetRemaining(BreakType.Move, now).TotalSeconds,
            EyeNextDue = _state.EyeNextDue,
            MoveNextDue = _state.MoveNextDue,
            EyeSnoozeUntil = _state.EyeSnoozeUntil,
            MoveSnoozeUntil = _state.MoveSnoozeUntil,
            IsPausedManual = _state.IsPausedManual,
            IsIdlePaused = _settings.PauseWhileIdle && _isIdle,
            IsConfigurationPaused = _isConfigurationPaused,
            IsOutsideWorkHours = _settings.WorkHoursEnabled && _outsideWorkHours,
            ActiveBreak = _state.ActiveBreak,
            QueuedBreak = _state.QueuedBreak,
            GlassClarity = _settings.GlassClarity,
            BackgroundTintOpacity = OverlayGlassSettings.BackgroundTintOpacityFromClarity(_settings.GlassClarity),
            BlurRadius = OverlayGlassSettings.BlurRadiusFromClarity(_settings.GlassClarity),
            IdleSeconds = idleSeconds
        };
    }

    private void ClearEyeFreezeState()
    {
        _manualFrozenEyeRemaining = null;
        _workHoursFrozenEyeRemaining = null;
        _configFrozenEyeRemaining = null;
    }

    private void ClearMoveFreezeState()
    {
        _manualFrozenMoveRemaining = null;
        _workHoursFrozenMoveRemaining = null;
        _configFrozenMoveRemaining = null;
    }

    public void EnterConfigurationPause()
    {
        var now = _clock.Now;
        _configFrozenEyeRemaining = GetRawRemaining(BreakType.Eye, now);
        _configFrozenMoveRemaining = GetRawRemaining(BreakType.Move, now);
        if (_state.EyeSnoozeUntil is not null && now < _state.EyeSnoozeUntil.Value)
        {
            _configFrozenEyeSnoozeRemaining = _configFrozenEyeRemaining;
        }

        if (_state.MoveSnoozeUntil is not null && now < _state.MoveSnoozeUntil.Value)
        {
            _configFrozenMoveSnoozeRemaining = _configFrozenMoveRemaining;
        }

        _isConfigurationPaused = true;
        PublishSnapshot();
    }

    public void ApplyConfigurationSave(AppSettings settings, DateTimeOffset saveTime)
    {
        if (!SettingsValidator.IsValid(settings))
        {
            throw new InvalidOperationException("Invalid settings cannot be applied to scheduler.");
        }

        var originalSettings = _settings;
        var eyeScheduleChanged = SettingsScheduleChanges.EyeScheduleChanged(originalSettings, settings);
        var moveScheduleChanged = SettingsScheduleChanges.MoveScheduleChanged(originalSettings, settings);

        var frozenEye = _configFrozenEyeRemaining;
        var frozenMove = _configFrozenMoveRemaining;
        var frozenEyeSnooze = _configFrozenEyeSnoozeRemaining;
        var frozenMoveSnooze = _configFrozenMoveSnoozeRemaining;

        _settings = settings;
        _isConfigurationPaused = false;
        _configFrozenEyeRemaining = null;
        _configFrozenMoveRemaining = null;
        _configFrozenEyeSnoozeRemaining = null;
        _configFrozenMoveSnoozeRemaining = null;

        if (eyeScheduleChanged)
        {
            if (settings.EyeResetEnabled)
            {
                _state.EyeNextDue = saveTime.AddMinutes(settings.EyeResetIntervalMinutes);
            }

            _state.EyeSnoozeUntil = null;
        }
        else if (frozenEye is not null && settings.EyeResetEnabled)
        {
            _state.EyeNextDue = saveTime + frozenEye.Value;
            if (frozenEyeSnooze is not null)
            {
                _state.EyeSnoozeUntil = _state.EyeNextDue;
            }
        }

        if (moveScheduleChanged)
        {
            if (settings.MoveBreakEnabled)
            {
                _state.MoveNextDue = saveTime.AddMinutes(settings.MoveBreakIntervalMinutes);
            }

            _state.MoveSnoozeUntil = null;
        }
        else if (frozenMove is not null && settings.MoveBreakEnabled)
        {
            _state.MoveNextDue = saveTime + frozenMove.Value;
            if (frozenMoveSnooze is not null)
            {
                _state.MoveSnoozeUntil = _state.MoveNextDue;
            }
        }

        if (eyeScheduleChanged || moveScheduleChanged)
        {
            _state.QueuedBreak = null;
            ClearSnoozeHandoff();
        }

        PublishSnapshot();
    }

    public void CancelConfigurationPause()
    {
        var now = _clock.Now;
        if (_configFrozenEyeRemaining is not null && _settings.EyeResetEnabled)
        {
            _state.EyeNextDue = now + _configFrozenEyeRemaining.Value;
            if (_configFrozenEyeSnoozeRemaining is not null)
            {
                _state.EyeSnoozeUntil = _state.EyeNextDue;
            }
        }

        if (_configFrozenMoveRemaining is not null && _settings.MoveBreakEnabled)
        {
            _state.MoveNextDue = now + _configFrozenMoveRemaining.Value;
            if (_configFrozenMoveSnoozeRemaining is not null)
            {
                _state.MoveSnoozeUntil = _state.MoveNextDue;
            }
        }

        _isConfigurationPaused = false;
        _configFrozenEyeRemaining = null;
        _configFrozenMoveRemaining = null;
        _configFrozenEyeSnoozeRemaining = null;
        _configFrozenMoveSnoozeRemaining = null;
        PublishSnapshot();
    }

    public void Pause()
    {
        var now = _clock.Now;
        _manualFrozenEyeRemaining = GetRawRemaining(BreakType.Eye, now);
        _manualFrozenMoveRemaining = GetRawRemaining(BreakType.Move, now);
        _state.IsPausedManual = true;
        PublishSnapshot();
    }

    public void Resume()
    {
        var now = _clock.Now;
        if (_manualFrozenEyeRemaining is not null && _settings.EyeResetEnabled)
        {
            _state.EyeNextDue = now + _manualFrozenEyeRemaining.Value;
        }

        if (_manualFrozenMoveRemaining is not null && _settings.MoveBreakEnabled)
        {
            _state.MoveNextDue = now + _manualFrozenMoveRemaining.Value;
        }

        _manualFrozenEyeRemaining = null;
        _manualFrozenMoveRemaining = null;
        _state.IsPausedManual = false;
        PublishSnapshot();
    }

    private void UpdateWorkHoursFreeze(DateTimeOffset now)
    {
        if (!_settings.WorkHoursEnabled)
        {
            if (_outsideWorkHours)
            {
                ResumeFromWorkHoursFreeze(now);
            }

            _outsideWorkHours = false;
            return;
        }

        var inside = WorkHoursEvaluator.IsWithinWorkHours(now, true, _settings.WorkStart, _settings.WorkEnd);
        if (!inside && !_outsideWorkHours)
        {
            _workHoursFrozenEyeRemaining = GetRawRemaining(BreakType.Eye, now);
            _workHoursFrozenMoveRemaining = GetRawRemaining(BreakType.Move, now);
            _outsideWorkHours = true;
        }
        else if (inside && _outsideWorkHours)
        {
            ResumeFromWorkHoursFreeze(now);
            _outsideWorkHours = false;
        }
        else if (!inside)
        {
            _outsideWorkHours = true;
        }
        else
        {
            _outsideWorkHours = false;
        }
    }

    private void ResumeFromWorkHoursFreeze(DateTimeOffset now)
    {
        if (_workHoursFrozenEyeRemaining is not null && _settings.EyeResetEnabled)
        {
            _state.EyeNextDue = now + _workHoursFrozenEyeRemaining.Value;
        }

        if (_workHoursFrozenMoveRemaining is not null && _settings.MoveBreakEnabled)
        {
            _state.MoveNextDue = now + _workHoursFrozenMoveRemaining.Value;
        }

        _workHoursFrozenEyeRemaining = null;
        _workHoursFrozenMoveRemaining = null;
    }

    public void SetIdle(bool isIdle)
    {
        if (_isIdle == isIdle)
        {
            return;
        }

        if (isIdle && !_isIdle && _settings.PauseWhileIdle)
        {
            var now = _clock.Now;
            _idleFrozenEyeRemaining = GetRawRemaining(BreakType.Eye, now);
            _idleFrozenMoveRemaining = GetRawRemaining(BreakType.Move, now);
        }

        if (!isIdle && _isIdle && _settings.PauseWhileIdle)
        {
            ResetIntervalsAfterIdleReturn(_clock.Now);
        }

        _isIdle = isIdle;
        PublishSnapshot();
    }

    private void ResetIntervalsAfterIdleReturn(DateTimeOffset now)
    {
        _idleFrozenEyeRemaining = null;
        _idleFrozenMoveRemaining = null;

        if (_settings.EyeResetEnabled)
        {
            _state.EyeNextDue = now.AddMinutes(_settings.EyeResetIntervalMinutes);
        }

        _state.EyeSnoozeUntil = null;

        if (_settings.MoveBreakEnabled)
        {
            _state.MoveNextDue = now.AddMinutes(_settings.MoveBreakIntervalMinutes);
        }

        _state.MoveSnoozeUntil = null;
        _state.QueuedBreak = null;
        ClearSnoozeHandoff();
    }

    public void TriggerNow(BreakType breakType)
    {
        if (_isConfigurationPaused)
        {
            return;
        }

        if (!IsBreakEnabled(breakType))
        {
            return;
        }

        if (_state.ActiveBreak is not null)
        {
            if (_state.QueuedBreak is null && _state.ActiveBreak != breakType)
            {
                _state.QueuedBreak = breakType;
            }

            PublishSnapshot();
            return;
        }

        StartBreak(breakType, manual: true);
        PublishSnapshot();
    }

    public void CompleteActiveBreak()
    {
        if (_state.ActiveBreak is null)
        {
            return;
        }

        var breakType = _state.ActiveBreak.Value;
        EndBreak(breakType, completed: true, skipped: false, snoozed: false);
        TryStartQueuedOrDue();
        PublishSnapshot();
    }

    public void SkipActiveBreak()
    {
        if (_state.ActiveBreak is null || !_settings.AllowSkip)
        {
            return;
        }

        var breakType = _state.ActiveBreak.Value;
        EndBreak(breakType, completed: false, skipped: true, snoozed: false);
        TryStartQueuedOrDue();
        PublishSnapshot();
    }

    public void SnoozeActiveBreak()
    {
        if (_state.ActiveBreak is null || !_settings.AllowSnooze || _snoozeInProgress)
        {
            return;
        }

        _snoozeInProgress = true;
        try
        {
            var now = _clock.Now;
            var breakType = _state.ActiveBreak.Value;
            var snoozeEnd = now.AddMinutes(_settings.SnoozeDurationMinutes);

            if (breakType == BreakType.Eye)
            {
                _state.EyeNextDue = snoozeEnd;
                _state.EyeSnoozeUntil = snoozeEnd;
            }
            else
            {
                _state.MoveNextDue = snoozeEnd;
                _state.MoveSnoozeUntil = snoozeEnd;
            }

            _state.SnoozeUntil = null;
            _lastSnoozedBreak = breakType;
            _snoozeHandoffUntil = now + SnoozeHandoffGrace;
            EndBreak(breakType, completed: false, skipped: false, snoozed: true);
            PublishSnapshot();
        }
        finally
        {
            _snoozeInProgress = false;
        }
    }

    public void RestoreState(SchedulerState state)
    {
        _state = state;
        NormalizeStateOnLoad();
        PublishSnapshot();
    }

    private void NormalizeStateOnLoad()
    {
        var now = _clock.Now;
        if (_state.EyeNextDue == default)
        {
            _state.EyeNextDue = now.AddMinutes(_settings.EyeResetIntervalMinutes);
        }

        if (_state.MoveNextDue == default)
        {
            _state.MoveNextDue = now.AddMinutes(_settings.MoveBreakIntervalMinutes);
        }

        if (_state.LastClockCheck == default)
        {
            _state.LastClockCheck = now;
        }

        MigrateLegacySnoozeState();
    }

    private void MigrateLegacySnoozeState()
    {
        if (_state.SnoozeUntil is null)
        {
            return;
        }

        if (_state.EyeSnoozeUntil is null)
        {
            _state.EyeSnoozeUntil = _state.SnoozeUntil;
            _state.EyeNextDue = _state.SnoozeUntil.Value;
        }

        _state.SnoozeUntil = null;
    }

    private bool IsAnyBreakSnoozed(DateTimeOffset now) =>
        IsBreakSnoozed(BreakType.Eye, now) || IsBreakSnoozed(BreakType.Move, now);

    private bool IsBreakSnoozed(BreakType breakType, DateTimeOffset now)
    {
        var snoozeUntil = breakType == BreakType.Eye ? _state.EyeSnoozeUntil : _state.MoveSnoozeUntil;
        return snoozeUntil is not null && now < snoozeUntil.Value;
    }

    /// <summary>
    /// True while <paramref name="breakType"/> is still inside the short handoff grace that follows
    /// a snooze of the *other* reminder. The reminder that was snoozed is governed by its own
    /// snooze time instead, so a snoozed Eye Reset never postpones a Move Break beyond this grace.
    /// </summary>
    private bool IsWithinSnoozeHandoffGrace(BreakType breakType, DateTimeOffset now) =>
        _snoozeHandoffUntil is not null &&
        now < _snoozeHandoffUntil.Value &&
        _lastSnoozedBreak is not null &&
        _lastSnoozedBreak.Value != breakType;

    private void ClearSnoozeHandoff()
    {
        _lastSnoozedBreak = null;
        _snoozeHandoffUntil = null;
    }

    private bool CanStartBreakNow(BreakType breakType, DateTimeOffset now) =>
        !IsBreakSnoozed(breakType, now) && !IsWithinSnoozeHandoffGrace(breakType, now);

    private void HandleClockJump(DateTimeOffset now)
    {
        var delta = now - _state.LastClockCheck;
        if (delta < TimeSpan.FromMinutes(-1))
        {
            if (_state.EyeNextDue < now)
            {
                _state.EyeNextDue = now.AddMinutes(_settings.EyeResetIntervalMinutes);
            }

            if (_state.MoveNextDue < now)
            {
                _state.MoveNextDue = now.AddMinutes(_settings.MoveBreakIntervalMinutes);
            }
        }
    }

    private bool CanDeliverReminders(DateTimeOffset now)
    {
        if (_state.IsPausedManual)
        {
            return false;
        }

        if (_isConfigurationPaused)
        {
            return false;
        }

        if (_settings.PauseWhileIdle && _isIdle)
        {
            return false;
        }

        if (!WorkHoursEvaluator.IsWithinWorkHours(now, _settings.WorkHoursEnabled, _settings.WorkStart, _settings.WorkEnd))
        {
            return false;
        }

        return true;
    }

    private SchedulerStatus ComputeStatus(DateTimeOffset now)
    {
        if (_state.ActiveBreak is not null)
        {
            return SchedulerStatus.BreakActive;
        }

        if (IsAnyBreakSnoozed(now))
        {
            return SchedulerStatus.Snoozed;
        }

        if (!_settings.EyeResetEnabled && !_settings.MoveBreakEnabled)
        {
            return SchedulerStatus.Disabled;
        }

        if (_state.IsPausedManual)
        {
            return SchedulerStatus.PausedManual;
        }

        if (_isConfigurationPaused)
        {
            return SchedulerStatus.ConfigurationPaused;
        }

        if (_settings.PauseWhileIdle && _isIdle)
        {
            return SchedulerStatus.Idle;
        }

        if (!WorkHoursEvaluator.IsWithinWorkHours(now, _settings.WorkHoursEnabled, _settings.WorkStart, _settings.WorkEnd))
        {
            return SchedulerStatus.OutsideWorkHours;
        }

        return SchedulerStatus.Running;
    }

    private TimeSpan GetRemaining(BreakType breakType, DateTimeOffset now)
    {
        if (!IsBreakEnabled(breakType))
        {
            return TimeSpan.Zero;
        }

        if (_state.IsPausedManual)
        {
            var manualFrozen = breakType == BreakType.Eye ? _manualFrozenEyeRemaining : _manualFrozenMoveRemaining;
            if (manualFrozen is not null)
            {
                return manualFrozen.Value;
            }
        }

        if (_isConfigurationPaused)
        {
            var configFrozen = breakType == BreakType.Eye ? _configFrozenEyeRemaining : _configFrozenMoveRemaining;
            if (configFrozen is not null)
            {
                return configFrozen.Value;
            }
        }

        if (_settings.WorkHoursEnabled && _outsideWorkHours)
        {
            var workFrozen = breakType == BreakType.Eye ? _workHoursFrozenEyeRemaining : _workHoursFrozenMoveRemaining;
            if (workFrozen is not null)
            {
                return workFrozen.Value;
            }
        }

        if (_settings.PauseWhileIdle && _isIdle)
        {
            var idleFrozen = breakType == BreakType.Eye ? _idleFrozenEyeRemaining : _idleFrozenMoveRemaining;
            if (idleFrozen is not null)
            {
                return idleFrozen.Value;
            }
        }

        return GetRawRemaining(breakType, now);
    }

    private TimeSpan GetRawRemaining(BreakType breakType, DateTimeOffset now)
    {
        var due = breakType == BreakType.Eye ? _state.EyeNextDue : _state.MoveNextDue;
        var remaining = due - now;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private bool IsBreakEnabled(BreakType breakType) =>
        breakType == BreakType.Eye ? _settings.EyeResetEnabled : _settings.MoveBreakEnabled;

    private void TryStartDueBreak(DateTimeOffset now)
    {
        // Each reminder is evaluated on its own schedule. Snoozing one reminder must never hold the
        // other one back beyond the short handoff grace, otherwise a 60-minute Eye Reset snooze
        // would silently postpone an unrelated Move Break by up to an hour.
        var dueBreaks = new List<(BreakType Type, DateTimeOffset Due)>();
        if (_settings.EyeResetEnabled && now >= _state.EyeNextDue && CanStartBreakNow(BreakType.Eye, now))
        {
            dueBreaks.Add((BreakType.Eye, _state.EyeNextDue));
        }

        if (_settings.MoveBreakEnabled && now >= _state.MoveNextDue && CanStartBreakNow(BreakType.Move, now))
        {
            dueBreaks.Add((BreakType.Move, _state.MoveNextDue));
        }

        if (dueBreaks.Count == 0)
        {
            return;
        }

        dueBreaks.Sort((a, b) => a.Due.CompareTo(b.Due));
        StartBreak(dueBreaks[0].Type, manual: false);

        if (dueBreaks.Count > 1 && _state.QueuedBreak is null)
        {
            _state.QueuedBreak = dueBreaks[1].Type;
        }
    }

    private void TryStartQueuedOrDue()
    {
        var now = _clock.Now;
        if (_state.ActiveBreak is not null)
        {
            return;
        }

        if (!CanDeliverReminders(now))
        {
            return;
        }

        if (_state.QueuedBreak is not null)
        {
            var queued = _state.QueuedBreak.Value;
            if (!CanStartBreakNow(queued, now))
            {
                // The queued reminder was snoozed while another break was on screen. Drop it and
                // let its own due time bring it back rather than holding the other reminder back.
                _state.QueuedBreak = null;
                TryStartDueBreak(now);
                return;
            }

            _state.QueuedBreak = null;
            StartBreak(queued, manual: false);
            return;
        }

        TryStartDueBreak(now);
    }

    private void StartBreak(BreakType breakType, bool manual)
    {
        if (!IsBreakEnabled(breakType))
        {
            return;
        }

        var now = _clock.Now;
        var durationSeconds = breakType == BreakType.Eye
            ? _settings.EyeResetDurationSeconds
            : _settings.MoveBreakDurationSeconds;

        _state.ActiveBreak = breakType;
        _state.BreakEndsAt = now.AddSeconds(durationSeconds);
        _state.QueuedBreak = null;
        ClearSnoozeHandoff();

        if (breakType == BreakType.Eye)
        {
            _state.EyeSnoozeUntil = null;
        }
        else
        {
            _state.MoveSnoozeUntil = null;
        }

        if (breakType == BreakType.Move)
        {
            _moveActivityIndex = (_moveActivityIndex + 1) % MoveActivityCount;
        }

        BreakStarted?.Invoke(this, new BreakStartedEventArgs
        {
            BreakType = breakType,
            DurationSeconds = durationSeconds,
            ActivityIndex = _moveActivityIndex
        });
    }

    private void EndBreak(BreakType breakType, bool completed, bool skipped, bool snoozed)
    {
        var now = _clock.Now;
        _state.ActiveBreak = null;
        _state.BreakEndsAt = null;

        if (completed)
        {
            ScheduleNextFromCompletion(breakType, now);
        }
        else if (skipped)
        {
            ScheduleNextFromCompletion(breakType, now);
        }
        else if (snoozed)
        {
            // Per-break snooze due times were set by the caller.
        }

        BreakEnded?.Invoke(this, new BreakEndedEventArgs
        {
            BreakType = breakType,
            Completed = completed,
            Skipped = skipped,
            Snoozed = snoozed
        });
    }

    private void ScheduleNextFromCompletion(BreakType breakType, DateTimeOffset from)
    {
        var interval = breakType == BreakType.Eye
            ? _settings.EyeResetIntervalMinutes
            : _settings.MoveBreakIntervalMinutes;

        if (breakType == BreakType.Eye)
        {
            _state.EyeNextDue = from.AddMinutes(interval);
            _state.EyeLastCompleted = from;
        }
        else
        {
            _state.MoveNextDue = from.AddMinutes(interval);
            _state.MoveLastCompleted = from;
        }
    }

    private void RescheduleOnIntervalChange(BreakType breakType, DateTimeOffset now, int intervalMinutes, bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        var lastCompleted = breakType == BreakType.Eye ? _state.EyeLastCompleted : _state.MoveLastCompleted;
        var anchor = lastCompleted ?? now;
        var next = anchor.AddMinutes(intervalMinutes);
        if (next < now)
        {
            next = now;
        }

        if (breakType == BreakType.Eye)
        {
            _state.EyeNextDue = next;
        }
        else
        {
            _state.MoveNextDue = next;
        }
    }

    private void PublishSnapshot() => SnapshotChanged?.Invoke(this, GetSnapshot());
}
