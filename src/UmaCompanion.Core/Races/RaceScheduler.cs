using UmaCompanion.Core.Trophies;

namespace UmaCompanion.Core.Races;

/// <summary>A race chosen for a turn slot, with the score that won it that slot.</summary>
public sealed record ScheduledRace(CareerRace Race, double Score);

/// <summary>
/// Builds a career race schedule from the calendar in three modes. All three share one
/// mechanism — pick the highest-scoring race per turn slot — and differ only in the scoring
/// function, so the modes stay consistent (one race per slot, finales excluded, ordered by turn).
/// </summary>
public sealed class RaceScheduler
{
    /// <summary>
    /// Whether a calendar race is a real user-schedulable race. Excludes entries with no fan
    /// requirement: the scenario finales (URA Finale / Twinkle Star Climax, which auto-adapt to
    /// the character's aptitude) and the forced maiden / Make Debut races — none of which are a
    /// schedulable choice.
    /// </summary>
    public static bool IsSchedulable(CareerRace race) => race.NeedFanCount > 0;

    /// <summary>Maximise prestige: schedule the highest-grade race available each slot.</summary>
    public IReadOnlyList<ScheduledRace> G1MaxSchedule(IEnumerable<CareerRace> calendar)
        => BestPerSlot(calendar, race => Prestige(race.Grade));

    /// <summary>Schedule only races that fill a missing trophy, best grade first within a slot.</summary>
    public IReadOnlyList<ScheduledRace> MissingTrophySchedule(
        IEnumerable<CareerRace> calendar, IReadOnlySet<int> missingTrophyRaceIds)
        => BestPerSlot(calendar,
            race => missingTrophyRaceIds.Contains(race.RaceId) ? Prestige(race.Grade) : null);

    /// <summary>Schedule the best-aptitude race each slot for this character.</summary>
    public IReadOnlyList<ScheduledRace> AptitudeSchedule(
        IEnumerable<CareerRace> calendar, CharacterAptitudes aptitudes)
        => BestPerSlot(calendar, race => (int)aptitudes.For(race));

    private static double Prestige(RaceGrade grade) => grade switch
    {
        RaceGrade.G1 => 3,
        RaceGrade.G2 => 2,
        RaceGrade.G3 => 1,
        _ => 0,
    };

    private static IReadOnlyList<ScheduledRace> BestPerSlot(
        IEnumerable<CareerRace> calendar, Func<CareerRace, double?> score)
    {
        return calendar
            .Where(IsSchedulable)
            .Select(race => (race, score: score(race)))
            .Where(x => x.score is not null)
            // One race per turn: a turn is a (career year, month, half) — the same calendar month
            // in different years is a different turn, so recurring races are kept in each year.
            .GroupBy(x => (x.race.Year, x.race.Month, x.race.Half))
            .Select(slot => slot
                .OrderByDescending(x => x.score!.Value)
                .ThenBy(x => (int)x.race.Grade)   // tie-break: prefer the higher race grade
                .ThenBy(x => x.race.RaceId)       // final tie-break: deterministic
                .First())
            .OrderBy(x => x.race.Year).ThenBy(x => x.race.Month).ThenBy(x => x.race.Half)
            .Select(x => new ScheduledRace(x.race, x.score!.Value))
            .ToList();
    }
}
