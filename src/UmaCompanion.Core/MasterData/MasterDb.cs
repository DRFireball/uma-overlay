using Microsoft.Data.Sqlite;
using UmaCompanion.Core.Trophies;

namespace UmaCompanion.Core.MasterData;

/// <summary>
/// Read-only accessor over the game's <c>master.mdb</c> SQLite database.
/// Callers own the connection lifetime; use <see cref="OpenReadOnlyCopy"/> for a real install.
/// </summary>
public sealed class MasterDb
{
    // text_data category holding full race names, keyed by race id.
    private const int RaceNameCategory = 32;

    private readonly SqliteConnection _connection;

    public MasterDb(SqliteConnection connection) => _connection = connection;

    /// <summary>
    /// Snapshot-copies the (possibly game-locked) master.mdb to a temp file and opens it read-only.
    /// </summary>
    public static MasterDb OpenReadOnlyCopy(string masterMdbPath)
    {
        var tempCopy = Path.Combine(Path.GetTempPath(), $"uma-master-{Guid.NewGuid():N}.mdb");
        File.Copy(masterMdbPath, tempCopy, overwrite: true);
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = tempCopy,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString());
        conn.Open();
        return new MasterDb(conn);
    }

    /// <summary>
    /// Every distinct trophy-eligible race, with its English name and grade, ordered by race id.
    /// Multiple trophy rows for the same race (e.g. Satsuki Sho) collapse to a single entry.
    /// </summary>
    public IReadOnlyList<TrophyRace> GetTrophyRaces()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT ri.race_id, r.grade, td.text
            FROM race_trophy rt
            JOIN race_instance ri ON ri.id = rt.race_instance_id
            JOIN race r ON r.id = ri.race_id
            LEFT JOIN text_data td ON td.category = $nameCat AND td."index" = ri.race_id
            ORDER BY ri.race_id
            """;
        cmd.Parameters.AddWithValue("$nameCat", RaceNameCategory);

        var races = new List<TrophyRace>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var raceId = reader.GetInt32(0);
            var grade = MapGrade(reader.GetInt32(1));
            var name = reader.IsDBNull(2) ? $"Race {raceId}" : reader.GetString(2);
            races.Add(new TrophyRace(raceId, name, grade));
        }
        return races;
    }

    private static RaceGrade MapGrade(int grade) => grade switch
    {
        100 => RaceGrade.G1,
        200 => RaceGrade.G2,
        300 => RaceGrade.G3,
        _ => RaceGrade.Ex,
    };
}
