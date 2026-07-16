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
        Assert.AreEqual(0.82, settings.OverlayOpacity, 0.001);
        Assert.IsFalse(settings.ReducedMotion);
        Assert.AreEqual(AppLanguage.Auto, settings.Language);
        Assert.AreEqual(AppTheme.Dark, settings.Theme);
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
    public void SaveAndLoad_RoundTrips()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 15;
        var json = JsonSerializer.Serialize(settings, JsonOptions.Create());
        var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions.Create());

        Assert.IsNotNull(loaded);
        Assert.AreEqual(15, loaded.EyeResetIntervalMinutes);
    }

    [TestMethod]
    public void PartialMalformedJson_ApplyDocumentPropertiesDirectly()
    {
        const string json = "{ \"eyeResetEnabled\": false, \"eyeResetIntervalMinutes\": \"bad\", \"overlayOpacity\": 0.5 }";
        using var doc = JsonDocument.Parse(json);
        var settings = AppSettings.CreateDefault();
        SettingsRecovery.ApplyDocumentProperties(doc.RootElement, settings);

        Assert.IsFalse(settings.EyeResetEnabled);
        Assert.AreEqual(0.5, settings.OverlayOpacity, 0.001);
    }

    [TestMethod]
    public void PartialMalformedJson_RecoversKnownFields()
    {
        const string json = "{ \"eyeResetEnabled\": false, \"eyeResetIntervalMinutes\": \"bad\", \"overlayOpacity\": 0.5 }";
        var recovered = SettingsRecovery.LoadWithRecovery(json);

        Assert.IsFalse(recovered.EyeResetEnabled);
        Assert.AreEqual(0.5, recovered.OverlayOpacity, 0.001);
    }

    [TestMethod]
    public void FullyCorruptJson_UsesDefaults()
    {
        var recovered = SettingsRecovery.LoadWithRecovery("not json at all");
        Assert.AreEqual(AppSettings.CreateDefault().EyeResetIntervalMinutes, recovered.EyeResetIntervalMinutes);
    }

    [TestMethod]
    public void SchemaVersion_Preserved()
    {
        var settings = AppSettings.CreateDefault();
        Assert.AreEqual(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }
}
