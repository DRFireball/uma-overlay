using Microsoft.Data.Sqlite;
using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Races;
using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class MasterDbCareerRacesTests
{
    private static SqliteConnection BuildFixture()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE race (id INTEGER PRIMARY KEY, grade INTEGER, course_set INTEGER);
            CREATE TABLE race_instance (id INTEGER PRIMARY KEY, race_id INTEGER);
            CREATE TABLE race_course_set (id INTEGER PRIMARY KEY, distance INTEGER, ground INTEGER);
            CREATE TABLE single_mode_program (id INTEGER PRIMARY KEY, race_instance_id INTEGER, month INTEGER, half INTEGER, need_fan_count INTEGER);
            CREATE TABLE text_data (category INTEGER, "index" INTEGER, text TEXT);

            -- Sprint turf G1 in early June; Long dirt G3 in late December.
            INSERT INTO race_course_set VALUES (9001, 1200, 1), (9002, 2500, 2);
            INSERT INTO race VALUES (1001, 100, 9001), (1002, 300, 9002);
            INSERT INTO race_instance VALUES (100101, 1001), (100201, 1002);
            INSERT INTO single_mode_program VALUES (1, 100101, 6, 1, 12000), (2, 100201, 12, 2, 20000);
            INSERT INTO text_data VALUES (32, 1001, 'Yasuda Kinen'), (32, 1002, 'Long Dirt Test');
            """;
        cmd.ExecuteNonQuery();
        return conn;
    }

    [Fact]
    public void GetCareerRaces_MapsGradeSurfaceDistanceCategoryAndTurn()
    {
        using var conn = BuildFixture();
        using var db = new MasterDb(conn);

        var races = db.GetCareerRaces();

        Assert.Equal(2, races.Count);

        var sprint = races[0];
        Assert.Equal(1001, sprint.RaceId);
        Assert.Equal("Yasuda Kinen", sprint.Name);
        Assert.Equal(RaceGrade.G1, sprint.Grade);
        Assert.Equal(6, sprint.Month);
        Assert.Equal(RaceHalf.Early, sprint.Half);
        Assert.Equal(1200, sprint.Distance);
        Assert.Equal(Surface.Turf, sprint.Surface);
        Assert.Equal(DistanceCategory.Sprint, sprint.DistanceCategory);
        Assert.Equal(12000, sprint.NeedFanCount);

        var longDirt = races[1];
        Assert.Equal(RaceGrade.G3, longDirt.Grade);
        Assert.Equal(RaceHalf.Late, longDirt.Half);
        Assert.Equal(Surface.Dirt, longDirt.Surface);
        Assert.Equal(DistanceCategory.Long, longDirt.DistanceCategory);
    }

    [Theory]
    [InlineData(1400, DistanceCategory.Sprint)]
    [InlineData(1401, DistanceCategory.Mile)]
    [InlineData(1800, DistanceCategory.Mile)]
    [InlineData(2400, DistanceCategory.Medium)]
    [InlineData(2401, DistanceCategory.Long)]
    public void DistanceCategory_BoundariesMatchGameBuckets(int distance, DistanceCategory expected)
    {
        Assert.Equal(expected, CareerRace.CategoryFor(distance));
    }
}
