using Awayra.Core.Models;

namespace Awayra.Core.Services;

public static class SettingsValidator
{
    public const int MinIntervalMinutes = 1;
    public const int MaxIntervalMinutes = 480;
    public const int MinDurationSeconds = 5;
    public const int MaxDurationSeconds = 600;
    public const int MinSnoozeMinutes = 1;
    public const int MaxSnoozeMinutes = 60;
    public const int MinIdleMinutes = 1;
    public const int MaxIdleMinutes = 120;
    public const double MinOpacity = 0.3;
    public const double MaxOpacity = 1.0;

    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        var errors = new List<string>();

        if (settings.EyeResetIntervalMinutes < MinIntervalMinutes || settings.EyeResetIntervalMinutes > MaxIntervalMinutes)
        {
            errors.Add("EyeResetIntervalInvalid");
        }

        if (settings.EyeResetDurationSeconds < MinDurationSeconds || settings.EyeResetDurationSeconds > MaxDurationSeconds)
        {
            errors.Add("EyeResetDurationInvalid");
        }

        if (settings.EyeResetDurationSeconds > settings.EyeResetIntervalMinutes * 60)
        {
            errors.Add("EyeResetDurationExceedsInterval");
        }

        if (settings.MoveBreakIntervalMinutes < MinIntervalMinutes || settings.MoveBreakIntervalMinutes > MaxIntervalMinutes)
        {
            errors.Add("MoveBreakIntervalInvalid");
        }

        if (settings.MoveBreakDurationSeconds < MinDurationSeconds || settings.MoveBreakDurationSeconds > MaxDurationSeconds)
        {
            errors.Add("MoveBreakDurationInvalid");
        }

        if (settings.MoveBreakDurationSeconds > settings.MoveBreakIntervalMinutes * 60)
        {
            errors.Add("MoveBreakDurationExceedsInterval");
        }

        if (settings.SnoozeDurationMinutes < MinSnoozeMinutes || settings.SnoozeDurationMinutes > MaxSnoozeMinutes)
        {
            errors.Add("SnoozeDurationInvalid");
        }

        if (settings.IdleThresholdMinutes < MinIdleMinutes || settings.IdleThresholdMinutes > MaxIdleMinutes)
        {
            errors.Add("IdleThresholdInvalid");
        }

        if (settings.OverlayOpacity < MinOpacity || settings.OverlayOpacity > MaxOpacity)
        {
            errors.Add("OverlayOpacityInvalid");
        }

        if (settings.WorkHoursEnabled && settings.WorkStart == settings.WorkEnd)
        {
            errors.Add("WorkHoursRangeInvalid");
        }

        return errors;
    }

    public static bool IsValid(AppSettings settings) => Validate(settings).Count == 0;
}
