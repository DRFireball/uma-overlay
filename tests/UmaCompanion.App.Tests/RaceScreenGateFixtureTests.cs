using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using UmaCompanion.App.Reading;
using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Reading;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace UmaCompanion.App.Tests;

/// <summary>
/// The strict on/off gate, against real captures. Exactly one screen in the game may be read as a
/// race result; the two that previously leaked false wins must read as nothing at all:
///   race-win-shuka-sho.png — the real result screen (gate ON, race identified as Shuka Sho)
///   career-goal-list.png   — produced a false Japanese Oaks win ("Place 1st in …" + a race name)
///   race-start-arima.png   — the pre-race card, which shows a grade, a race name and "Turf"
/// None of the three is the Trophy Room, so the trophy path must decline all of them too.
/// </summary>
public class RaceScreenGateFixtureTests
{
    [SkippableFact]
    public async Task WinScreen_IsIdentifiedAndNamesTheRace()
    {
        var (lines, matcher) = await ReadFixtureAsync("race-win-shuka-sho.png");

        Assert.True(RaceResultScreenInterpreter.IsRaceResultScreen(lines));

        // The name pill sits above the condition banner; nothing else on screen may win the match.
        var race = new RaceResultScreenInterpreter(matcher).RaceOnResultScreen(lines);
        Assert.NotNull(race);
        Assert.Equal("Shuka Sho", race.Name);
    }

    [SkippableTheory]
    [InlineData("career-goal-list.png")]
    [InlineData("race-start-arima.png")]
    [InlineData("home-screen.png")]
    public async Task NonResultScreens_AreNeverRead(string fixture)
    {
        var (lines, matcher) = await ReadFixtureAsync(fixture);

        Assert.False(RaceResultScreenInterpreter.IsRaceResultScreen(lines));
        Assert.Null(new RaceResultScreenInterpreter(matcher).Interpret(lines));
        Assert.False(new TrophyRoomScreenInterpreter(matcher).IsTrophyRoomScreen(
            lines.Select(l => new OcrLine(l, new OcrRect(0, 0, 1, 1))).ToList()));
    }

    private static async Task<(IReadOnlyList<string> Lines, RaceNameMatcher Matcher)> ReadFixtureAsync(
        string fixture)
    {
        var mdb = Environment.GetEnvironmentVariable("UMA_MASTER_MDB");
        Skip.If(string.IsNullOrEmpty(mdb) || !File.Exists(mdb), "needs the full race list");
        var reader = WindowsOcrReader.TryCreate();
        Skip.If(reader is null, "no Windows OCR language pack installed");

        using var db = MasterDb.OpenReadOnlyCopy(mdb!);
        var matcher = new RaceNameMatcher(db.GetCareerRaces().Select(r => (r.RaceId, r.Name))
            .Concat(db.GetTrophyRaces().Select(t => (t.RaceId, t.Name))));

        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", fixture);
        var bytes = await File.ReadAllBytesAsync(path);
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var lines = await reader!.ReadAsync(bitmap);

        return (lines.Select(l => l.Text).ToList(), matcher);
    }
}
