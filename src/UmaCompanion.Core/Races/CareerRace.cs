using UmaCompanion.Core.Trophies;

namespace UmaCompanion.Core.Races;

public enum Surface
{
    Turf,
    Dirt,
}

public enum RaceHalf
{
    Early,
    Late,
}

/// <summary>Aptitude distance buckets, matching the game's Sprint/Mile/Medium/Long grades.</summary>
public enum DistanceCategory
{
    Sprint,
    Mile,
    Medium,
    Long,
}

/// <summary>A race on the career calendar, with everything the scheduler needs to reason about it.</summary>
public sealed record CareerRace(
    int RaceId,
    string Name,
    RaceGrade Grade,
    int Month,
    RaceHalf Half,
    int Distance,
    Surface Surface,
    DistanceCategory DistanceCategory,
    int NeedFanCount)
{
    /// <summary>Maps a distance in metres to its aptitude bucket (Sprint ≤1400 … Long ≥2401).</summary>
    public static DistanceCategory CategoryFor(int distanceMetres) => distanceMetres switch
    {
        <= 1400 => DistanceCategory.Sprint,
        <= 1800 => DistanceCategory.Mile,
        <= 2400 => DistanceCategory.Medium,
        _ => DistanceCategory.Long,
    };
}
