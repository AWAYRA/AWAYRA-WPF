using Awayra.App.Tests.Support;
using Awayra.App.ViewModels;
using Awayra.Core.Localization;
using Awayra.Core.Models;

namespace Awayra.App.Tests;

[TestClass]
public sealed class MainViewModelDashboardTests
{
    [TestMethod]
    public void EyeSnooze_ImmediatelyProjectsPauseButtonAndSnoozedStatus()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var viewModel = new MainViewModel(host, () => { });

            host.Scheduler.TriggerNow(BreakType.Eye);
            host.Scheduler.SnoozeActiveBreak();
            viewModel.Refresh();

            var snapshot = host.Scheduler.GetSnapshot();
            Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
            Assert.IsFalse(snapshot.IsPausedManual);
            Assert.IsFalse(viewModel.IsManuallyPaused);
            Assert.AreEqual("Pause", viewModel.PauseResumeText);
            Assert.IsTrue(viewModel.CanPause);
            Assert.IsFalse(viewModel.CanResume);
        });
    }

    [TestMethod]
    public void MoveSnooze_ImmediatelyProjectsPauseButtonAndSnoozedStatus()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var viewModel = new MainViewModel(host, () => { });

            host.Scheduler.TriggerNow(BreakType.Move);
            host.Scheduler.SnoozeActiveBreak();
            viewModel.Refresh();

            var snapshot = host.Scheduler.GetSnapshot();
            Assert.AreEqual(SchedulerStatus.Snoozed, snapshot.Status);
            Assert.IsFalse(snapshot.IsPausedManual);
            Assert.AreEqual("Pause", viewModel.PauseResumeText);
        });
    }

    [TestMethod]
    public void PauseWhileSnoozed_ProjectsResumeWithoutOpeningSettings()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var viewModel = new MainViewModel(host, () => { });

            host.Scheduler.TriggerNow(BreakType.Eye);
            host.Scheduler.SnoozeActiveBreak();
            host.Scheduler.Pause();
            viewModel.Refresh();

            // The dashboard reports the pause the user chose, not the snooze underneath it.
            Assert.AreEqual(SchedulerStatus.PausedManual, host.Scheduler.GetSnapshot().Status);
            Assert.IsTrue(viewModel.IsManuallyPaused);
            Assert.AreEqual("Resume", viewModel.PauseResumeText);
            Assert.IsFalse(viewModel.CanPause);
            Assert.IsTrue(viewModel.CanResume);
        });
    }

    [TestMethod]
    public void StateChangedAfterSnooze_RefreshesPauseProjectionOnUiThread()
    {
        StaTestContext.Run(() =>
        {
            WpfTestHost.EnsureApplicationResources();
            var host = WpfTestHost.CreateHost();
            var viewModel = new MainViewModel(host, () => { });

            host.Scheduler.TriggerNow(BreakType.Eye);
            host.Scheduler.SnoozeActiveBreak();

            Assert.AreEqual(SchedulerStatus.Snoozed, host.Scheduler.GetSnapshot().Status);
            Assert.AreEqual("Pause", viewModel.PauseResumeText);
            Assert.IsFalse(viewModel.IsManuallyPaused);
        });
    }
}
