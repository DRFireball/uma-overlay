using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Races;
using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class RaceSchedulerIntegrationTests
{
    [SkippableFact]
    public void G1MaxSchedule_AgainstRealCalendar_IsOnePerSlot_FinalesExcluded_AndIncludesArima()
    {
        var path = Environment.GetEnvironmentVariable("UMA_MASTER_MDB");
        Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path),
            "UMA_MASTER_MDB is not set or does not point at an existing master.mdb");

        using var db = MasterDb.OpenReadOnlyCopy(path!);
        var calendar = db.GetCareerRaces();

        var schedule = new RaceScheduler().G1MaxSchedule(calendar);

        Assert.NotEmpty(schedule);
        // One race per (month, half) turn slot.
        var slots = schedule.Select(s => (s.Race.Month, s.Race.Half)).ToList();
        Assert.Equal(slots.Count, slots.Distinct().Count());
        // Scenario finales (fans == 0) never scheduled.
        Assert.All(schedule, s => Assert.True(s.Race.NeedFanCount > 0));
        // Arima Kinen (G1, Dec late) wins its slot.
        Assert.Contains(schedule, s => s.Race.Name == "Arima Kinen" && s.Race.Grade == RaceGrade.G1);
        // Ordered by turn.
        Assert.Equal(
            schedule.OrderBy(s => s.Race.Month).ThenBy(s => s.Race.Half).Select(s => s.Race.RaceId),
            schedule.Select(s => s.Race.RaceId));
    }
}
