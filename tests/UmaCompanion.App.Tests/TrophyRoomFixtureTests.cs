using System.IO;
using System.Runtime.InteropServices.WindowsRuntime; // AsBuffer()
using UmaCompanion.App.Reading;
using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Reading;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace UmaCompanion.App.Tests;

/// <summary>
/// The whole trophy-room pipeline — real OCR engine, real brightness probe, real interpreters —
/// against REAL captures of the game's trophy-room pages. The matcher is built from the FULL
/// master.mdb race list (not a curated subset), so a false positive from any of the ~hundreds of
/// races surfaces here; each test asserts the EXACT owned set, both directions:
/// every owned trophy found, and nothing else (no silhouettes, no strays).
/// </summary>
public class TrophyRoomFixtureTests
{
    // Owned trophy ids visible on each captured page (verified by eye against the screenshot).
    private static readonly int[] G3OwnedIds =
        { 3062, 3063, 3064, 3065, 3067, 3068, 3069, 3070, 3074, 3075 };

    private static readonly int[] G1Page1OwnedIds =
        { 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010, 1011, 1012, 1013, 1014, 1015 };

    private static readonly int[] G1Page2OwnedIds =
        { 1016, 1017, 1018, 1019, 1020, 1021, 1022, 1023, 1024, 1101, 1102, 1103, 1104, 1105, 1106 };

    [SkippableFact]
    public Task G3Page_ExactOwnedSet() => AssertExactOwned("trophy-room-g3.png", G3OwnedIds);

    [SkippableFact]
    public Task G1Page1_ExactOwnedSet() => AssertExactOwned("trophy-room-g1-page1.png", G1Page1OwnedIds);

    [SkippableFact]
    public Task G1Page2_ExactOwnedSet() => AssertExactOwned("trophy-room-g1-page2.png", G1Page2OwnedIds);

    private static async Task AssertExactOwned(string fixture, int[] expectedOwned)
    {
        var mdb = Environment.GetEnvironmentVariable("UMA_MASTER_MDB");
        Skip.If(string.IsNullOrEmpty(mdb) || !File.Exists(mdb),
            "UMA_MASTER_MDB not set — needs the full race list to catch false positives");
        var reader = WindowsOcrReader.TryCreate();
        Skip.If(reader is null, "no Windows OCR language pack installed");

        using var db = MasterDb.OpenReadOnlyCopy(mdb!);
        var names = db.GetCareerRaces().Select(r => (r.RaceId, r.Name))
            .Concat(db.GetTrophyRaces().Select(t => (t.RaceId, t.Name)));
        var interpreter = new TrophyRoomScreenInterpreter(new RaceNameMatcher(names));

        using var bitmap = await LoadFixtureAsync(fixture);
        var lines = await reader!.ReadAsync(bitmap);
        Assert.True(interpreter.IsTrophyRoomScreen(lines), "trophy-room gate did not recognise the page");

        var owned = interpreter.OwnedRaceIds(
            lines, region => BitmapBrightnessProbe.MeanLuminance(bitmap, region));

        // Exact set: every owned trophy detected, and NOTHING extra (the 17-on-a-15-page bug).
        Assert.Equal(expectedOwned.OrderBy(x => x), owned.OrderBy(x => x));
    }

    private static async Task<SoftwareBitmap> LoadFixtureAsync(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", name);
        var bytes = await File.ReadAllBytesAsync(path);

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }
}
