using UmaCompanion.Core.Races;
using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class TrophyPlannerTests
{
    // Trophy races: 1001 & 1002 are on the career calendar; 9999 has a trophy but is not a
    // career-calendar race (e.g. a legend/other-mode race).
    private static readonly TrophyRace[] TrophyRaces =
    {
        new(1001, "Japanese Derby", RaceGrade.G1),
        new(1002, "Arima Kinen", RaceGrade.G1),
        new(9999, "Legend-only Race", RaceGrade.G1),
    };

    private static CareerRace Cr(int id, CareerYear year, int month, RaceHalf half)
        => new(id, $"Race {id}", RaceGrade.G1, year, month, half, 2400, Surface.Turf,
            DistanceCategory.Medium, 10000);

    // 1003 is on the calendar but has no trophy.
    private static readonly CareerRace[] Calendar =
    {
        Cr(1001, CareerYear.Classic, 5, RaceHalf.Late),
        Cr(1002, CareerYear.Senior, 12, RaceHalf.Late),
        Cr(1003, CareerYear.Senior, 11, RaceHalf.Late),
    };

    [Fact]
    public void MissingTrophies_ExcludesRacesTheCharacterHasWon()
    {
        var planner = new TrophyPlanner(TrophyRaces, Calendar);

        var missing = planner.MissingTrophies(new[] { 1001 });

        Assert.Equal(new[] { 1002, 9999 }, missing.Select(r => r.RaceId));
    }

    [Fact]
    public void MissingTrophySchedule_SchedulesOnlyMissingTrophyRacesThatAreOnTheCalendar()
    {
        var planner = new TrophyPlanner(TrophyRaces, Calendar);

        // Won 1001 → missing {1002, 9999}. 9999 is not on the calendar, 1003 has no trophy.
        var schedule = planner.MissingTrophySchedule(new[] { 1001 });

        Assert.Equal(new[] { 1002 }, schedule.Select(s => s.Race.RaceId));
    }

    [Fact]
    public void MissingTrophySchedule_IsEmpty_WhenEveryTrophyIsWon()
    {
        var planner = new TrophyPlanner(TrophyRaces, Calendar);

        Assert.Empty(planner.MissingTrophySchedule(new[] { 1001, 1002, 9999 }));
    }
}
