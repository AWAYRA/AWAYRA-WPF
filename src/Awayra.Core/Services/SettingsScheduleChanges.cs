using Awayra.Core.Models;

namespace Awayra.Core.Services;

public static class SettingsScheduleChanges
{
    public static bool EyeScheduleChanged(AppSettings original, AppSettings updated) =>
        original.EyeResetEnabled != updated.EyeResetEnabled ||
        original.EyeResetIntervalMinutes != updated.EyeResetIntervalMinutes ||
        original.EyeResetDurationSeconds != updated.EyeResetDurationSeconds;

    public static bool MoveScheduleChanged(AppSettings original, AppSettings updated) =>
        original.MoveBreakEnabled != updated.MoveBreakEnabled ||
        original.MoveBreakIntervalMinutes != updated.MoveBreakIntervalMinutes ||
        original.MoveBreakDurationSeconds != updated.MoveBreakDurationSeconds;
}
