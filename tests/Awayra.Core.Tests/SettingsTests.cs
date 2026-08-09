using System.Text.Json;
using Awayra.Core.Models;
using Awayra.Core.Persistence;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class SettingsTests
{
    [TestMethod]
    public void Defaults_MatchSpecification()
    {
        var settings = AppSettings.CreateDefault();

        Assert.IsTrue(settings.EyeResetEnabled);
        Assert.AreEqual(20, settings.EyeResetIntervalMinutes);
        Assert.AreEqual(20, settings.EyeResetDurationSeconds);
        Assert.IsTrue(settings.MoveBreakEnabled);
        Assert.AreEqual(45, settings.MoveBreakIntervalMinutes);
        Assert.AreEqual(60, settings.MoveBreakDurationSeconds);
        Assert.AreEqual(5, settings.SnoozeDurationMinutes);
        Assert.IsTrue(settings.AllowSkip);
        Assert.IsTrue(settings.AllowSnooze);
        Assert.IsTrue(settings.PauseWhileIdle);
        Assert.AreEqual(5, settings.IdleThresholdMinutes);
        Assert.IsFalse(settings.WorkHoursEnabled);
        Assert.IsFalse(settings.RunAtStartup);
        Assert.IsFalse(settings.StartMinimized);
        Assert.IsTrue(settings.CloseToTray);
        Assert.AreEqual(OverlayGlassSettings.DefaultGlassClarity, settings.GlassClarity);
        Assert.IsFalse(settings.ReducedMotion);
        Assert.IsTrue(settings.BreakAnimationEnabled, "The guided break exercise should be on by default.");
    }

    [TestMethod]
    public void Validation_RejectsInvalidIntervals()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 0;

        Assert.IsFalse(SettingsValidator.IsValid(settings));
        Assert.IsTrue(SettingsValidator.Validate(settings).Contains("EyeResetIntervalInvalid"));
    }

    [TestMethod]
    public void Validation_RejectsInvalidGlassClarity()
    {
        var settings = AppSettings.CreateDefault();
        settings.GlassClarity = 151;

        Assert.IsFalse(SettingsValidator.IsValid(settings));
        Assert.IsTrue(SettingsValidator.Validate(settings).Contains("GlassClarityInvalid"));
    }

    [TestMethod]
    public void Validation_AllowsTenSecondDuration()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetDurationSeconds = 10;
        settings.MoveBreakDurationSeconds = 10;

        Assert.IsTrue(SettingsValidator.IsValid(settings));
    }

    [TestMethod]
    public void SaveAndLoad_RoundTrips()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 15;
        settings.GlassClarity = 125;

        var json = JsonSerializer.Serialize(settings, JsonOptions.Create());
        var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions.Create());

        Assert.IsNotNull(loaded);
        Assert.AreEqual(15, loaded.EyeResetIntervalMinutes);
        Assert.AreEqual(125, loaded.GlassClarity);
    }

    [TestMethod]
    public void PartialMalformedJson_MigratesLegacyOverlayOpacity()
    {
        const string json = "{ \"eyeResetEnabled\": false, \"eyeResetIntervalMinutes\": \"bad\", \"overlayOpacity\": 0.82 }";
        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.IsFalse(recovered.EyeResetEnabled);
        Assert.AreEqual(40, recovered.GlassClarity);
    }

    [TestMethod]
    public void LegacyOverlayOpacity_MigratesToGlassClarity()
    {
        const string json = "{ \"overlayOpacity\": 0.5 }";
        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.AreEqual(100, recovered.GlassClarity);
    }

    [TestMethod]
    public void MalformedGlassClarity_FallsBackToDefault()
    {
        const string json = "{ \"glassClarity\": \"bad\" }";
        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.AreEqual(OverlayGlassSettings.DefaultGlassClarity, recovered.GlassClarity);
    }

    [TestMethod]
    public void OutOfRangeGlassClarity_IsClampedToRange()
    {
        // Repair clamps rather than resets, so a value that simply overshoots keeps the user's
        // intent ("as clear as possible") instead of snapping back to the middle of the scale.
        Assert.AreEqual(
            OverlayGlassSettings.MaxGlassClarity,
            SettingsRecovery.LoadWithRecovery("{ \"glassClarity\": 200 }").GlassClarity);
        Assert.AreEqual(
            OverlayGlassSettings.MinGlassClarity,
            SettingsRecovery.LoadWithRecovery("{ \"glassClarity\": -40 }").GlassClarity);
    }

    [TestMethod]
    public void LegacyGlassTransparency_MigratesToGlassClarity()
    {
        const string json = "{ \"glassTransparency\": 75 }";
        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.AreEqual(75, recovered.GlassClarity);
    }

    [TestMethod]
    public void LegacyBackgroundVisibility_MigratesToGlassClarity()
    {
        const string json = "{ \"language\": \"Persian\", \"backgroundVisibility\": 22 }";
        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.AreEqual(60, recovered.GlassClarity);
    }

    [TestMethod]
    public void Save_RemovesObsoleteLanguageProperty()
    {
        var settings = AppSettings.CreateDefault();
        var json = JsonSerializer.Serialize(settings, JsonOptions.Create());

        Assert.IsFalse(json.Contains("language", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("backgroundVisibility", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("glassTransparency", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void FullyCorruptJson_UsesDefaults()
    {
        var recovered = SettingsRecovery.LoadWithRecovery("not json at all");
        Assert.AreEqual(AppSettings.CreateDefault().EyeResetIntervalMinutes, recovered.EyeResetIntervalMinutes);
    }

    [TestMethod]
    public void StartMinimizedAndCloseToTray_RemainIndependent()
    {
        var settings = AppSettings.CreateDefault();
        settings.StartMinimized = true;
        settings.CloseToTray = false;

        Assert.IsTrue(settings.StartMinimized);
        Assert.IsFalse(settings.CloseToTray);

        settings.StartMinimized = false;
        settings.CloseToTray = true;
        Assert.IsFalse(settings.StartMinimized);
        Assert.IsTrue(settings.CloseToTray);
    }

    [TestMethod]
    public void PartialMalformedJson_PreservesUnrelatedSettings()
    {
        const string json = "{ \"eyeResetEnabled\": false, \"eyeResetIntervalMinutes\": \"bad\", \"overlayOpacity\": 0.82 }";
        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.IsFalse(recovered.EyeResetEnabled);
        Assert.AreEqual(40, recovered.GlassClarity);
        Assert.AreEqual(AppSettings.CreateDefault().EyeResetIntervalMinutes, recovered.EyeResetIntervalMinutes);
        Assert.AreEqual(AppSettings.CreateDefault().MoveBreakIntervalMinutes, recovered.MoveBreakIntervalMinutes);
    }

    [TestMethod]
    public void MissingStartMinimized_DefaultsFalse()
    {
        const string json = "{ \"closeToTray\": true }";
        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.IsFalse(recovered.StartMinimized);
        Assert.IsTrue(recovered.CloseToTray);
    }

    [TestMethod]
    public void SchemaVersion_Preserved()
    {
        var settings = AppSettings.CreateDefault();
        Assert.AreEqual(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }
}
