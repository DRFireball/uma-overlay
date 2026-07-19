using System.IO;
using System.Text.Json;

namespace UmaCompanion.App;

/// <summary>
/// Durable account-wide set of won race ids. Wins gathered from result screens and Trophy Room
/// sync survive restarts, so the missing-trophy view is a true tracker rather than a per-session
/// scratchpad. Storage is a plain JSON int array — trivially inspectable and hand-editable.
/// </summary>
public sealed class WonRaceStore
{
    private readonly string _path;

    public WonRaceStore(string path)
    {
        _path = path;
    }

    public static WonRaceStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UmaCompanion", "won-races.json"));

    public IReadOnlySet<int> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new HashSet<int>();
            }
            var ids = JsonSerializer.Deserialize<int[]>(File.ReadAllText(_path));
            return ids is null ? new HashSet<int>() : new HashSet<int>(ids);
        }
        catch (Exception)
        {
            return new HashSet<int>(); // corrupt/unreadable file: start empty, never crash
        }
    }

    public void Save(IEnumerable<int> wonRaceIds)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(wonRaceIds.Order().ToArray()));
        }
        catch (Exception)
        {
            // Transient write failure: the set is still in memory; the next win retries.
        }
    }
}
