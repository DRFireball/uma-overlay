using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class TrophyTrackerTests
{
    private static readonly TrophyRace Derby = new(1001, "Tokyo Yushun (Japanese Derby)", RaceGrade.G1);
    private static readonly TrophyRace Oaks = new(1002, "Yushun Himba (Oaks)", RaceGrade.G1);
    private static readonly TrophyRace SatsukiSho = new(1003, "Satsuki Sho", RaceGrade.G1);

    [Fact]
    public void MissingRaces_ReturnsTrophyRacesTheCharacterHasNotWon()
    {
        var tracker = new TrophyTracker();
        var allTrophyRaces = new[] { Derby, Oaks, SatsukiSho };
        var wonRaceIds = new[] { 1001 };

        var missing = tracker.MissingRaces(allTrophyRaces, wonRaceIds);

        Assert.Equal(new[] { Oaks, SatsukiSho }, missing);
    }

    [Fact]
    public void MissingRaces_ReturnsEmpty_WhenAllTrophiesWon()
    {
        var tracker = new TrophyTracker();
        var allTrophyRaces = new[] { Derby, Oaks };

        var missing = tracker.MissingRaces(allTrophyRaces, new[] { 1001, 1002 });

        Assert.Empty(missing);
    }
}
