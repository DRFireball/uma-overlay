using Microsoft.Data.Sqlite;
using UmaCompanion.Core.Trophies;

namespace UmaCompanion.Core.MasterData;

/// <summary>
/// Read-only accessor over the game's <c>master.mdb</c> SQLite database.
/// When constructed via <see cref="OpenReadOnlyCopy"/>, disposing closes the connection and
/// deletes the temp snapshot; when constructed with an external connection, the caller owns it.
/// </summary>
public sealed class MasterDb : IDisposable
{
    // text_data category holding full race names, keyed by race id.
    private const int RaceNameCategory = 32;

    private readonly SqliteConnection _connection;
    private readonly bool _ownsConnection;

    public MasterDb(SqliteConnection connection) : this(connection, ownsConnection: false, tempCopyPath: null)
    {
    }

    private MasterDb(SqliteConnection connection, bool ownsConnection, string? tempCopyPath)
    {
        _connection = connection;
        _ownsConnection = ownsConnection;
        TempCopyPath = tempCopyPath;
    }

    /// <summary>Path of the temp snapshot backing this instance, or null for external connections.</summary>
    public string? TempCopyPath { get; }

    /// <summary>
    /// Snapshot-copies the (possibly game-locked) master.mdb to a temp file and opens it read-only.
    /// Dispose the returned instance to close the connection and delete the snapshot.
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
        return new MasterDb(conn, ownsConnection: true, tempCopyPath: tempCopy);
    }

    public void Dispose()
    {
        if (!_ownsConnection)
        {
            return;
        }

        _connection.Dispose();
        // Sqlite pools native handles per connection string; clear so the file is unlocked for delete.
        SqliteConnection.ClearPool(_connection);
        if (TempCopyPath is not null)
        {
            File.Delete(TempCopyPath);
        }
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

    // As of the 2026-07 Global master.mdb, every race reachable via race_trophy has grade
    // 100/200/300 (verified by query), so Ex is a forward-compat fallback, not a live bucket.
    private static RaceGrade MapGrade(int grade) => grade switch
    {
        100 => RaceGrade.G1,
        200 => RaceGrade.G2,
        300 => RaceGrade.G3,
        _ => RaceGrade.Ex,
    };
}
