using Microsoft.Data.Sqlite;
using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class MasterDbTrophyTests
{
    // Builds a minimal in-memory master.mdb-shaped DB with just the tables the trophy query needs.
    private static SqliteConnection BuildFixture()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        Exec(conn, """
            CREATE TABLE race (id INTEGER PRIMARY KEY, grade INTEGER);
            CREATE TABLE race_instance (id INTEGER PRIMARY KEY, race_id INTEGER);
            CREATE TABLE race_trophy (id INTEGER PRIMARY KEY, trophy_id INTEGER, race_instance_id INTEGER, disp_order INTEGER);
            CREATE TABLE text_data (category INTEGER, "index" INTEGER, text TEXT);

            INSERT INTO race VALUES (1001, 100), (1002, 200), (1003, 300);
            INSERT INTO race_instance VALUES (100101, 1001), (100201, 1002), (100301, 1003);
            -- Satsuki-style: three trophy rows for the same race must collapse to one TrophyRace.
            INSERT INTO race_trophy VALUES (1,1,100101,3), (2,2,100101,2), (3,3,100101,1), (4,4,100201,5), (5,5,100301,6);
            INSERT INTO text_data VALUES (32,1001,'Satsuki Sho'), (32,1002,'Takarazuka Kinen'), (32,1003,'Some G3');
            """);
        return conn;
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void GetTrophyRaces_ReturnsOneRacePerDistinctRace_WithNameAndGrade()
    {
        using var conn = BuildFixture();
        var db = new MasterDb(conn);

        var races = db.GetTrophyRaces();

        Assert.Equal(new[]
        {
            new TrophyRace(1001, "Satsuki Sho", RaceGrade.G1),
            new TrophyRace(1002, "Takarazuka Kinen", RaceGrade.G2),
            new TrophyRace(1003, "Some G3", RaceGrade.G3),
        }, races);
    }
}
