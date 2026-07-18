using UmaCompanion.Core.Skills;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class SkillPurchaseRecommenderTests
{
    private static PurchasableSkill Skill(int id, int rankValue, int cost)
        => new(id, $"Skill {id}", rankValue, cost);

    [Fact]
    public void Rank_OrdersByRankValuePerSkillPoint_AndExcludesDebuffsAndZeroValue()
    {
        var skills = new[]
        {
            Skill(1, 200, 100),   // 2.0 per SP
            Skill(2, 180, 60),    // 3.0 per SP  → best
            Skill(3, -500, 100),  // debuff, excluded
            Skill(4, 240, 120),   // 2.0 per SP, higher raw value than #1
            Skill(5, 0, 40),      // zero value, excluded
        };

        var ranked = new SkillPurchaseRecommender().Rank(skills);

        Assert.Equal(new[] { 2, 4, 1 }, ranked.Select(r => r.Skill.SkillId));
        Assert.Equal(3.0, ranked[0].ValuePerPoint, 3);
    }

    [Fact]
    public void RecommendWithinBudget_GreedilyPicksMostEfficientSkillsThatFit()
    {
        var skills = new[]
        {
            Skill(1, 200, 100),   // 2.0 per SP
            Skill(2, 180, 60),    // 3.0 per SP
            Skill(4, 240, 120),   // 2.0 per SP
        };

        // Budget 160: take #2 (60, most efficient), #4 (120) doesn't fit remaining 100, #1 (100) fits.
        var chosen = new SkillPurchaseRecommender().RecommendWithinBudget(skills, availableSkillPoints: 160);

        Assert.Equal(new[] { 2, 1 }, chosen.Select(s => s.SkillId));
    }

    [Fact]
    public void RecommendWithinBudget_ReturnsEmpty_WhenNothingAffordable()
    {
        var skills = new[] { Skill(1, 200, 100) };

        var chosen = new SkillPurchaseRecommender().RecommendWithinBudget(skills, availableSkillPoints: 50);

        Assert.Empty(chosen);
    }
}
