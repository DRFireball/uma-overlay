using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using UmaCompanion.Core.Races;
using UmaCompanion.Core.Skills;
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
    /// The trophy races as the in-game Trophy Room shows them: one entry per named race. The
    /// raw <c>race_trophy</c> table carries far more rows than the room displays — legend/scenario
    /// variants that repeat a race's name (Japan Cup appears four times), junk duplicates whose
    /// whole name is the trophy abbreviation ("Hanshin J.F." beside "Hanshin Juvenile Fillies"),
    /// and unnamed data artifacts. Those are collapsed to the canonical race (the lowest race id
    /// for a name — the career race), so the count matches the game rather than inflating it.
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

        var byName = new Dictionary<string, TrophyRace>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(2))
            {
                continue; // unnamed data row — not a Trophy Room entry
            }
            var name = reader.GetString(2);
            if (IsAbbreviationName(name))
            {
                continue; // junk duplicate named after the trophy abbreviation
            }
            var raceId = reader.GetInt32(0);
            var key = CanonicalNameKey(name);
            if (!byName.TryGetValue(key, out var existing) || raceId < existing.RaceId)
            {
                byName[key] = new TrophyRace(raceId, name, MapGrade(reader.GetInt32(1)));
            }
        }
        return byName.Values.OrderBy(r => r.RaceId).ToList();
    }

    private static string CanonicalNameKey(string name)
        => new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    // A name whose final token is a pure run of single-letter initials ("S.", "F.S.", "J.F.") is
    // a bare trophy abbreviation, not a canonical race name (the "St." of "St. Lite Kinen" isn't).
    private static readonly Regex InitialsOnlyToken = new(@"^([A-Za-z]\.)+$", RegexOptions.Compiled);

    private static bool IsAbbreviationName(string name)
    {
        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && InitialsOnlyToken.IsMatch(tokens[^1]);
    }

    /// <summary>
    /// Every race on the career (single-mode) calendar, with grade, career year, turn
    /// (month + half), distance/surface, and fan requirement — the input the scheduler reasons over.
    /// </summary>
    public IReadOnlyList<CareerRace> GetCareerRaces()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT sp.race_permission, ri.race_id, r.grade, sp.month, sp.half,
                   cs.distance, cs.ground, sp.need_fan_count, td.text
            FROM single_mode_program sp
            JOIN race_instance ri ON ri.id = sp.race_instance_id
            JOIN race r ON r.id = ri.race_id
            JOIN race_course_set cs ON cs.id = r.course_set
            LEFT JOIN text_data td ON td.category = $nameCat AND td."index" = ri.race_id
            ORDER BY sp.month, sp.half, ri.race_id
            """;
        cmd.Parameters.AddWithValue("$nameCat", RaceNameCategory);

        var races = new List<CareerRace>();
        // A race can appear under both Senior permissions (3 and 4) for the same turn; collapse
        // those to one entry per (year, race, turn).
        var seen = new HashSet<(CareerYear, int, int, RaceHalf)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var year = MapYear(reader.GetInt32(0));
            var raceId = reader.GetInt32(1);
            var month = reader.GetInt32(3);
            var half = reader.GetInt32(4) == 1 ? RaceHalf.Early : RaceHalf.Late;
            if (!seen.Add((year, raceId, month, half)))
            {
                continue;
            }

            var distance = reader.GetInt32(5);
            races.Add(new CareerRace(
                RaceId: raceId,
                Name: reader.IsDBNull(8) ? $"Race {raceId}" : reader.GetString(8),
                Grade: MapGrade(reader.GetInt32(2)),
                Year: year,
                Month: month,
                Half: half,
                Distance: distance,
                Surface: reader.GetInt32(6) == 1 ? Surface.Turf : Surface.Dirt,
                DistanceCategory: CareerRace.CategoryFor(distance),
                NeedFanCount: reader.GetInt32(7)));
        }
        return races;
    }

    // text_data category holding skill names, keyed by skill id.
    private const int SkillNameCategory = 47;

    /// <summary>
    /// Every skill that can be bought in a career run (i.e. has a skill-point cost), with its name,
    /// rank/evaluation value, and cost. Returns skills raw, including debuffs (negative value); the
    /// <see cref="SkillPurchaseRecommender"/> is responsible for filtering.
    /// </summary>
    public IReadOnlyList<PurchasableSkill> GetPurchasableSkills()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT sd.id, td.text, sd.grade_value, np.need_skill_point
            FROM skill_data sd
            JOIN single_mode_skill_need_point np ON np.id = sd.id
            LEFT JOIN text_data td ON td.category = $nameCat AND td."index" = sd.id
            ORDER BY sd.id
            """;
        cmd.Parameters.AddWithValue("$nameCat", SkillNameCategory);

        var skills = new List<PurchasableSkill>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            skills.Add(new PurchasableSkill(
                SkillId: id,
                Name: reader.IsDBNull(1) ? $"Skill {id}" : reader.GetString(1),
                RankValue: reader.GetInt32(2),
                SkillPointCost: reader.GetInt32(3)));
        }
        return skills;
    }

    // single_mode_program.race_permission encodes the career year a race is offered in:
    // 1 = Junior (months 7-12), 2 = Classic (full year), 3 and 4 = Senior (two program subsets
    // that together cover the Senior year). Verified against the Global master.mdb.
    private static CareerYear MapYear(int permission) => permission switch
    {
        1 => CareerYear.Junior,
        2 => CareerYear.Classic,
        _ => CareerYear.Senior,
    };

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
