using Awayra.App.Services;
using Awayra.Core.Models;
using Awayra.Core.Persistence;

namespace Awayra.App.Tests;

[TestClass]
public sealed class JsonFileStoreTests
{
    private string _tempDir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "awayra-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAndLoad_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new JsonFileStore<AppSettings>(path, new NullLogger(), AppSettings.CreateDefault);
        var settings = AppSettings.CreateDefault();
        settings.EyeResetIntervalMinutes = 12;

        await store.SaveAsync(settings).ConfigureAwait(false);
        var loaded = await store.LoadAsync().ConfigureAwait(false);

        Assert.AreEqual(12, loaded.EyeResetIntervalMinutes);
    }

    [TestMethod]
    public async Task FullyCorruptJson_IsBackedUpAndRecovered()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(path, "not json").ConfigureAwait(false);
        var store = new JsonFileStore<AppSettings>(
            path,
            new NullLogger(),
            AppSettings.CreateDefault,
            json => SettingsRecovery.LoadWithRecovery(json));

        var loaded = await store.LoadAsync().ConfigureAwait(false);

        Assert.IsFalse(loaded.StartMinimized);
        Assert.IsTrue(Directory.GetFiles(_tempDir, "settings.json.corrupt.*").Length >= 1);
    }

    [TestMethod]
    public async Task Save_UsesAtomicReplace()
    {
        var path = Path.Combine(_tempDir, "state.json");
        var store = new JsonFileStore<AppSettings>(path, new NullLogger(), AppSettings.CreateDefault);
        await store.SaveAsync(AppSettings.CreateDefault()).ConfigureAwait(false);

        Assert.IsTrue(File.Exists(path));
        Assert.IsFalse(File.Exists(path + ".tmp"));
    }
}
