using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class SettingsRecoveryRegressionTests
{
    [TestMethod]
    public void Normalize_RepairsOneInvalidFieldWithoutLosingUnrelatedPreferences()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetDurationSeconds = 9_999;
        settings.RunAtStartup = true;
        settings.StartMinimized = true;
        settings.CloseToTray = false;
        settings.EyeResetIntervalMinutes = 35;

        var normalized = SettingsRecovery.Normalize(settings);

        Assert.AreEqual(AppSettings.CreateDefault().EyeResetDurationSeconds, normalized.EyeResetDurationSeconds);
        Assert.AreEqual(35, normalized.EyeResetIntervalMinutes);
        Assert.IsTrue(normalized.RunAtStartup);
        Assert.IsTrue(normalized.StartMinimized);
        Assert.IsFalse(normalized.CloseToTray);
        Assert.IsTrue(SettingsValidator.IsValid(normalized));
    }

    [TestMethod]
    public void InvalidTimeJson_RecoversOtherValidFields()
    {
        const string json = """
            {
              "workHoursEnabled": true,
              "workStart": "99:99",
              "workEnd": "18:00",
              "runAtStartup": true,
              "startMinimized": true,
              "eyeResetIntervalMinutes": 30
            }
            """;

        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.AreEqual(AppSettings.CreateDefault().WorkStart, recovered.WorkStart);
        Assert.AreEqual(new TimeOnly(18, 0), recovered.WorkEnd);
        Assert.IsTrue(recovered.RunAtStartup);
        Assert.IsTrue(recovered.StartMinimized);
        Assert.AreEqual(30, recovered.EyeResetIntervalMinutes);
        Assert.IsTrue(SettingsValidator.IsValid(recovered));
    }
}
