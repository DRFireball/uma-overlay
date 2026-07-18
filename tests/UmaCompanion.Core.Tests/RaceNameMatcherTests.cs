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
