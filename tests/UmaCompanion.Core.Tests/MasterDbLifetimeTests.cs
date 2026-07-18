using Microsoft.Data.Sqlite;
using UmaCompanion.Core.MasterData;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class MasterDbLifetimeTests
{
    private static string CreateMiniMasterMdb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"uma-test-src-{Guid.NewGuid():N}.mdb");
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE race (id INTEGER PRIMARY KEY, grade INTEGER);
            CREATE TABLE race_instance (id INTEGER PRIMARY KEY, race_id INTEGER);
            CREATE TABLE race_trophy (id INTEGER PRIMARY KEY, trophy_id INTEGER, race_instance_id INTEGER, disp_order INTEGER);
            CREATE TABLE text_data (category INTEGER, "index" INTEGER, text TEXT);
            """;
        cmd.ExecuteNonQuery();
        SqliteConnection.ClearPool(conn);
        return path;
    }

    [Fact]
    public void Dispose_DeletesTempSnapshotCopy()
    {
        var source = CreateMiniMasterMdb();
        try
        {
            string tempCopy;
            using (var db = MasterDb.OpenReadOnlyCopy(source))
            {
                tempCopy = Assert.IsType<string>(db.TempCopyPath);
                Assert.True(File.Exists(tempCopy), "snapshot copy should exist while open");
            }

            Assert.False(File.Exists(tempCopy), "snapshot copy should be deleted on dispose");
        }
        finally
        {
            File.Delete(source);
        }
    }
}
