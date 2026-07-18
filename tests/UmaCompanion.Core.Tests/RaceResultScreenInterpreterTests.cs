using UmaCompanion.Core.Reading;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class RaceResultScreenInterpreterTests
{
    private static RaceResultScreenInterpreter BuildInterpreter() => new(new RaceNameMatcher(new[]
    {
        (1005, "Satsuki Sho"),
        (1010, "Tokyo Yushun (Japanese Derby)"),
        (1013, "Japan Cup"),
    }));

    [Fact]
    public void Interpret_WinScreen_ReturnsWonResult()
    {
        var result = BuildInterpreter().Interpret(new[] { "Japan Cup", "1st", "Special Week" });

        Assert.NotNull(result);
        Assert.Equal(1013, result.RaceId);
        Assert.True(result.Won);
    }

    [Fact]
    public void Interpret_SecondPlace_ReturnsNotWon()
    {
        var result = BuildInterpreter().Interpret(new[] { "Satsuki Sho", "2nd" });

        Assert.NotNull(result);
        Assert.Equal(1005, result.RaceId);
        Assert.False(result.Won);
    }

    [Fact]
    public void Interpret_NameAndPlacementOnSameLine_Works()
    {
        var result = BuildInterpreter().Interpret(new[] { "Japan Cup — 1st" });

        Assert.NotNull(result);
        Assert.True(result.Won);
    }

    [Fact]
    public void Interpret_OcrMisreads1stAsLst_StillCountsAsWin()
    {
        var result = BuildInterpreter().Interpret(new[] { "Tokyo Yushun (Japanese Derby)", "lst" });

        Assert.NotNull(result);
        Assert.Equal(1010, result.RaceId);
        Assert.True(result.Won);
    }

    [Fact]
    public void Interpret_RaceNameWithoutPlacement_IsNotAResult()
    {
        // The pre-race card shows the name but no finishing place — must not emit a result.
        Assert.Null(BuildInterpreter().Interpret(new[] { "Japan Cup", "2400m Turf" }));
    }

    [Fact]
    public void Interpret_PlacementWithoutKnownRace_IsNotAResult()
    {
        Assert.Null(BuildInterpreter().Interpret(new[] { "Practice Session", "1st" }));
    }

    [Fact]
    public void Interpret_UnknownCharacter_ReportsCharaIdZero()
    {
        var result = BuildInterpreter().Interpret(new[] { "Japan Cup", "1st" });

        Assert.NotNull(result);
        Assert.Equal(0, result.CharaId);
    }

    [Theory]
    [InlineData("1st", 1)]
    [InlineData("2nd", 2)]
    [InlineData("3rd", 3)]
    [InlineData("12th", 12)]
    [InlineData("2 nd", 2)]
    [InlineData("lst", 1)]
    [InlineData("Ist", 1)]
    [InlineData("!st", 1)]
    public void TryParsePlacement_RecognisesPlacementForms(string line, int expected)
    {
        Assert.Equal(expected, RaceResultScreenInterpreter.TryParsePlacement(line));
    }

    [Theory]
    [InlineData("Fastest lap")]       // "st" inside a word
    [InlineData("21st Century Cup")]  // beyond field size of 18
    [InlineData("2400m Turf")]
    [InlineData("")]
    public void TryParsePlacement_IgnoresNonPlacements(string line)
    {
        Assert.Null(RaceResultScreenInterpreter.TryParsePlacement(line));
    }
}
