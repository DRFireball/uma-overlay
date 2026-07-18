using System.IO;

namespace UmaCompanion.App;

/// <summary>
/// Finds the game's read-only <c>master.mdb</c> across its known install locations. The game
/// refreshes this file each patch; the app only ever snapshot-copies and reads it.
/// </summary>
public static class MasterMdbLocator
{
    /// <summary>The first existing master.mdb, or null if none of the known locations has one.</summary>
    public static string? Locate()
    {
        foreach (var path in CandidatePaths())
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

    private static IEnumerable<string> CandidatePaths()
    {
        // Explicit override wins — same env var the Core integration tests use.
        yield return Environment.GetEnvironmentVariable("UMA_MASTER_MDB") ?? string.Empty;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Cygames LocalLow install (Steam Global / DMM standalone).
        yield return Path.Combine(
            userProfile, "AppData", "LocalLow", "Cygames", "Umamusume", "master", "master.mdb");
    }
}
