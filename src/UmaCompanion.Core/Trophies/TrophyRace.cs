namespace UmaCompanion.Core.Trophies;

/// <summary>Race grade, as used for trophy categorisation in the Trophy Room.</summary>
public enum RaceGrade
{
    G1,
    G2,
    G3,
    Ex,
}

/// <summary>A trophy-eligible race, identified by its master.mdb race/program id.</summary>
public sealed record TrophyRace(int RaceId, string Name, RaceGrade Grade);
