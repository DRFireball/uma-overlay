using UmaCompanion.Core.Reading;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class RaceNameMatcherTests
{
    private static RaceNameMatcher BuildMatcher() => new(new[]
    {
        (1001, "February Stakes"),
        (1005, "Satsuki Sho"),
        (1010, "Tokyo Yushun (Japanese Derby)"),
        (1013, "Japan Cup"),
        (1014, "Arima Kinen"),
    });

    [Fact]
    public void FindBest_ExactName_Matches()
    {
        var match = BuildMatcher().FindBest("Japan Cup");

        Assert.NotNull(match);
        Assert.Equal(1013, match.RaceId);
        Assert.Equal(0, match.Distance);
    }

    [Fact]
    public void FindBest_NameEmbeddedInSurroundingScreenText_Matches()
    {
        var match = BuildMatcher().FindBest("G1 Japan Cup 2400m Turf");

        Assert.NotNull(match);
        Assert.Equal(1013, match.RaceId);
    }

    [Fact]
    public void FindBest_OcrNoiseWithinTolerance_MatchesLongName()
    {
        // 29-char name ("tokyoyushunjapanesederby" normalized = 24) tolerates a mangled character.
        var match = BuildMatcher().FindBest("Tokyo Yushun (Japanese Derbv)");

        Assert.NotNull(match);
        Assert.Equal(1010, match.RaceId);
    }

    [Fact]
    public void FindBest_ShortNameRequiresExactMatch()
    {
        // "japancup" normalized is 8 chars → tolerance 1; two errors must not match.
        Assert.Null(BuildMatcher().FindBest("Jopan Cvp"));
    }

    [Fact]
    public void FindBest_UnknownText_ReturnsNull()
    {
        Assert.Null(BuildMatcher().FindBest("Training Results"));
    }

    [Fact]
    public void FindBest_EmptyOrPunctuationOnly_ReturnsNull()
    {
        var matcher = BuildMatcher();
        Assert.Null(matcher.FindBest(""));
        Assert.Null(matcher.FindBest("--- !!! ---"));
    }

    [Fact]
    public void FindBest_CaseAndSpacingDifferences_AreIgnored()
    {
        var match = BuildMatcher().FindBest("ARIMA  KINEN");

        Assert.NotNull(match);
        Assert.Equal(1014, match.RaceId);
        Assert.Equal(0, match.Distance);
    }

    [Theory]
    [InlineData("Musashino S.", 2001, "Musashino Stakes")]
    [InlineData("Challenge C.", 2002, "Challenge Cup")]
    [InlineData("Sparking Lady Cup", 2003, "Sparking Lady Cup")] // full form still matches
    [InlineData("Mile Ch.", 2004, "Mile Championship")]
    [InlineData("Hanshin J.F.", 2005, "Hanshin Juvenile Fillies")]
    [InlineData("Asahi Hai F.S.", 2006, "Asahi Hai Futurity Stakes")]
    public void FindBest_TrophyRoomAbbreviations_MatchFullNames(string label, int expectedId, string name)
    {
        var matcher = new RaceNameMatcher(new[]
        {
            (2001, "Musashino Stakes"), (2002, "Challenge Cup"), (2003, "Sparking Lady Cup"),
            (2004, "Mile Championship"), (2005, "Hanshin Juvenile Fillies"),
            (2006, "Asahi Hai Futurity Stakes"),
        });

        var match = matcher.FindBest(label);

        Assert.NotNull(match);
        Assert.Equal(expectedId, match.RaceId);
        Assert.Equal(name, match.Name);
    }

    [Fact]
    public void FindBest_DuplicateNames_PreferTheLowestRaceId()
    {
        // master.mdb repeats names for legend-race variants; the career race is the low id.
        var matcher = new RaceNameMatcher(new[] { (5920, "Japan Cup"), (1019, "Japan Cup") });

        var match = matcher.FindBest("Japan Cup");

        Assert.NotNull(match);
        Assert.Equal(1019, match.RaceId);
    }

    [Fact]
    public void FindBest_AbbreviatedMasterDuplicate_LosesToTheFullCareerName()
    {
        // master.mdb has BOTH "Asahi Hai Futurity Stakes" (career, 1022) and a variant literally
        // named "Asahi Hai F.S." (5960). The trophy label must resolve to the career race.
        var matcher = new RaceNameMatcher(new[]
        {
            (5960, "Asahi Hai F.S."), (1022, "Asahi Hai Futurity Stakes"),
        });

        var match = matcher.FindBest("Asahi Hai F.S.");

        Assert.NotNull(match);
        Assert.Equal(1022, match.RaceId);
    }

    [Fact]
    public void SubstringEditDistance_PatternInsideText_IsZero()
    {
        Assert.Equal(0, RaceNameMatcher.SubstringEditDistance("japancup", "g1japancup2400m"));
    }

    [Fact]
    public void SubstringEditDistance_OneSubstitution_IsOne()
    {
        Assert.Equal(1, RaceNameMatcher.SubstringEditDistance("japancup", "g1japancvp2400m"));
    }
}
