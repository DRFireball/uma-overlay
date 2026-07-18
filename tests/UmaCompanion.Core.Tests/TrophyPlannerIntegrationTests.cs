using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class TrophyPlannerIntegrationTests
{
    [SkippableFact]
    public void MissingTrophySchedule_OnRealData_SchedulesOnlyTrophyRaces_AndEmptiesWhenAllWon()
    {
        var path = Environment.GetEnvironmentVariable("UMA_MASTER_MDB");
        Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path),
            "UMA_MASTER_MDB is not set or does not point at an existing master.mdb");

        using var db = MasterDb.OpenReadOnlyCopy(path!);
        var planner = TrophyPlanner.FromMasterDb(db);
        var allTrophyRaceIds = db.GetTrophyRaces().Select(r => r.RaceId).ToHashSet();

        // Nothing won yet: hunt every trophy race that appears on the career calendar.
        var schedule = planner.MissingTrophySchedule(Array.Empty<int>());

        Assert.True(schedule.Count > 30, $"expected a substantial hunt schedule, got {schedule.Count}");
        Assert.All(schedule, s => Assert.Contains(s.Race.RaceId, allTrophyRaceIds));
        var slots = schedule.Select(s => (s.Race.Year, s.Race.Month, s.Race.Half)).ToList();
        Assert.Equal(slots.Count, slots.Distinct().Count()); // one race per turn

        // Everything won: nothing left to hunt.
        Assert.Empty(planner.MissingTrophySchedule(allTrophyRaceIds));
    }
}
