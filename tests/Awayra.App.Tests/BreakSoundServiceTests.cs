using System.Text;
using System.Windows.Threading;
using Awayra.App.Services;
using Awayra.App.Tests.Support;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;

namespace Awayra.App.Tests;

[TestClass]
public sealed class BreakSoundServiceTests
{
    [TestMethod]
    public void ActiveBreak_PlaysMutesUnmutesAndStops()
    {
        StaTestContext.Run(() =>
        {
            var player = new RecordingTonePlayer();
            using var service = new BreakSoundService(
                Dispatcher.CurrentDispatcher,
                new TestLogger(),
                player);
            var settings = AppSettings.CreateDefault();
            settings.EyeBreakSoundEnabled = true;
            settings.BreakSoundTheme = BreakSoundTheme.GentleChime;
            settings.BreakSoundVolume = 23;
            settings.BreakSoundRepeatSeconds = 1;

            service.StartBreak(BreakType.Eye, settings);

            Assert.IsTrue(service.IsSessionActive);
            Assert.IsFalse(service.IsMuted);
            Assert.AreEqual(BreakType.Eye, service.ActiveBreakType);
            Assert.AreEqual(1, player.PlayCount);
            Assert.AreEqual(BreakSoundTheme.GentleChime, player.LastTheme);
            Assert.AreEqual(23, player.LastVolume);

            Assert.IsTrue(service.ToggleMute());
            Assert.IsTrue(service.IsMuted);
            Assert.IsTrue(player.StopCount >= 1);

            Assert.IsFalse(service.ToggleMute());
            Assert.IsFalse(service.IsMuted);
            Assert.AreEqual(2, player.PlayCount);

            service.StopBreak();
            Assert.IsFalse(service.IsSessionActive);
            Assert.IsTrue(service.IsMuted);
            Assert.IsNull(service.ActiveBreakType);
        });
    }

    [TestMethod]
    public void DisabledSound_StartsMutedButCanBeEnabledForCurrentBreak()
    {
        StaTestContext.Run(() =>
        {
            var player = new RecordingTonePlayer();
            using var service = new BreakSoundService(
                Dispatcher.CurrentDispatcher,
                new TestLogger(),
                player);
            var settings = AppSettings.CreateDefault();
            settings.MoveBreakSoundEnabled = false;

            service.StartBreak(BreakType.Move, settings);

            Assert.IsTrue(service.IsMuted);
            Assert.AreEqual(0, player.PlayCount);

            service.ToggleMute();
            Assert.IsFalse(service.IsMuted);
            Assert.AreEqual(1, player.PlayCount);
        });
    }

    [TestMethod]
    public void SystemTransition_PausesAndResumesActiveSound()
    {
        StaTestContext.Run(() =>
        {
            var player = new RecordingTonePlayer();
            using var service = new BreakSoundService(
                Dispatcher.CurrentDispatcher,
                new TestLogger(),
                player);
            var settings = AppSettings.CreateDefault();
            settings.EyeBreakSoundEnabled = true;

            service.StartBreak(BreakType.Eye, settings);
            service.PauseForSystemTransition();
            var stopsAfterPause = player.StopCount;
            service.ResumeAfterSystemTransition(settings);

            Assert.IsTrue(stopsAfterPause >= 1);
            Assert.AreEqual(2, player.PlayCount);
        });
    }

    [TestMethod]
    public void ToneGenerator_ProducesDistinctValidWaveFilesForEveryTheme()
    {
        var generated = Enum.GetValues<BreakSoundTheme>()
            .Select(BreakToneGenerator.GenerateWaveBytes)
            .ToArray();

        Assert.AreEqual(6, generated.Length);
        foreach (var wave in generated)
        {
            Assert.IsTrue(wave.Length > 44);
            Assert.AreEqual("RIFF", Encoding.ASCII.GetString(wave, 0, 4));
            Assert.AreEqual("WAVE", Encoding.ASCII.GetString(wave, 8, 4));
        }

        for (var left = 0; left < generated.Length; left++)
        {
            for (var right = left + 1; right < generated.Length; right++)
            {
                Assert.IsFalse(
                    generated[left].SequenceEqual(generated[right]),
                    $"Themes {left} and {right} unexpectedly generated identical audio.");
            }
        }
    }

    [TestMethod]
    public void CalmPiano_IsGeneratedLocallyWithExpectedDuration()
    {
        var wave = BreakToneGenerator.GenerateWaveBytes(BreakSoundTheme.CalmPiano);
        var dataLength = BitConverter.ToInt32(wave, 40);
        var sampleCount = dataLength / sizeof(short);
        var durationSeconds = sampleCount / 44_100d;

        Assert.IsTrue(durationSeconds >= 1.79 && durationSeconds <= 1.81);
        Assert.IsTrue(wave.Skip(44).Any(value => value != 0));
    }

    [TestMethod]
    [DataRow(BreakSoundTheme.MorningDew, 3.2)]
    [DataRow(BreakSoundTheme.StillWater, 4.0)]
    public void MelodicThemes_StartFromSilenceAndReturnToIt(BreakSoundTheme theme, double expectedSeconds)
    {
        var samples = ReadSamples(BreakToneGenerator.GenerateWaveBytes(theme));
        var duration = samples.Length / 44_100d;
        Assert.IsTrue(
            Math.Abs(duration - expectedSeconds) < 0.02,
            $"{theme} should last {expectedSeconds}s but lasted {duration:0.000}s.");

        // A startling sound is one that is already loud in its first few milliseconds. These two
        // must swell in from silence instead.
        var peak = samples.Max(Math.Abs);
        var firstTenMs = samples.Take(441).Max(Math.Abs);
        Assert.IsTrue(
            firstTenMs < peak * 0.06,
            $"{theme} opens at {firstTenMs / (double)peak:P1} of peak, which is an abrupt onset.");

        // ...and recede again rather than being cut off mid-note.
        var lastFiftyMs = samples.Skip(samples.Length - 2205).Max(Math.Abs);
        Assert.IsTrue(
            lastFiftyMs < peak * 0.12,
            $"{theme} ends at {lastFiftyMs / (double)peak:P1} of peak, which is an abrupt cut.");

        // Deliberately quieter than the alert themes.
        var bellPeak = ReadSamples(BreakToneGenerator.GenerateWaveBytes(BreakSoundTheme.SoftBell)).Max(Math.Abs);
        Assert.IsTrue(peak < bellPeak, $"{theme} should be softer than Soft bell.");
    }

    private static short[] ReadSamples(byte[] wave)
    {
        var dataLength = BitConverter.ToInt32(wave, 40);
        var samples = new short[dataLength / sizeof(short)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BitConverter.ToInt16(wave, 44 + (i * sizeof(short)));
        }

        return samples;
    }

    private sealed class RecordingTonePlayer : IBreakTonePlayer
    {
        public int PlayCount { get; private set; }
        public int StopCount { get; private set; }
        public BreakSoundTheme? LastTheme { get; private set; }
        public int LastVolume { get; private set; }

        public void Play(BreakSoundTheme theme, int volumePercent)
        {
            PlayCount++;
            LastTheme = theme;
            LastVolume = volumePercent;
        }

        public void Stop() => StopCount++;
        public void Dispose() { }
    }

    private sealed class TestLogger : IAppLogger
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
        public Task FlushAsync() => Task.CompletedTask;
    }
}