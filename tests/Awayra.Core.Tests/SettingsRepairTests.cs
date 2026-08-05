using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

/// <summary>
/// A settings file that holds one unusable number is still valid JSON, so it never reaches the
/// corrupt-file recovery path. Repair has to bring that single field back into range without costing
/// the user everything else they had configured.
/// </summary>
[TestClass]
public sealed class SettingsRepairTests
{
    [TestMethod]
    public void Repair_KeepsEveryValidSettingAroundOneBrokenValue()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 25;
        settings.EyeResetDurationSeconds = 90_000;
        settings.WorkHoursEnabled = true;
        settings.WorkStart = new TimeOnly(8, 30);
        settings.WorkEnd = new TimeOnly(16, 45);
        settings.CloseToTray = false;
        settings.BreakSoundVolume = 42;
        settings.BreakSoundTheme = BreakSoundTheme.StillWater;
        settings.GlassClarity = 130;

        var repaired = SettingsRecovery.Repair(settings);

        Assert.IsTrue(SettingsValidator.IsValid(repaired));
        Assert.AreEqual(25, repaired.EyeResetIntervalMinutes);
        Assert.AreEqual(new TimeOnly(8, 30), repaired.WorkStart);
        Assert.AreEqual(new TimeOnly(16, 45), repaired.WorkEnd);
        Assert.IsFalse(repaired.CloseToTray);
        Assert.AreEqual(42, repaired.BreakSoundVolume);
        Assert.AreEqual(BreakSoundTheme.StillWater, repaired.BreakSoundTheme);
        Assert.AreEqual(130, repaired.GlassClarity);
        Assert.AreEqual(SettingsValidator.MaxDurationSeconds, repaired.EyeResetDurationSeconds);
    }

    [TestMethod]
    public void Repair_ClampsDurationToTheIntervalItInterrupts()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 1;
        settings.EyeResetDurationSeconds = 600;

        var repaired = SettingsRecovery.Repair(settings);

        Assert.AreEqual(60, repaired.EyeResetDurationSeconds);
        Assert.IsTrue(SettingsValidator.IsValid(repaired));
    }

    [TestMethod]
    public void Repair_BringsEveryOutOfRangeFieldBackWithoutResetting()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 0;
        settings.MoveBreakIntervalMinutes = 10_000;
        settings.SnoozeDurationMinutes = 999;
        settings.IdleThresholdMinutes = 0;
        settings.BreakSoundVolume = 5_000;
        settings.BreakSoundRepeatSeconds = 0;
        settings.BreakSoundTheme = (BreakSoundTheme)99;

        var repaired = SettingsRecovery.Repair(settings);

        Assert.IsTrue(SettingsValidator.IsValid(repaired));
        Assert.AreEqual(SettingsValidator.MinIntervalMinutes, repaired.EyeResetIntervalMinutes);
        Assert.AreEqual(SettingsValidator.MaxIntervalMinutes, repaired.MoveBreakIntervalMinutes);
        Assert.AreEqual(SettingsValidator.MaxSnoozeMinutes, repaired.SnoozeDurationMinutes);
        Assert.AreEqual(SettingsValidator.MinIdleMinutes, repaired.IdleThresholdMinutes);
        Assert.AreEqual(SettingsValidator.MaxBreakSoundVolume, repaired.BreakSoundVolume);
        Assert.AreEqual(SettingsValidator.MinBreakSoundRepeatSeconds, repaired.BreakSoundRepeatSeconds);
        Assert.AreEqual(AppSettings.CreateDefault().BreakSoundTheme, repaired.BreakSoundTheme);
    }

    [TestMethod]
    public void Repair_ResetsAnEmptyWorkHoursRange()
    {
        var settings = AppSettings.CreateDefault();
        settings.WorkHoursEnabled = true;
        settings.WorkStart = new TimeOnly(9, 0);
        settings.WorkEnd = new TimeOnly(9, 0);

        var repaired = SettingsRecovery.Repair(settings);

        Assert.IsTrue(SettingsValidator.IsValid(repaired));
        Assert.AreNotEqual(repaired.WorkStart, repaired.WorkEnd);
    }

    [TestMethod]
    public void Repair_LeavesAValidFileUntouched()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 33;
        settings.GlassClarity = 7;

        var repaired = SettingsRecovery.Repair(settings);

        Assert.AreEqual(33, repaired.EyeResetIntervalMinutes);
        Assert.AreEqual(7, repaired.GlassClarity);
    }

    [TestMethod]
    public void Recovery_MistypedBooleanDoesNotAbortLaterMigrations()
    {
        // A string "true" used to throw out of the property loop, taking every later property and
        // the legacy migrations with it.
        const string json = """
        { "eyeResetEnabled": "true", "glassTransparency": 40, "moveBreakEnabled": false }
        """;

        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.IsTrue(recovered.EyeResetEnabled);
        Assert.IsFalse(recovered.MoveBreakEnabled);
        Assert.AreEqual(40, recovered.GlassClarity);
    }

    [TestMethod]
    public void Recovery_UnreadableBooleanLeavesTheRestIntact()
    {
        const string json = """
        { "eyeResetEnabled": 12, "eyeResetIntervalMinutes": 31 }
        """;

        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.AreEqual(31, recovered.EyeResetIntervalMinutes);
    }

    [TestMethod]
    public void WorkHours_RoundTripThroughJsonUnderAnyTimeSeparator()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // fi-FI writes times as 9.00. Persisted values must stay machine readable regardless.
            var finnish = (System.Globalization.CultureInfo)
                System.Globalization.CultureInfo.GetCultureInfo("fi-FI").Clone();
            System.Globalization.CultureInfo.CurrentCulture = finnish;

            var settings = AppSettings.CreateDefault();
            settings.WorkStart = new TimeOnly(9, 0);
            settings.WorkEnd = new TimeOnly(18, 30);

            var json = System.Text.Json.JsonSerializer.Serialize(settings, JsonOptions.Create());
            Assert.IsTrue(json.Contains("09:00", StringComparison.Ordinal), json);

            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var reloaded = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, JsonOptions.Create());

            Assert.IsNotNull(reloaded);
            Assert.AreEqual(new TimeOnly(9, 0), reloaded.WorkStart);
            Assert.AreEqual(new TimeOnly(18, 30), reloaded.WorkEnd);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void StatisticsDayKey_IsCalendarIndependent()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // fa-IR defaults to the Persian calendar, which would key today's statistics as 1405-xx.
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("fa-IR");

            var key = StatisticsService.GetDayKey(new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero));

            Assert.AreEqual("2026-08-05", key);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }
}
