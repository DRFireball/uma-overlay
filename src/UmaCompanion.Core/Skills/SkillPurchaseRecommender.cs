namespace UmaCompanion.Core.Skills;

/// <summary>
/// Recommends which skills to buy to raise a character's rank most efficiently, ranking by
/// rank value per skill point. This is a pure master.mdb-data heuristic — no race simulation —
/// so it carries no third-party (GPL) licensing obligations.
/// </summary>
public sealed class SkillPurchaseRecommender
{
    /// <summary>
    /// Ranks skills by rank value per skill point, best first. Skills that cannot raise rank
    /// (non-positive rank value) or have no valid cost are excluded.
    /// </summary>
    public IReadOnlyList<RankedSkill> Rank(IEnumerable<PurchasableSkill> skills)
    {
        return skills
            .Where(s => s.RankValue > 0 && s.SkillPointCost > 0)
            .Select(s => new RankedSkill(s, (double)s.RankValue / s.SkillPointCost))
            .OrderByDescending(r => r.ValuePerPoint)
            .ThenByDescending(r => r.Skill.RankValue)   // tie-break: prefer larger raw rank gain
            .ThenBy(r => r.Skill.SkillPointCost)        // then the cheaper skill
            .ThenBy(r => r.Skill.SkillId)               // then deterministic
            .ToList();
    }

    /// <summary>
    /// Greedily selects the most rank-efficient skills that fit the available skill points, in
    /// purchase order. Greedy by efficiency (not an optimal knapsack): a skill that does not fit
    /// the remaining budget is skipped and cheaper efficient skills after it are still considered.
    /// </summary>
    public IReadOnlyList<PurchasableSkill> RecommendWithinBudget(
        IEnumerable<PurchasableSkill> skills, int availableSkillPoints)
    {
        var chosen = new List<PurchasableSkill>();
        var remaining = availableSkillPoints;
        foreach (var ranked in Rank(skills))
        {
            if (ranked.Skill.SkillPointCost <= remaining)
            {
                chosen.Add(ranked.Skill);
                remaining -= ranked.Skill.SkillPointCost;
            }
        }
        return chosen;
    }
}
