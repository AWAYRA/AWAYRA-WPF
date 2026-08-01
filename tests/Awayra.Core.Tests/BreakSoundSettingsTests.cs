using System.Text.Json;
using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.Core.Tests;

[TestClass]
public sealed class BreakSoundSettingsTests
{
    [TestMethod]
    public void Defaults_AreQuietAndSafe()
    {
        var settings = AppSettings.CreateDefault();

        Assert.IsFalse(settings.EyeBreakSoundEnabled);
        Assert.IsFalse(settings.MoveBreakSoundEnabled);
        Assert.AreEqual(BreakSoundTheme.SoftBell, settings.BreakSoundTheme);
        Assert.AreEqual(15, settings.BreakSoundVolume);
        Assert.AreEqual(2, settings.BreakSoundRepeatSeconds);
        Assert.IsTrue(SettingsValidator.IsValid(settings));
    }

    [TestMethod]
    public void LegacyJson_ReceivesNewPropertyDefaults()
    {
        const string legacyJson = "{\"EyeResetEnabled\":true,\"MoveBreakEnabled\":true}";

        var settings = JsonSerializer.Deserialize<AppSettings>(legacyJson);

        Assert.IsNotNull(settings);
        Assert.IsFalse(settings.EyeBreakSoundEnabled);
        Assert.IsFalse(settings.MoveBreakSoundEnabled);
        Assert.AreEqual(15, settings.BreakSoundVolume);
        Assert.AreEqual(2, settings.BreakSoundRepeatSeconds);
        Assert.AreEqual(BreakSoundTheme.SoftBell, settings.BreakSoundTheme);
    }

    [TestMethod]
    public void RepeatInterval_AllowsOneSecondButRejectsZero()
    {
        var settings = AppSettings.CreateDefault();
        settings.BreakSoundRepeatSeconds = 1;
        Assert.IsTrue(SettingsValidator.IsValid(settings));

        settings.BreakSoundRepeatSeconds = 0;
        CollectionAssert.Contains(SettingsValidator.Validate(settings).ToList(), "BreakSoundRepeatInvalid");
    }

    [TestMethod]
    public void Volume_AllowsFullRangeAndRejectsOverflow()
    {
        var settings = AppSettings.CreateDefault();
        settings.BreakSoundVolume = 0;
        Assert.IsTrue(SettingsValidator.IsValid(settings));

        settings.BreakSoundVolume = 100;
        Assert.IsTrue(SettingsValidator.IsValid(settings));

        settings.BreakSoundVolume = 101;
        CollectionAssert.Contains(SettingsValidator.Validate(settings).ToList(), "BreakSoundVolumeInvalid");
    }

    [TestMethod]
    public void Copy_PreservesSoundSettingsWithoutSharingMutation()
    {
        var settings = AppSettings.CreateDefault();
        settings.EyeBreakSoundEnabled = true;
        settings.MoveBreakSoundEnabled = true;
        settings.BreakSoundTheme = BreakSoundTheme.CalmDrop;
        settings.BreakSoundVolume = 42;
        settings.BreakSoundRepeatSeconds = 1;

        var copy = settings.Copy();
        copy.BreakSoundVolume = 7;

        Assert.IsTrue(copy.EyeBreakSoundEnabled);
        Assert.IsTrue(copy.MoveBreakSoundEnabled);
        Assert.AreEqual(BreakSoundTheme.CalmDrop, copy.BreakSoundTheme);
        Assert.AreEqual(1, copy.BreakSoundRepeatSeconds);
        Assert.AreEqual(42, settings.BreakSoundVolume);
        Assert.AreEqual(7, copy.BreakSoundVolume);
    }
}
