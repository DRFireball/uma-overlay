namespace UmaCompanion.Core.Trophies;

/// <summary>
/// Computes which trophy races a character still needs to win.
/// Pure logic: reference race data comes from master.mdb, won-race data from career/roster state.
/// </summary>
public sealed class TrophyTracker
{
    /// <summary>
    /// Returns the trophy races the character has not yet won, preserving the input order of
    /// <paramref name="allTrophyRaces"/>.
    /// </summary>
    public IReadOnlyList<TrophyRace> MissingRaces(
        IEnumerable<TrophyRace> allTrophyRaces,
        IEnumerable<int> wonRaceIds)
    {
        var won = new HashSet<int>(wonRaceIds);
        return allTrophyRaces.Where(race => !won.Contains(race.RaceId)).ToList();
    }
}
