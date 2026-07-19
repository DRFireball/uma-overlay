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

    // G1 page 1: 15 owned trophies including the hard cases — "Victoria M." (trailing word
    // dropped) vs "NHK Mile C." (Mile kept), the dark Tenno Sho (Spring) plaque, and the
    // "Japanese Derby" parenthetical alias of "Tokyo Yushun (Japanese Derby)".
    private static readonly (int Id, string Name)[] G1Page1Races =
    {
        (1001, "February Stakes"),
        (1002, "Takamatsunomiya Kinen"),
        (1003, "Osaka Hai"),
        (1004, "Oka Sho"),
        (1005, "Satsuki Sho"),
        (1006, "Tenno Sho (Spring)"),
        (1007, "NHK Mile Cup"),
        (1008, "Victoria Mile"),
        (1009, "Japanese Oaks"),
        (1010, "Tokyo Yushun (Japanese Derby)"),
        (1011, "Yasuda Kinen"),
        (1012, "Takarazuka Kinen"),
        (1013, "Sprinters Stakes"),
        (1014, "Shuka Sho"),
        (1015, "Kikuka Sho"),
        // Decoy: master.mdb "Mile Championship" must not steal "NHK Mile C.".
        (1018, "Mile Championship"),
        (1016, "Tenno Sho (Autumn)"), // must not win "Tenno Sho (Spring)"
    };

    [SkippableFact]
    public async Task RealG1Page1Capture_FindsAllFifteenOwnedTrophies_IncludingAbbreviations()
    {
        var reader = WindowsOcrReader.TryCreate();
        Skip.If(reader is null, "no Windows OCR language pack installed");

        using var bitmap = await LoadFixtureAsync("trophy-room-g1-page1.png");
        var lines = await reader!.ReadAsync(bitmap);

        var interpreter = new TrophyRoomScreenInterpreter(new RaceNameMatcher(
            G1Page1Races.Select(r => (r.Id, r.Name))));
        var owned = interpreter.OwnedRaceIds(
            lines, region => BitmapBrightnessProbe.MeanLuminance(bitmap, region));

        foreach (var id in Enumerable.Range(1001, 15))
        {
            Assert.Contains(id, owned);
        }
        Assert.DoesNotContain(1018, owned); // Mile Championship is not on this page
    }

    // G1 page 2: 15 owned trophies, no silhouettes, and the hard cases — the dark
    // Tenno Sho (Autumn) plaque, "Mile Ch."/"Hanshin J.F."/"Asahi Hai F.S." abbreviations, a
    // wrapped "Queen Elizabeth II / Cup", and master.mdb duplicate-name decoys.
    private static readonly (int Id, string Name)[] G1Races =
    {
        (1016, "Tenno Sho (Autumn)"),
        (1017, "Queen Elizabeth II Cup"),
        (1018, "Mile Championship"),
        (1019, "Japan Cup"),
        (1020, "Champions Cup"),
        (1021, "Hanshin Juvenile Fillies"),
        (1022, "Asahi Hai Futurity Stakes"),
        (1023, "Arima Kinen"),
        (1024, "Hopeful Stakes"),
        (1101, "Teio Sho"),
        (1102, "Japan Dirt Derby"),
        (1103, "JBC Ladies' Classic"),
        (1104, "JBC Sprint"),
        (1105, "JBC Classic"),
        (1106, "Tokyo Daishoten"),
        // Real master.mdb duplicates that must NOT win over the career ids above.
        (5920, "Japan Cup"),
        (5960, "Asahi Hai F.S."),
    };

    [SkippableFact]
    public async Task RealG1Page2Capture_FindsAllFifteenOwnedTrophies_UnderCareerIds()
    {
        var reader = WindowsOcrReader.TryCreate();
        Skip.If(reader is null, "no Windows OCR language pack installed");

        using var bitmap = await LoadFixtureAsync("trophy-room-g1-page2.png");
        var lines = await reader!.ReadAsync(bitmap);

        var interpreter = new TrophyRoomScreenInterpreter(new RaceNameMatcher(
            G1Races.Select(r => (r.Id, r.Name))));
        var owned = interpreter.OwnedRaceIds(
            lines, region => BitmapBrightnessProbe.MeanLuminance(bitmap, region));

        foreach (var id in new[]
        {
            1016, 1017, 1018, 1019, 1020, 1021, 1022, 1023, 1024, 1101, 1102, 1103, 1104, 1105, 1106,
        })
        {
            Assert.Contains(id, owned);
        }
        // The duplicate-name decoys must have lost to the career ids.
        Assert.DoesNotContain(5920, owned);
        Assert.DoesNotContain(5960, owned);
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
