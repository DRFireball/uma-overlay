using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

/// <summary>
/// Exercises the reader against a real master.mdb when one is available. Point at a copy via the
/// UMA_MASTER_MDB env var (CI has none, so these self-skip by passing trivially).
/// </summary>
public class MasterDbIntegrationTests
{
    private static string? RealDbPath =>
        Environment.GetEnvironmentVariable("UMA_MASTER_MDB") is { Length: > 0 } p && File.Exists(p) ? p : null;

    [Fact]
    public void GetTrophyRaces_AgainstRealDb_ReturnsManyG1RacesWithNames()
    {
        var path = RealDbPath;
        if (path is null) return; // no real DB available in this environment

        var db = MasterDb.OpenReadOnlyCopy(path);
        var races = db.GetTrophyRaces();

        Assert.True(races.Count > 50, $"expected many trophy races, got {races.Count}");
        Assert.All(races, r => Assert.False(string.IsNullOrWhiteSpace(r.Name)));
        Assert.Contains(races, r => r.Name.Contains("Arima Kinen") && r.Grade == RaceGrade.G1);
    }
}
