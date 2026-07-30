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
        SchedulerStatus.PausedIdle => Get(StringKeys.StatusPausedIdle),
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
        "WorkHoursTimeInvalid" => Get(StringKeys.ValidationWorkHoursTimeInvalid),
        "WorkHoursRangeInvalid" => Get(StringKeys.ValidationWorkHoursRangeInvalid),
        _ => errorKey
    };
}
