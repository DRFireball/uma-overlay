using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

/// <summary>
/// Exercises the reader against a real master.mdb when one is available. Point at a copy via the
/// UMA_MASTER_MDB env var; without it the test reports as Skipped (never silently green).
/// </summary>
public class MasterDbIntegrationTests
{
    [SkippableFact]
    public void GetTrophyRaces_AgainstRealDb_ReturnsManyG1RacesWithNames()
    {
        var path = Environment.GetEnvironmentVariable("UMA_MASTER_MDB");
        Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path),
            "UMA_MASTER_MDB is not set or does not point at an existing master.mdb");

        using var db = MasterDb.OpenReadOnlyCopy(path!);
        var races = db.GetTrophyRaces();

        Assert.True(races.Count > 50, $"expected many trophy races, got {races.Count}");
        Assert.All(races, r => Assert.False(string.IsNullOrWhiteSpace(r.Name)));
        Assert.Contains(races, r => r.Name.Contains("Arima Kinen") && r.Grade == RaceGrade.G1);
    }
}
