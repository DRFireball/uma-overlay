using UmaCompanion.Core.Reading;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class RaceResultScreenInterpreterTests
{
    // The condition banner is what identifies the result screen; every positive case needs one.
    private const string Banner = "Kyoto Turf 2000m (Medium) Right / Inner";

    private static RaceResultScreenInterpreter BuildInterpreter() => new(new RaceNameMatcher(new[]
    {
        (1005, "Satsuki Sho"),
        (1010, "Tokyo Yushun (Japanese Derby)"),
        (1013, "Japan Cup"),
    }));

    [Fact]
    public void Interpret_WinScreen_ReturnsWonResult()
    {
        var result = BuildInterpreter().Interpret(new[] { "1st", "Japan Cup", Banner, "Special Week" });

        Assert.NotNull(result);
        Assert.Equal(1013, result.RaceId);
        Assert.True(result.Won);
    }

    [Fact]
    public void Interpret_SecondPlace_ReturnsNotWon()
    {
        var result = BuildInterpreter().Interpret(new[] { "2nd", "Satsuki Sho", Banner });

        Assert.NotNull(result);
        Assert.Equal(1005, result.RaceId);
        Assert.False(result.Won);
    }

    [Fact]
    public void Interpret_NameAndBannerOnSameLine_Works()
    {
        var result = BuildInterpreter().Interpret(new[] { "Japan Cup " + Banner, "1st" });

        Assert.NotNull(result);
        Assert.Equal(1013, result.RaceId);
        Assert.True(result.Won);
    }

    [Fact]
    public void Interpret_OcrMisreads1stAsLst_StillCountsAsWin()
    {
        var result = BuildInterpreter().Interpret(
            new[] { "lst", "Tokyo Yushun (Japanese Derby)", Banner });

        Assert.NotNull(result);
        Assert.Equal(1010, result.RaceId);
        Assert.True(result.Won);
    }

    [Fact]
    public void Interpret_RaceNameWithoutPlacement_IsNotAResult()
    {
        // The results screen before the placements render — no place yet, so no result.
        Assert.Null(BuildInterpreter().Interpret(new[] { "Japan Cup", Banner }));
    }

    [Fact]
    public void Interpret_PlacementWithoutKnownRace_IsNotAResult()
    {
        Assert.Null(BuildInterpreter().Interpret(new[] { "Practice Session", Banner, "1st" }));
    }

    [Fact]
    public void Interpret_WithoutTheConditionBanner_IsNeverAResult()
    {
        // The gate: a name and a placement alone are not enough. Whatever screen this is, it is
        // not the result screen, so nothing is read from it.
        Assert.Null(BuildInterpreter().Interpret(new[] { "Japan Cup", "1st" }));
    }

    [Fact]
    public void Interpret_CareerGoalList_IsNotAResult()
    {
        // Verbatim from the goal screen that produced a false Japanese Oaks win: "Place 1st in …"
        // parses as a placement and another goal names a real race. No banner, so it cannot fire.
        var interpreter = BuildInterpreter();

        Assert.Null(interpreter.Interpret(new[]
        {
            "5 turn(s) until your next goal race! Let's do it!",
            "Place top 5 in Japanese Oaks",
            "Place top 3 in Satsuki Sho",
            "Place 1st in Queen Elizabeth II Cup",
            "Goals Achieved",
            "7/10",
        }));
    }

    [Fact]
    public void Interpret_PreRaceCard_IsNotAResult()
    {
        // The race-start card shows the grade, the race name and "Turf" as an aptitude grade — but
        // never surface and distance on one line.
        var interpreter = BuildInterpreter();

        Assert.Null(interpreter.Interpret(new[]
        {
            "GI", "Japan Cup", "No. 3 Fav", "Turf", "Long", "Late S", "1st", "Race",
        }));
    }

    [Fact]
    public void Interpret_RaceNameFarFromTheBanner_IsIgnored()
    {
        // Commentary and runner rows sit well below the banner; only the pill above it is the race.
        var result = BuildInterpreter().Interpret(new[]
        {
            "1st", "Japan Cup", Banner, "a", "b", "c", "d", "Satsuki Sho",
        });

        Assert.NotNull(result);
        Assert.Equal(1013, result.RaceId);
    }

    [Fact]
    public void Interpret_EventNameResemblingAbbreviatedTrophy_IsNotAResult()
    {
        // "Champions Meeting" resembles the "Champions C." trophy label, but result screens use
        // full names — even on a real result screen it must not register as Champions Cup.
        var interpreter = new RaceResultScreenInterpreter(new RaceNameMatcher(new[]
        {
            (1020, "Champions Cup"), (1013, "Japan Cup"),
        }));

        Assert.Null(interpreter.Interpret(new[] { "Champions Meeting", Banner, "1st" }));
    }

    [Fact]
    public void Interpret_UnknownCharacter_ReportsCharaIdZero()
    {
        var result = BuildInterpreter().Interpret(new[] { "1st", "Japan Cup", Banner });

        Assert.NotNull(result);
        Assert.Equal(0, result.CharaId);
    }

    [Theory]
    [InlineData("Kyoto Turf 2000m (Medium) Right / Inner")]
    [InlineData("Nakayama Dirt 1800m (Mile) Left")]
    [InlineData("Tokyo Turf 2400 m (Long) Left")]
    public void IsRaceResultScreen_RecognisesTheConditionBanner(string banner)
    {
        Assert.True(RaceResultScreenInterpreter.IsRaceResultScreen(new[] { "Japan Cup", banner }));
    }

    [Theory]
    [InlineData("Turf")]              // aptitude grade on the pre-race card
    [InlineData("2000m")]             // distance with no surface
    [InlineData("Turf A")]
    [InlineData("861")]
    [InlineData("Place 1st in Queen Elizabeth II Cup")]
    public void IsRaceResultScreen_RejectsEverythingElse(string line)
    {
        Assert.False(RaceResultScreenInterpreter.IsRaceResultScreen(new[] { "Japan Cup", line }));
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
