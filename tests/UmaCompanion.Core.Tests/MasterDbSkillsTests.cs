using Microsoft.Data.Sqlite;
using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Skills;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class MasterDbSkillsTests
{
    private static SqliteConnection BuildFixture()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE skill_data (id INTEGER PRIMARY KEY, grade_value INTEGER);
            CREATE TABLE single_mode_skill_need_point (id INTEGER PRIMARY KEY, need_skill_point INTEGER);
            CREATE TABLE text_data (category INTEGER, "index" INTEGER, text TEXT);

            INSERT INTO skill_data VALUES (200011, 240), (200012, 129), (200013, -500), (200099, 300);
            -- 200099 has no need_point row → not career-purchasable → excluded by the join.
            INSERT INTO single_mode_skill_need_point VALUES (200011, 110), (200012, 90), (200013, 100);
            INSERT INTO text_data VALUES
                (47, 200011, 'Corner Adept ◎'), (47, 200012, 'Straightaway Adept'),
                (47, 200013, 'Slow Metabolism');
            """;
        cmd.ExecuteNonQuery();
        return conn;
    }

    [Fact]
    public void GetPurchasableSkills_ReturnsSkillsWithCareerCost_NameValueAndCost()
    {
        using var conn = BuildFixture();
        using var db = new MasterDb(conn);

        var skills = db.GetPurchasableSkills();

        Assert.Equal(3, skills.Count); // 200099 excluded (no career SP cost)
        var corner = Assert.Single(skills, s => s.SkillId == 200011);
        Assert.Equal("Corner Adept ◎", corner.Name);
        Assert.Equal(240, corner.RankValue);
        Assert.Equal(110, corner.SkillPointCost);
        // Debuff skill is returned raw (the recommender filters, not the data accessor).
        Assert.Contains(skills, s => s.SkillId == 200013 && s.RankValue == -500);
    }

    [Fact]
    public void GetPurchasableSkills_FeedsRecommender_RankingByValuePerPoint()
    {
        using var conn = BuildFixture();
        using var db = new MasterDb(conn);

        var ranked = new SkillPurchaseRecommender().Rank(db.GetPurchasableSkills());

        // 200012: 129/90 = 1.43; 200011: 240/110 = 2.18; debuff excluded → order 200011, 200012.
        Assert.Equal(new[] { 200011, 200012 }, ranked.Select(r => r.Skill.SkillId));
    }
}
