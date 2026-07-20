using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using UmaCompanion.App.Reading;
using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Reading;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace UmaCompanion.App.Tests;

/// <summary>
/// The G2 page-1 capture is cropped above the "Trophy Room" title, so it can't exercise the
/// gated owned-set path; instead it guards the MATCHER against real G2 label art — the game's
/// harder abbreviations ("New Zealand T." = Trophy, "Yomiuri Milers C." = Milers Cup,
/// "American JCC"), an apostrophe ("Fillies' Revue"), and a two-line-wrapped name
/// ("Hanshin Umamusume Stakes"). Each expected race must be found by FindBest on some OCR line.
/// </summary>
public class G2MatcherCoverageTest
{
    private static readonly int[] ExpectedG2Ids =
        { 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008, 2009, 2010, 2011, 2012, 2013, 2014, 3018 };

    [SkippableFact]
    public async Task EveryG2LabelResolves()
    {
        var mdb = Environment.GetEnvironmentVariable("UMA_MASTER_MDB");
        Skip.If(string.IsNullOrEmpty(mdb) || !File.Exists(mdb), "needs the full race list");
        var reader = WindowsOcrReader.TryCreate();
        Skip.If(reader is null, "no Windows OCR language pack installed");

        using var db = MasterDb.OpenReadOnlyCopy(mdb!);
        var names = db.GetCareerRaces().Select(r => (r.RaceId, r.Name))
            .Concat(db.GetTrophyRaces().Select(t => (t.RaceId, t.Name)));
        var matcher = new RaceNameMatcher(names);

        var bitmap = await LoadFixtureAsync("trophy-room-g2-page1.png");
        using (bitmap)
        {
            var lines = await reader!.ReadAsync(bitmap);
            // Include adjacent-line joins so a two-line-wrapped name is still matchable.
            var candidates = lines.Select(l => l.Text)
                .Concat(lines.Zip(lines.Skip(1), (a, b) => a.Text + " " + b.Text));
            var found = candidates
                .Select(c => matcher.FindBest(c))
                .Where(m => m is not null)
                .Select(m => m!.RaceId)
                .ToHashSet();

            foreach (var id in ExpectedG2Ids)
            {
                Assert.Contains(id, found);
            }
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
