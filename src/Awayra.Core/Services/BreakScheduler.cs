using Awayra.Core.Abstractions;
using Awayra.Core.Models;

namespace Awayra.Core.Services;

public sealed class BreakScheduler
{
    public const int MoveActivityCount = 5;

    private readonly IClock _clock;
    private AppSettings _settings;
    private SchedulerState _state;
    private bool _isIdle;
    private int _moveActivityIndex;

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

        if (_state.ActiveBreak is not null)
        {
            if (_state.BreakEndsAt is not null && now >= _state.BreakEndsAt.Value)
            {
                CompleteActiveBreak();
            }

            PublishSnapshot();
            return;
        }

        if (_state.SnoozeUntil is not null)
        {
            if (now >= _state.SnoozeUntil.Value)
            {
                _state.SnoozeUntil = null;
                TryStartDueBreak(now);
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
        var old = _settings;
        _settings = settings;

        if (old.EyeResetIntervalMinutes != settings.EyeResetIntervalMinutes || !settings.EyeResetEnabled)
        {
            RescheduleOnIntervalChange(BreakType.Eye, now, settings.EyeResetIntervalMinutes, settings.EyeResetEnabled);
        }

        if (old.MoveBreakIntervalMinutes != settings.MoveBreakIntervalMinutes || !settings.MoveBreakEnabled)
        {
            RescheduleOnIntervalChange(BreakType.Move, now, settings.MoveBreakIntervalMinutes, settings.MoveBreakEnabled);
        }

        PublishSnapshot();
    }

    public void Pause()
    {
        _state.IsPausedManual = true;
        PublishSnapshot();
    }

    public void Resume()
    {
        _state.IsPausedManual = false;
        PublishSnapshot();
    }

    public void SetIdle(bool isIdle)
    {
        if (_isIdle == isIdle)
        {
            return;
        }

        _isIdle = isIdle;

        if (!isIdle && _state.ActiveBreak is null && _state.SnoozeUntil is null)
        {
            var now = _clock.Now;
            RescheduleAfterIdleReturn(now);
        }

        PublishSnapshot();
    }

    public void TriggerNow(BreakType breakType)
    {
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
        if (_state.ActiveBreak is null || !_settings.AllowSnooze)
        {
            return;
        }

        var breakType = _state.ActiveBreak.Value;
        EndBreak(breakType, completed: false, skipped: false, snoozed: true);
        _state.SnoozeUntil = _clock.Now.AddMinutes(_settings.SnoozeDurationMinutes);
        TryStartQueuedOrDue();
        PublishSnapshot();
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
    }

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

        if (_state.SnoozeUntil is not null && now < _state.SnoozeUntil.Value)
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

        if (_settings.PauseWhileIdle && _isIdle)
        {
            return SchedulerStatus.PausedIdle;
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

        var due = breakType == BreakType.Eye ? _state.EyeNextDue : _state.MoveNextDue;
        var remaining = due - now;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private bool IsBreakEnabled(BreakType breakType) =>
        breakType == BreakType.Eye ? _settings.EyeResetEnabled : _settings.MoveBreakEnabled;

    private void TryStartDueBreak(DateTimeOffset now)
    {
        var dueBreaks = new List<(BreakType Type, DateTimeOffset Due)>();
        if (_settings.EyeResetEnabled && now >= _state.EyeNextDue)
        {
            dueBreaks.Add((BreakType.Eye, _state.EyeNextDue));
        }

        if (_settings.MoveBreakEnabled && now >= _state.MoveNextDue)
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
        if (_state.ActiveBreak is not null || _state.SnoozeUntil is not null)
        {
            return;
        }

        if (!CanDeliverReminders(_clock.Now))
        {
            return;
        }

        if (_state.QueuedBreak is not null)
        {
            var queued = _state.QueuedBreak.Value;
            _state.QueuedBreak = null;
            StartBreak(queued, manual: false);
            return;
        }

        TryStartDueBreak(_clock.Now);
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
            // SnoozeUntil set by caller; keep current due times.
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

    private void RescheduleAfterIdleReturn(DateTimeOffset now)
    {
        if (_settings.EyeResetEnabled && now >= _state.EyeNextDue && _settings.MoveBreakEnabled && now >= _state.MoveNextDue)
        {
            if (_state.EyeNextDue <= _state.MoveNextDue)
            {
                _state.MoveNextDue = now.AddMinutes(_settings.MoveBreakIntervalMinutes);
            }
            else
            {
                _state.EyeNextDue = now.AddMinutes(_settings.EyeResetIntervalMinutes);
            }
        }
    }

    private void PublishSnapshot() => SnapshotChanged?.Invoke(this, GetSnapshot());
}
