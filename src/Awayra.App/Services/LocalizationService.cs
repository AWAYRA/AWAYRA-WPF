using System.Globalization;
using Awayra.Core.Localization;
using Awayra.Core.Models;
using Awayra.Core.Services;
using Awayra.App.Resources;

namespace Awayra.App.Services;

public sealed class LocalizationService
{
    public string CurrentCultureName { get; private set; } = "en";

    public void Apply()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        Strings.Culture = culture;
        CurrentCultureName = "en";
    }

    public string Get(string key) => Strings.Get(key);

    public string GetStatus(SchedulerStatus status) => status switch
    {
        SchedulerStatus.Running => Get(StringKeys.StatusRunning),
        SchedulerStatus.PausedManual => Get(StringKeys.StatusPaused),
        SchedulerStatus.Idle => Get(StringKeys.StatusIdle),
        SchedulerStatus.ConfigurationPaused => Get(StringKeys.StatusConfigurationPaused),
        SchedulerStatus.OutsideWorkHours => Get(StringKeys.StatusOutsideWorkHours),
        SchedulerStatus.BreakActive => Get(StringKeys.StatusBreakActive),
        SchedulerStatus.Snoozed => Get(StringKeys.StatusSnoozed),
        SchedulerStatus.Disabled => Get(StringKeys.StatusDisabled),
        _ => Get(StringKeys.StatusRunning)
    };

    public string GetMoveActivity(int index) => (index % BreakScheduler.MoveActivityCount) switch
    {
        0 => Get(StringKeys.MoveActivityStand),
        1 => Get(StringKeys.MoveActivityWalk),
        2 => Get(StringKeys.MoveActivityShoulders),
        3 => Get(StringKeys.MoveActivityNeck),
        _ => Get(StringKeys.MoveActivityStretch)
    };

    public string GetValidationMessage(string errorKey) => errorKey switch
    {
        "EyeResetIntervalInvalid" => Get(StringKeys.ValidationEyeResetIntervalInvalid),
        "EyeResetDurationInvalid" => Get(StringKeys.ValidationEyeResetDurationInvalid),
        "MoveBreakIntervalInvalid" => Get(StringKeys.ValidationMoveBreakIntervalInvalid),
        "MoveBreakDurationInvalid" => Get(StringKeys.ValidationMoveBreakDurationInvalid),
        "SnoozeDurationInvalid" => Get(StringKeys.ValidationSnoozeDurationInvalid),
        "IdleThresholdInvalid" => Get(StringKeys.ValidationIdleThresholdInvalid),
        "GlassClarityInvalid" => Get(StringKeys.ValidationGlassClarityInvalid),
        "WorkHoursRangeInvalid" => Get(StringKeys.ValidationWorkHoursRangeInvalid),
        "BreakSoundVolumeInvalid" => "Sound volume must be between 0 and 100.",
        "BreakSoundRepeatInvalid" => "Sound repeat interval must be between 1 and 60 seconds.",
        "BreakSoundThemeInvalid" => "Select a valid break sound.",
        "WorkHoursFormatInvalid" => "Work hours must use 24-hour HH:mm format, for example 09:00.",
        "NumericFieldFormatInvalid" => "These fields need a whole number and were not saved: {0}.",
        "SettingsSaveFailed" => "Your settings could not be saved: {0}",
        _ => errorKey
    };
}
