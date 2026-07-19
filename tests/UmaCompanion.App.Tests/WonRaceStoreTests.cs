using System.IO;
using UmaCompanion.App;

namespace UmaCompanion.App.Tests;

public class WonRaceStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "uma-store-test-" + Guid.NewGuid());

    private string StorePath => Path.Combine(_dir, "won-races.json");

    [Fact]
    public void SaveThenLoad_RoundTripsTheSet()
    {
        var store = new WonRaceStore(StorePath);
        store.Save(new[] { 1013, 1005, 1014 });

        var loaded = new WonRaceStore(StorePath).Load();

        Assert.Equal(new HashSet<int> { 1005, 1013, 1014 }, loaded);
    }

    [Fact]
    public void Load_NoFileYet_ReturnsEmptySet()
    {
        Assert.Empty(new WonRaceStore(StorePath).Load());
    }

    [Fact]
    public void Load_CorruptFile_ReturnsEmptySetInsteadOfThrowing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(StorePath, "{not json[");

        Assert.Empty(new WonRaceStore(StorePath).Load());
    }

    [Fact]
    public void Save_OverwritesPreviousContents()
    {
        var store = new WonRaceStore(StorePath);
        store.Save(new[] { 1 });
        store.Save(new[] { 1, 2, 3 });

        Assert.Equal(new HashSet<int> { 1, 2, 3 }, store.Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
