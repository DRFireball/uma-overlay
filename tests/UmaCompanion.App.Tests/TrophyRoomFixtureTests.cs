using System.IO;
using System.Runtime.InteropServices.WindowsRuntime; // AsBuffer()
using UmaCompanion.App.Reading;
using UmaCompanion.Core.Reading;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace UmaCompanion.App.Tests;

/// <summary>
/// The whole trophy-room pipeline — real OCR engine, real brightness probe, real interpreters —
/// against a REAL capture of the game's G3 trophy-room page (tests/fixtures/trophy-room-g3.png).
/// The page shows 10 owned trophies and 5 unowned silhouettes, every one of them labelled, so
/// this guards exactly the failure mode that matters: visible label ≠ won.
/// </summary>
public class TrophyRoomFixtureTests
{
    // The 15 races on the fixture page, with their real master.mdb ids and names.
    private static readonly (int Id, string Name)[] Races =
    {
        (3062, "Musashino Stakes"),
        (3063, "Fukushima Kinen"),
        (3064, "Tokyo Sports Hai Junior Stakes"),
        (3065, "Kyoto Junior Stakes"),
        (3066, "Keihan Hai"),
        (3067, "Challenge Cup"),
        (3068, "Chunichi Shimbun Hai"),
        (3069, "Capella Stakes"),
        (3070, "Turquoise Stakes"),
        (3071, "TCK Jo-o Hai"),
        (3072, "Tokyo Sprint"),
        (3073, "Sparking Lady Cup"),
        (3074, "Marine Cup"),
        (3075, "Queen Sho"),
        (3076, "Mercury Cup"),
    };

    private static readonly int[] OwnedOnPage = { 3062, 3063, 3064, 3065, 3067, 3068, 3069, 3070, 3074, 3075 };
    private static readonly int[] UnownedOnPage = { 3066, 3071, 3072, 3073, 3076 };

    [SkippableFact]
    public async Task RealTrophyRoomCapture_MarksOwnedTrophies_AndNeverSilhouettes()
    {
        var reader = WindowsOcrReader.TryCreate();
        Skip.If(reader is null, "no Windows OCR language pack installed");

        using var bitmap = await LoadFixtureAsync("trophy-room-g3.png");
        var lines = await reader!.ReadAsync(bitmap);

        var interpreter = new TrophyRoomScreenInterpreter(new RaceNameMatcher(
            Races.Select(r => (r.Id, r.Name))));
        Assert.True(interpreter.IsTrophyRoomScreen(lines), "trophy-room gate did not recognise the page");

        var owned = interpreter.OwnedRaceIds(
            lines, region => BitmapBrightnessProbe.MeanLuminance(bitmap, region));

        // A silhouette marked as won corrupts the tracker — zero tolerance.
        foreach (var id in UnownedOnPage)
        {
            Assert.DoesNotContain(id, owned);
        }
        // OCR should pick up every owned trophy's label on this clean capture.
        foreach (var id in OwnedOnPage)
        {
            Assert.Contains(id, owned);
        }
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
