using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Races;

namespace UmaCompanion.Core.Trophies;

/// <summary>
/// End-to-end trophy feature: given the races a character has won, reports the trophies it is
/// still missing and a race schedule that hunts them. Holds the static reference data (all trophy
/// races + the career calendar) once, then answers against live won-race state.
/// </summary>
public sealed class TrophyPlanner
{
    private readonly IReadOnlyList<TrophyRace> _allTrophyRaces;
    private readonly IReadOnlyList<CareerRace> _calendar;
    private readonly TrophyTracker _tracker = new();
    private readonly RaceScheduler _scheduler = new();

    public TrophyPlanner(IReadOnlyList<TrophyRace> allTrophyRaces, IReadOnlyList<CareerRace> calendar)
    {
        _allTrophyRaces = allTrophyRaces;
        _calendar = calendar;
    }

    /// <summary>Loads the static reference data (trophy races + career calendar) from master.mdb.</summary>
    public static TrophyPlanner FromMasterDb(MasterDb db)
        => new(db.GetTrophyRaces(), db.GetCareerRaces());

    /// <summary>The trophy races this character has not yet won.</summary>
    public IReadOnlyList<TrophyRace> MissingTrophies(IEnumerable<int> wonRaceIds)
        => _tracker.MissingRaces(_allTrophyRaces, wonRaceIds);

    /// <summary>
    /// A career schedule that runs the races filling this character's missing trophies. Trophy
    /// races that never appear on the career calendar (e.g. legend-mode races) are naturally
    /// excluded, since they cannot be scheduled.
    /// </summary>
    public IReadOnlyList<ScheduledRace> MissingTrophySchedule(IEnumerable<int> wonRaceIds)
    {
        var missingIds = MissingTrophies(wonRaceIds).Select(r => r.RaceId).ToHashSet();
        return _scheduler.MissingTrophySchedule(_calendar, missingIds);
    }
}
