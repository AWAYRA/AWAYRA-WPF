using Awayra.App.Services;
using Awayra.App.Tests.Support;
using Awayra.App.ViewModels;
using Awayra.Core.Localization;
using Awayra.Core.Models;

namespace Awayra.App.Tests;

/// <summary>
/// Guards against the two ways the UI used to accept an action and then quietly do something else:
/// a settings save that dropped a number WPF could not read, and an overlay Complete button that let
/// a user past a break they had asked not to be able to skip.
/// </summary>
[TestClass]
public sealed class SettingsAndOverlayGuardTests
{
    [TestMethod]
    public void Save_IsRefusedWhileAFieldCannotBeRead()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var closed = false;
            var viewModel = new SettingsViewModel(host, _ => closed = true);

            var originalInterval = host.Settings.EyeResetIntervalMinutes;
            viewModel.SetFieldReadFailure(nameof(SettingsViewModel.EyeResetIntervalMinutes), failed: true);
            Assert.IsTrue(viewModel.HasUnreadableFields);

            viewModel.SaveCommand.Execute(null);

            Assert.IsFalse(closed, "The window must stay open so the user can fix the field.");
            Assert.AreEqual(1, viewModel.ValidationErrors.Count);
            StringAssert.Contains(viewModel.ValidationErrors[0], "Eye Reset interval");
            Assert.AreEqual(originalInterval, host.Settings.EyeResetIntervalMinutes);
        });
    }

    [TestMethod]
    public void Save_ProceedsOnceTheFieldBecomesReadableAgain()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var closedWithSave = false;
            var viewModel = new SettingsViewModel(host, saved => closedWithSave = saved);

            viewModel.SetFieldReadFailure(nameof(SettingsViewModel.EyeResetIntervalMinutes), failed: true);
            viewModel.SetFieldReadFailure(nameof(SettingsViewModel.EyeResetIntervalMinutes), failed: false);
            viewModel.EyeResetIntervalMinutes = 27;

            viewModel.SaveCommand.Execute(null);

            Assert.IsFalse(viewModel.HasUnreadableFields);
            Assert.AreEqual(0, viewModel.ValidationErrors.Count);
            Assert.IsTrue(closedWithSave);
            Assert.AreEqual(27, host.Settings.EyeResetIntervalMinutes);
        });
    }

    [TestMethod]
    public void WorkHours_RoundTripAsTwentyFourHourText()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var viewModel = new SettingsViewModel(host, _ => { })
            {
                WorkHoursEnabled = true,
                WorkStart = "07:45",
                WorkEnd = "19:15"
            };

            viewModel.SaveCommand.Execute(null);

            Assert.AreEqual(0, viewModel.ValidationErrors.Count);
            Assert.AreEqual(new TimeOnly(7, 45), host.Settings.WorkStart);
            Assert.AreEqual(new TimeOnly(19, 15), host.Settings.WorkEnd);
        });
    }

    [TestMethod]
    public void Overlay_CompleteStaysDisabledUntilTheEndWhenSkippingIsOff()
    {
        StaTestContext.Run(() =>
        {
            var localization = new LocalizationService();
            localization.Apply();

            var settings = AppSettings.CreateDefault();
            settings.AllowSkip = false;
            var viewModel = new OverlayViewModel();

            viewModel.ConfigureEye(
                new BreakStartedEventArgs { BreakType = BreakType.Eye, DurationSeconds = 20, ActivityIndex = 0 },
                settings,
                localization,
                snapshot: null);

            Assert.IsFalse(viewModel.ShowSkip);
            Assert.IsFalse(viewModel.CanComplete, "Complete would otherwise be a way around a disabled Skip.");

            viewModel.UpdateRemaining(TimeSpan.FromSeconds(5));
            Assert.IsFalse(viewModel.CanComplete);

            viewModel.UpdateRemaining(TimeSpan.Zero);
            Assert.IsTrue(viewModel.CanComplete);
        });
    }

    [TestMethod]
    public void Overlay_CompleteStaysAvailableWhenSkippingIsAllowed()
    {
        StaTestContext.Run(() =>
        {
            var localization = new LocalizationService();
            localization.Apply();

            var viewModel = new OverlayViewModel();
            viewModel.ConfigureMove(
                new BreakStartedEventArgs { BreakType = BreakType.Move, DurationSeconds = 60, ActivityIndex = 0 },
                AppSettings.CreateDefault(),
                localization,
                snapshot: null);

            viewModel.UpdateRemaining(TimeSpan.FromSeconds(59));

            Assert.IsTrue(viewModel.CanComplete);
        });
    }

    [TestMethod]
    public void Overlay_ButtonTextComesFromResourcesNotHardCodedStrings()
    {
        StaTestContext.Run(() =>
        {
            var localization = new LocalizationService();
            localization.Apply();

            var viewModel = new OverlayViewModel();
            viewModel.ConfigureEye(
                new BreakStartedEventArgs { BreakType = BreakType.Eye, DurationSeconds = 20, ActivityIndex = 0 },
                AppSettings.CreateDefault(),
                localization,
                snapshot: null);

            Assert.AreEqual(localization.Get(StringKeys.Skip), viewModel.SkipText);
            Assert.AreEqual(localization.Get(StringKeys.Snooze), viewModel.SnoozeText);
            Assert.AreEqual(localization.Get(StringKeys.Complete), viewModel.CompleteText);
            Assert.AreEqual(localization.Get(StringKeys.SoundMuted), viewModel.SoundToggleText);

            viewModel.SetSoundMuted(false);
            Assert.AreEqual(localization.Get(StringKeys.SoundOn), viewModel.SoundToggleText);
        });
    }

    [TestMethod]
    public void Host_RepairsAnOutOfRangeSettingsFileInsteadOfResettingIt()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();

            var stored = AppSettings.CreateDefault();
            stored.EyeResetIntervalMinutes = 25;
            stored.EyeResetDurationSeconds = 90_000;
            stored.CloseToTray = false;
            stored.BreakSoundVolume = 42;

            var host = WpfTestHost.CreateHost(stored);

            Assert.AreEqual(25, host.Settings.EyeResetIntervalMinutes);
            Assert.IsFalse(host.Settings.CloseToTray);
            Assert.AreEqual(42, host.Settings.BreakSoundVolume);
            Assert.IsTrue(Core.Services.SettingsValidator.IsValid(host.Settings));
        });
    }
}
