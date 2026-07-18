namespace UmaCompanion.Core.Races;

/// <summary>In-game aptitude letter grades, ordered worst (G) to best (S).</summary>
public enum AptitudeGrade
{
    G,
    F,
    E,
    D,
    C,
    B,
    A,
    S,
}

/// <summary>
/// A character's distance and surface aptitudes. Grades not supplied are treated as G (worst),
/// so a partial set is safe to pass.
/// </summary>
public sealed record CharacterAptitudes(
    IReadOnlyDictionary<DistanceCategory, AptitudeGrade> Distance,
    IReadOnlyDictionary<Surface, AptitudeGrade> Surface)
{
    /// <summary>
    /// The character's effective aptitude for a race: the weaker of its distance and surface
    /// aptitudes, since a race is only as runnable as its worst matching aptitude.
    /// </summary>
    public AptitudeGrade For(CareerRace race)
    {
        var distance = Distance.TryGetValue(race.DistanceCategory, out var d) ? d : AptitudeGrade.G;
        var surface = Surface.TryGetValue(race.Surface, out var s) ? s : AptitudeGrade.G;
        return (AptitudeGrade)Math.Min((int)distance, (int)surface);
    }
}
