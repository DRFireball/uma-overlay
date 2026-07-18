using UmaCompanion.Core.Live;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class FrameParserTests
{
    [Fact]
    public void Parse_CareerTurnFrame_ExtractsStatsAndTrainingOptions()
    {
        const string json = """
            { "v": 1, "type": "career_turn", "payload": {
                "turn": 24,
                "stats": { "speed": 412, "stamina": 301, "power": 288, "guts": 190, "wiz": 240 },
                "energy": 62, "mood": 4, "skill_points": 145, "fans": 12040,
                "goal_race_id": 1001,
                "training_options": [
                  { "facility": "speed", "level": 3,
                    "gains": { "speed": 24, "power": 8, "skill_pts": 4 },
                    "fail_pct": 5,
                    "supports": [ { "support_card_id": 30028, "bond": 74, "hint": true } ] } ] } }
            """;

        var frame = Assert.IsType<CareerTurnFrame>(FrameParser.Parse(json));

        Assert.Equal(24, frame.Turn);
        Assert.Equal(412, frame.Stats.Speed);
        Assert.Equal(240, frame.Stats.Wiz);
        Assert.Equal(62, frame.Energy);
        Assert.Equal(1001, frame.GoalRaceId);
        var opt = Assert.Single(frame.TrainingOptions);
        Assert.Equal("speed", opt.Facility);
        Assert.Equal(24, opt.Gains.Speed);
        Assert.Equal(5, opt.FailPct);
        var support = Assert.Single(opt.Supports);
        Assert.Equal(30028, support.SupportCardId);
        Assert.True(support.Hint);
    }

    [Fact]
    public void Parse_RaceResultsFrame_ExtractsWonRaces()
    {
        const string json = """
            { "v": 1, "type": "race_results", "payload": [
                { "chara_id": 1003, "race_id": 1001, "won": true },
                { "chara_id": 1003, "race_id": 1002, "won": false } ] }
            """;

        var frame = Assert.IsType<RaceResultsFrame>(FrameParser.Parse(json));

        Assert.Equal(2, frame.Results.Count);
        Assert.Equal(1001, frame.Results[0].RaceId);
        Assert.True(frame.Results[0].Won);
        Assert.False(frame.Results[1].Won);
    }

    [Fact]
    public void Parse_UnknownType_ReturnsNull()
    {
        Assert.Null(FrameParser.Parse("""{ "v": 1, "type": "future_thing", "payload": {} }"""));
    }

    [Fact]
    public void Parse_NullGoalRaceId_IsAllowed()
    {
        const string json = """
            { "v": 1, "type": "career_turn", "payload": {
                "turn": 1, "stats": {"speed":100,"stamina":100,"power":100,"guts":100,"wiz":100},
                "energy": 100, "mood": 3, "skill_points": 0, "fans": 0,
                "goal_race_id": null, "training_options": [] } }
            """;

        var frame = Assert.IsType<CareerTurnFrame>(FrameParser.Parse(json));

        Assert.Null(frame.GoalRaceId);
        Assert.Empty(frame.TrainingOptions);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsNull()
    {
        Assert.Null(FrameParser.Parse("not json at all"));
    }
}
