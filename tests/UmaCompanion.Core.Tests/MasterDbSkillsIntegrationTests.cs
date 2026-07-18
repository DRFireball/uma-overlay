using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Skills;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class MasterDbSkillsIntegrationTests
{
    [SkippableFact]
    public void PurchasableSkills_FeedRecommender_ProduceSanePositiveRanking()
    {
        var path = Environment.GetEnvironmentVariable("UMA_MASTER_MDB");
        Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path),
            "UMA_MASTER_MDB is not set or does not point at an existing master.mdb");

        using var db = MasterDb.OpenReadOnlyCopy(path!);
        var skills = db.GetPurchasableSkills();
        var ranked = new SkillPurchaseRecommender().Rank(skills);

        Assert.True(skills.Count > 400, $"expected many purchasable skills, got {skills.Count}");
        Assert.All(ranked, r => Assert.True(r.Skill.RankValue > 0 && r.Skill.SkillPointCost > 0));
        Assert.All(ranked, r => Assert.False(string.IsNullOrWhiteSpace(r.Skill.Name)));
        // Ranking is non-increasing in value-per-point.
        for (var i = 1; i < ranked.Count; i++)
        {
            Assert.True(ranked[i - 1].ValuePerPoint >= ranked[i].ValuePerPoint);
        }
        // The debuff skills the raw query returns are filtered out of the ranking.
        Assert.True(skills.Any(s => s.RankValue <= 0));
        Assert.DoesNotContain(ranked, r => r.Skill.RankValue <= 0);
    }
}
