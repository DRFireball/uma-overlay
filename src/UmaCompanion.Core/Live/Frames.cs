using System.Text.Json.Serialization;

namespace UmaCompanion.Core.Live;

/// <summary>Base type for all live-state frames pushed by the game hook over the WebSocket.</summary>
public abstract record GameFrame;

/// <summary>Five core stats, shared by career state and training previews.</summary>
public sealed record Stats(
    [property: JsonPropertyName("speed")] int Speed,
    [property: JsonPropertyName("stamina")] int Stamina,
    [property: JsonPropertyName("power")] int Power,
    [property: JsonPropertyName("guts")] int Guts,
    [property: JsonPropertyName("wiz")] int Wiz);

public sealed record TrainingGain(
    [property: JsonPropertyName("speed")] int Speed,
    [property: JsonPropertyName("stamina")] int Stamina,
    [property: JsonPropertyName("power")] int Power,
    [property: JsonPropertyName("guts")] int Guts,
    [property: JsonPropertyName("wiz")] int Wiz,
    [property: JsonPropertyName("skill_pts")] int SkillPts);

public sealed record SupportOnFacility(
    [property: JsonPropertyName("support_card_id")] int SupportCardId,
    [property: JsonPropertyName("bond")] int Bond,
    [property: JsonPropertyName("hint")] bool Hint);

public sealed record TrainingOption(
    [property: JsonPropertyName("facility")] string Facility,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("gains")] TrainingGain Gains,
    [property: JsonPropertyName("fail_pct")] int FailPct,
    [property: JsonPropertyName("supports")] IReadOnlyList<SupportOnFacility> Supports);

public sealed record CareerTurnFrame(
    [property: JsonPropertyName("turn")] int Turn,
    [property: JsonPropertyName("stats")] Stats Stats,
    [property: JsonPropertyName("energy")] int Energy,
    [property: JsonPropertyName("mood")] int Mood,
    [property: JsonPropertyName("skill_points")] int SkillPoints,
    [property: JsonPropertyName("fans")] int Fans,
    [property: JsonPropertyName("goal_race_id")] int? GoalRaceId,
    [property: JsonPropertyName("training_options")] IReadOnlyList<TrainingOption> TrainingOptions) : GameFrame;

public sealed record RaceResult(
    [property: JsonPropertyName("chara_id")] int CharaId,
    [property: JsonPropertyName("race_id")] int RaceId,
    [property: JsonPropertyName("won")] bool Won);

public sealed record RaceResultsFrame(IReadOnlyList<RaceResult> Results) : GameFrame;
