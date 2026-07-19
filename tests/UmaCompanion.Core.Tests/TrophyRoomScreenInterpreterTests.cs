using UmaCompanion.Core.Reading;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class TrophyRoomScreenInterpreterTests
{
    private const double Bright = 150; // owned trophy art
    private const double Dark = 45;    // unowned silhouette

    private static TrophyRoomScreenInterpreter BuildInterpreter() => new(new RaceNameMatcher(new[]
    {
        (3062, "Musashino Stakes"),
        (3066, "Keihan Hai"),
        (3064, "Tokyo Sports Hai Junior Stakes"),
        (3067, "Challenge Cup"),
    }));

    private static OcrLine Line(string text, double x, double y)
        => new(text, new OcrRect(x, y, Width: 140, Height: 16));

    private static OcrLine Marker() => Line("Trophy Room", 10, 10);

    /// <summary>Probe stub: labels at x &lt; 400 have bright art above, the rest silhouettes.</summary>
    private static double BrightLeftDarkRight(OcrRect probe) => probe.X < 400 ? Bright : Dark;

    [Fact]
    public void OwnedRaceIds_BrightTrophyIsOwned_DarkSilhouetteIsNot()
    {
        var ids = BuildInterpreter().OwnedRaceIds(new[]
        {
            Marker(),
            Line("Musashino S.", 240, 288), // bright → owned
            Line("Keihan Hai", 455, 474),   // dark → NOT owned, despite the visible label
        }, BrightLeftDarkRight);

        Assert.Equal(new[] { 3062 }, ids);
    }

    [Fact]
    public void OwnedRaceIds_AbbreviatedLabels_MatchFullMasterNames()
    {
        var ids = BuildInterpreter().OwnedRaceIds(
            new[] { Marker(), Line("Musashino S.", 100, 300), Line("Challenge C.", 100, 500) },
            _ => Bright);

        Assert.Equal(new[] { 3062, 3067 }.OrderBy(x => x), ids.OrderBy(x => x));
    }

    [Fact]
    public void OwnedRaceIds_NameWrappedAcrossTwoLines_IsMatchedViaJoin()
    {
        var ids = BuildInterpreter().OwnedRaceIds(new[]
        {
            Marker(),
            Line("Tokyo Sports Hai", 650, 268),
            Line("Junior S.", 650, 288),
        }, _ => Bright);

        Assert.Contains(3064, ids);
    }

    [Fact]
    public void OwnedRaceIds_RacePickerWithoutTrophyRoomMarker_ReturnsNothing()
    {
        // The race picker lists races the player has NOT won — the gate must reject it.
        var ids = BuildInterpreter().OwnedRaceIds(
            new[] { Line("Choose a race", 10, 10), Line("Keihan Hai", 100, 300) },
            _ => Bright);

        Assert.Empty(ids);
    }

    [Fact]
    public void OwnedRaceIds_MarkerWithOneOcrError_StillGatesIn()
    {
        var ids = BuildInterpreter().OwnedRaceIds(
            new[] { Line("Trophv Room", 10, 10), Line("Keihan Hai", 100, 300) },
            _ => Bright);

        Assert.Equal(new[] { 3066 }, ids);
    }

    [Fact]
    public void OwnedRaceIds_DuplicateLabels_AreDistinct()
    {
        var ids = BuildInterpreter().OwnedRaceIds(
            new[] { Marker(), Line("Keihan Hai", 100, 300), Line("Keihan Hai", 100, 500) },
            _ => Bright);

        Assert.Equal(new[] { 3066 }, ids);
    }

    [Fact]
    public void OwnedRaceIds_ProbesTheRegionAboveTheLabel()
    {
        OcrRect? probed = null;
        var label = Line("Musashino S.", 240, 288);

        BuildInterpreter().OwnedRaceIds(new[] { Marker(), label }, r =>
        {
            // Capture the label's probe, not the marker's (the marker line matches no race).
            probed = r;
            return Bright;
        });

        Assert.NotNull(probed);
        // Entirely above the label, horizontally centered on it.
        Assert.True(probed.Y + probed.Height <= label.Bounds.Y);
        var labelCenter = label.Bounds.X + label.Bounds.Width / 2;
        Assert.Equal(labelCenter, probed.X + probed.Width / 2, precision: 5);
    }

    [Fact]
    public void IsTrophyRoomScreen_PlainRaceResultScreen_IsFalse()
    {
        Assert.False(BuildInterpreter().IsTrophyRoomScreen(
            new[] { Line("Japan Cup", 100, 100), Line("1st", 100, 200) }));
    }
}
