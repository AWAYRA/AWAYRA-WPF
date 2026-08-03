using System.IO.Compression;
using System.Text.Json;
using Awayra.App.Services;

namespace Awayra.App.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DisplayDiagnosticsServiceTests
{
    [TestMethod]
    [Timeout(120_000)]
    public async Task CaptureBlinkReportAsync_WritesStrictTimelineAndCompleteZip()
    {
        var previousDataRoot = AppPaths.OverrideDataRoot;
        var dataRoot = Path.Combine(Path.GetTempPath(), "Awayra.DisplayDiagnostics.Tests", Guid.NewGuid().ToString("N"));
        AppPaths.OverrideDataRoot = dataRoot;

        try
        {
            AppPaths.EnsureDataRoot();
            using var logger = new FileLogger(AppPaths.LogFilePath);
            logger.Info("Display diagnostic bundle test started.");

            string reportPath;
            using (var recorder = new DisplayDiagnosticsService(logger))
            {
                recorder.Record("test", "before_blink", new { value = 42 });
                await recorder.FlushAsync().ConfigureAwait(false);

                var lines = await File.ReadAllLinesAsync(AppPaths.DisplayTimelinePath).ConfigureAwait(false);
                Assert.HasCount(1, lines);
                using (var timelineEvent = JsonDocument.Parse(lines[0]))
                {
                    Assert.AreEqual("before_blink", timelineEvent.RootElement.GetProperty("eventName").GetString());
                    Assert.AreEqual(42, timelineEvent.RootElement.GetProperty("data").GetProperty("value").GetInt32());
                    Assert.IsTrue(timelineEvent.RootElement.GetProperty("monotonicMilliseconds").GetDouble() >= 0);
                }

                reportPath = await recorder.CaptureBlinkReportAsync().ConfigureAwait(false);
            }

            Assert.IsTrue(File.Exists(reportPath), $"Diagnostic ZIP was not created: {reportPath}");
            using var archive = ZipFile.OpenRead(reportPath);
            var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var expectedEntry in new[]
                     {
                         "summary.json",
                         "README.txt",
                         "display-timeline.jsonl",
                         "awayra.log",
                         "eventlog-system.txt",
                         "eventlog-application.txt",
                         "eventlog-dxgkrnl.txt",
                         "eventlog-kernel-pnp.txt",
                         "devices-monitor.txt",
                         "devices-display-adapter.txt",
                         "power-active-scheme.txt",
                         "power-display-settings.txt",
                         "dxdiag.txt"
                     })
            {
                Assert.IsTrue(entryNames.Contains(expectedEntry), $"Diagnostic ZIP is missing {expectedEntry}.");
            }

            var summaryEntry = archive.GetEntry("summary.json");
            Assert.IsNotNull(summaryEntry);
            await using var summaryStream = summaryEntry.Open();
            using var summary = await JsonDocument.ParseAsync(summaryStream).ConfigureAwait(false);
            Assert.AreEqual(1, summary.RootElement.GetProperty("reportVersion").GetInt32());
            Assert.IsTrue(summary.RootElement.TryGetProperty("markerTime", out _));
            Assert.IsTrue(summary.RootElement.TryGetProperty("currentState", out _));
        }
        finally
        {
            AppPaths.OverrideDataRoot = previousDataRoot;
            try
            {
                if (Directory.Exists(dataRoot))
                {
                    Directory.Delete(dataRoot, recursive: true);
                }
            }
            catch
            {
                // A failed cleanup must not hide the diagnostic assertion that failed.
            }
        }
    }
}
