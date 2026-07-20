using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using UmaCompanion.App.Reading;
using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Reading;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace UmaCompanion.App.Tests;

/// <summary>
/// The game's Home screen (tests/fixtures/home-screen.png) is a NEGATIVE case: it shows the
/// "Champions Meeting" PvP event, whose name fuzzily resembles the "Champions C." trophy label.
/// Neither interpreter may fire on it — not the trophy path (no Trophy Room), and not the result
/// path (full-name matching rejects the event name).
/// </summary>
public class HomeScreenFixtureTests
{
    [SkippableFact]
    public async Task HomeScreen_ProducesNoDetection()
    {
        var mdb = Environment.GetEnvironmentVariable("UMA_MASTER_MDB");
        Skip.If(string.IsNullOrEmpty(mdb) || !File.Exists(mdb), "needs the full race list");
        var reader = WindowsOcrReader.TryCreate();
        Skip.If(reader is null, "no Windows OCR language pack installed");

        using var db = MasterDb.OpenReadOnlyCopy(mdb!);
        var names = db.GetCareerRaces().Select(r => (r.RaceId, r.Name))
            .Concat(db.GetTrophyRaces().Select(t => (t.RaceId, t.Name)));
        var matcher = new RaceNameMatcher(names);

        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "home-screen.png");
        var bytes = await File.ReadAllBytesAsync(path);
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var lines = await reader!.ReadAsync(bitmap);

        Assert.False(new TrophyRoomScreenInterpreter(matcher).IsTrophyRoomScreen(lines));
        Assert.Null(new RaceResultScreenInterpreter(matcher).Interpret(lines.Select(l => l.Text).ToList()));
    }
}
