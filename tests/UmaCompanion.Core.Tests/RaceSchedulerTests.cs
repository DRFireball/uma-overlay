using UmaCompanion.Core.Races;
using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class RaceSchedulerTests
{
    private static CareerRace Race(int id, string name, RaceGrade grade, int month, RaceHalf half,
        int distance, Surface surface, int fans = 10000)
        => new(id, name, grade, month, half, distance, surface, CareerRace.CategoryFor(distance), fans);

    [Fact]
    public void G1MaxSchedule_PicksBestGradePerSlot_AndExcludesScenarioFinales()
    {
        var calendar = new[]
        {
            Race(1, "Sprinters Stakes", RaceGrade.G1, 9, RaceHalf.Late, 1200, Surface.Turf),
            Race(2, "Some G3", RaceGrade.G3, 9, RaceHalf.Late, 1400, Surface.Turf), // same slot → G1 wins
            Race(3, "Some G2", RaceGrade.G2, 6, RaceHalf.Early, 1600, Surface.Turf), // earlier slot
            Race(4, "URA Finale", RaceGrade.G1, 1, RaceHalf.Late, 2000, Surface.Turf, fans: 0), // finale, excluded
        };

        var schedule = new RaceScheduler().G1MaxSchedule(calendar);

        Assert.Equal(new[] { 3, 1 }, schedule.Select(s => s.Race.RaceId)); // ordered by turn: June then September
        Assert.DoesNotContain(schedule, s => s.Race.RaceId == 4);
    }

    [Fact]
    public void MissingTrophySchedule_SchedulesOnlyUnwonTrophyRaces()
    {
        var calendar = new[]
        {
            Race(1, "Japanese Derby", RaceGrade.G1, 5, RaceHalf.Late, 2400, Surface.Turf),
            Race(2, "Japanese Oaks", RaceGrade.G1, 5, RaceHalf.Late, 2400, Surface.Turf), // same slot, already won
            Race(3, "Arima Kinen", RaceGrade.G1, 12, RaceHalf.Late, 2500, Surface.Turf),
        };
        var missingTrophyRaceIds = new HashSet<int> { 1, 3 }; // race 2 already won

        var schedule = new RaceScheduler().MissingTrophySchedule(calendar, missingTrophyRaceIds);

        Assert.Equal(new[] { 1, 3 }, schedule.Select(s => s.Race.RaceId));
    }

    [Fact]
    public void AptitudeSchedule_PrefersHighestAptitudeRacePerSlot_OverRaceGrade()
    {
        var calendar = new[]
        {
            Race(1, "Mile Turf G2", RaceGrade.G2, 5, RaceHalf.Early, 1600, Surface.Turf),
            Race(2, "Long Dirt G1", RaceGrade.G1, 5, RaceHalf.Early, 3000, Surface.Dirt), // same slot, higher grade
        };
        var aptitudes = new CharacterAptitudes(
            new Dictionary<DistanceCategory, AptitudeGrade>
            {
                [DistanceCategory.Mile] = AptitudeGrade.A,
                [DistanceCategory.Long] = AptitudeGrade.G,
            },
            new Dictionary<Surface, AptitudeGrade>
            {
                [Surface.Turf] = AptitudeGrade.A,
                [Surface.Dirt] = AptitudeGrade.G,
            });

        var schedule = new RaceScheduler().AptitudeSchedule(calendar, aptitudes);

        var pick = Assert.Single(schedule);
        Assert.Equal(1, pick.Race.RaceId); // Mile/Turf (A) beats Long/Dirt (G) despite lower race grade
    }

    [Fact]
    public void Aptitudes_For_UsesWeakerOfDistanceAndSurface_AndDefaultsMissingToG()
    {
        var aptitudes = new CharacterAptitudes(
            new Dictionary<DistanceCategory, AptitudeGrade> { [DistanceCategory.Mile] = AptitudeGrade.S },
            new Dictionary<Surface, AptitudeGrade> { [Surface.Turf] = AptitudeGrade.C });

        var mileTurf = Race(1, "x", RaceGrade.G1, 1, RaceHalf.Early, 1600, Surface.Turf);
        var mileDirt = Race(2, "y", RaceGrade.G1, 1, RaceHalf.Early, 1600, Surface.Dirt);

        Assert.Equal(AptitudeGrade.C, aptitudes.For(mileTurf)); // min(S, C)
        Assert.Equal(AptitudeGrade.G, aptitudes.For(mileDirt)); // dirt missing → G
    }
}
