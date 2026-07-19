using UmaCompanion.Core.Reading;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class TrophyRoomScreenInterpreterTests
{
    private static TrophyRoomScreenInterpreter BuildInterpreter() => new(new RaceNameMatcher(new[]
    {
        (1005, "Satsuki Sho"),
        (1010, "Tokyo Yushun (Japanese Derby)"),
        (1013, "Japan Cup"),
        (1014, "Arima Kinen"),
    }));

    [Fact]
    public void WonRaceIds_TrophyRoomPageWithSeveralTrophies_ReturnsAllVisibleRaces()
    {
        var ids = BuildInterpreter().WonRaceIds(new[]
        {
            "Trophy Room", "G1", "Japan Cup", "Arima Kinen", "Satsuki Sho", "2/3",
        });

        Assert.Equal(new[] { 1013, 1014, 1005 }.OrderBy(x => x), ids.OrderBy(x => x));
    }

    [Fact]
    public void WonRaceIds_RacePickerWithoutTrophyRoomMarker_ReturnsNothing()
    {
        // The race picker lists races the player has NOT won — the gate must reject it.
        var ids = BuildInterpreter().WonRaceIds(new[]
        {
            "Choose a race", "Japan Cup", "G1 2400m", "Arima Kinen", "G1 2500m",
        });

        Assert.Empty(ids);
    }

    [Fact]
    public void WonRaceIds_MarkerWithOneOcrError_StillGatesIn()
    {
        var ids = BuildInterpreter().WonRaceIds(new[] { "Trophv Room", "Japan Cup" });

        Assert.Equal(new[] { 1013 }, ids);
    }

    [Fact]
    public void WonRaceIds_NameWrappedAcrossTwoLines_IsMatchedViaJoin()
    {
        var ids = BuildInterpreter().WonRaceIds(new[]
        {
            "Trophy Room", "Tokyo Yushun", "(Japanese Derby)",
        });

        Assert.Contains(1010, ids);
    }

    [Fact]
    public void WonRaceIds_DuplicateTrophyLabels_AreDistinct()
    {
        var ids = BuildInterpreter().WonRaceIds(new[] { "Trophy Room", "Japan Cup", "Japan Cup" });

        Assert.Equal(new[] { 1013 }, ids);
    }

    [Fact]
    public void WonRaceIds_TrophyRoomWithNoRecognisableNames_ReturnsNothing()
    {
        Assert.Empty(BuildInterpreter().WonRaceIds(new[] { "Trophy Room", "???", "???" }));
    }

    [Fact]
    public void IsTrophyRoomScreen_PlainRaceResultScreen_IsFalse()
    {
        Assert.False(BuildInterpreter().IsTrophyRoomScreen(new[] { "Japan Cup", "1st" }));
    }
}
