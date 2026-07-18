namespace UmaCompanion.Core.Skills;

/// <summary>
/// A skill a character can buy in a career run. <see cref="RankValue"/> is the skill's
/// contribution to the character's rank/evaluation score (master.mdb <c>grade_value</c>);
/// it is negative for debuff skills.
/// </summary>
public sealed record PurchasableSkill(int SkillId, string Name, int RankValue, int SkillPointCost);

/// <summary>A skill with its computed rank-value efficiency (rank value per skill point).</summary>
public sealed record RankedSkill(PurchasableSkill Skill, double ValuePerPoint);
