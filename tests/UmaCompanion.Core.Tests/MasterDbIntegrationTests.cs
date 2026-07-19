using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Races;
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

        // Collapsed to the game's Trophy Room set (one per named race), not the raw race_trophy
        // rows (~267). G2/G3 tabs match the game exactly (42/76); total is well under the old 267.
        Assert.InRange(races.Count, 145, 165);
        Assert.Equal(42, races.Count(r => r.Grade == RaceGrade.G2));
        Assert.Equal(76, races.Count(r => r.Grade == RaceGrade.G3));
        Assert.All(races, r => Assert.False(string.IsNullOrWhiteSpace(r.Name)));
        Assert.Contains(races, r => r.Name.Contains("Arima Kinen") && r.Grade == RaceGrade.G1);
        // Duplicate names collapse to one entry at the career id; junk abbreviations are gone.
        Assert.Equal(new[] { 1019 }, races.Where(r => r.Name == "Japan Cup").Select(r => r.RaceId));
        Assert.DoesNotContain(races, r => r.Name == "Hanshin J.F.");
    }

    [SkippableFact]
    public void GetCareerRaces_AgainstRealDb_HasArimaKinenWithCorrectAttributes()
    {
        var path = Environment.GetEnvironmentVariable("UMA_MASTER_MDB");
        Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path),
            "UMA_MASTER_MDB is not set or does not point at an existing master.mdb");

        using var db = MasterDb.OpenReadOnlyCopy(path!);
        var races = db.GetCareerRaces();

        var arima = races.FirstOrDefault(r => r.Name == "Arima Kinen");
        Assert.NotNull(arima);
        Assert.Equal(RaceGrade.G1, arima.Grade);
        Assert.Equal(12, arima.Month);
        Assert.Equal(RaceHalf.Late, arima.Half);
        Assert.Equal(2500, arima.Distance);
        Assert.Equal(Surface.Turf, arima.Surface);
        Assert.Equal(DistanceCategory.Long, arima.DistanceCategory);

        // Arima Kinen recurs in Classic and Senior years (not Junior), each exactly once.
        var arimaYears = races.Where(r => r.Name == "Arima Kinen").Select(r => r.Year).OrderBy(y => y).ToList();
        Assert.Equal(new[] { CareerYear.Classic, CareerYear.Senior }, arimaYears);
        // The Triple Crown is Classic-year only.
        foreach (var tc in new[] { "Satsuki Sho", "Tokyo Yushun (Japanese Derby)", "Kikuka Sho" })
        {
            var years = races.Where(r => r.Name == tc).Select(r => r.Year).Distinct().ToList();
            Assert.Equal(new[] { CareerYear.Classic }, years);
        }
    }
}
