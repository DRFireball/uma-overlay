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
        // One race per (career year, month, half) turn — years are not collapsed.
        var slots = schedule.Select(s => (s.Race.Year, s.Race.Month, s.Race.Half)).ToList();
        Assert.Equal(slots.Count, slots.Distinct().Count());
        // Scenario finales (fans == 0) never scheduled.
        Assert.All(schedule, s => Assert.True(s.Race.NeedFanCount > 0));
        // All three career years are represented (the pre-fix bug collapsed them to one).
        Assert.Equal(3, schedule.Select(s => s.Race.Year).Distinct().Count());
        // Three years of turns — well beyond the 24 total the collapsed-slot bug produced.
        Assert.True(schedule.Count > 40, $"expected a full 3-year schedule, got {schedule.Count} turns");
        // More G1s than the 17 the collapse produced (24 distinct G1-containing turns exist).
        Assert.True(schedule.Count(s => s.Race.Grade == RaceGrade.G1) > 20,
            $"expected >20 G1s scheduled, got {schedule.Count(s => s.Race.Grade == RaceGrade.G1)}");
        // Ordered by turn (year, then month, then half).
        Assert.Equal(
            schedule.OrderBy(s => s.Race.Year).ThenBy(s => s.Race.Month).ThenBy(s => s.Race.Half).Select(s => (s.Race.Year, s.Race.Month, s.Race.Half)),
            schedule.Select(s => (s.Race.Year, s.Race.Month, s.Race.Half)));
    }
}
