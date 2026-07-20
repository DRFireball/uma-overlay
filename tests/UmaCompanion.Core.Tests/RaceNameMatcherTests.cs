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
    [InlineData("Mile Ch.", 2004, "Mile Championship")]          // trailing 2-letter prefix
    [InlineData("Hanshin J.F.", 2005, "Hanshin Juvenile Fillies")] // two trailing words -> initials
    [InlineData("Asahi Hai F.S.", 2006, "Asahi Hai Futurity Stakes")]
    [InlineData("Victoria M.", 2007, "Victoria Mile")]           // positional: trailing word dropped
    [InlineData("NHK Mile C.", 2008, "NHK Mile Cup")]            // same word (Mile) kept, not dropped
    public void FindBest_TrophyRoomAbbreviations_MatchFullNames(string label, int expectedId, string name)
    {
        var matcher = new RaceNameMatcher(new[]
        {
            (2001, "Musashino Stakes"), (2002, "Challenge Cup"), (2003, "Sparking Lady Cup"),
            (2004, "Mile Championship"), (2005, "Hanshin Juvenile Fillies"),
            (2006, "Asahi Hai Futurity Stakes"), (2007, "Victoria Mile"), (2008, "NHK Mile Cup"),
        });

        var match = matcher.FindBest(label);

        Assert.NotNull(match);
        Assert.Equal(expectedId, match.RaceId);
        Assert.Equal(name, match.Name);
    }

    [Theory]
    [InlineData("Rose S.", 2022, "Rose Stakes")]  // trailing word cut to ONE letter: "roses", 5 chars
    [InlineData("Fuji S.", 2029, "Fuji Stakes")]
    public void FindBest_SingleLetterAbbreviations_Match(string label, int expectedId, string name)
    {
        // These were dropped wholesale by a six-character floor on abbreviated patterns, so two
        // real G2 trophies could never be checked off however clearly they were captured.
        var matcher = new RaceNameMatcher(new[] { (2022, "Rose Stakes"), (2029, "Fuji Stakes") });

        var match = matcher.FindBest(label);

        Assert.NotNull(match);
        Assert.Equal(expectedId, match.RaceId);
        Assert.Equal(name, match.Name);
    }

    [Fact]
    public void FindBest_AbbreviationMustMatchTheEndOfTheLabel()
    {
        // OCR read the "Kyoto Kinen" label as "Kyoto-Kineri". Kyoto Kimpai's abbreviation
        // ("kyotoki") sits INSIDE that at distance 0 and so beat Kyoto Kinen's own full name at
        // distance 1 — a G3 trophy appearing on a G2 page. Abbreviations truncate the tail of a
        // name, so they only ever run to the end of a label; anchoring them there settles it.
        var matcher = new RaceNameMatcher(new[] { (3001, "Kyoto Kimpai"), (2004, "Kyoto Kinen") });

        var match = matcher.FindBest("Kyoto-Kineri");

        Assert.NotNull(match);
        Assert.Equal(2004, match.RaceId);
    }

    [Fact]
    public void FindBest_AbbreviationAfterASponsorPrefix_StillMatches()
    {
        // The room labels this "Yomiuri Milers C." but master.mdb only knows "Milers Cup", so text
        // BEFORE the abbreviation has to stay free — only trailing text is disqualifying.
        var matcher = new RaceNameMatcher(new[] { (2014, "Milers Cup") });

        var match = matcher.FindBest("Yomiuri Milers C.");

        Assert.NotNull(match);
        Assert.Equal(2014, match.RaceId);
    }

    [Fact]
    public void SuffixEditDistance_PatternAtTheEnd_IsZero()
    {
        Assert.Equal(0, RaceNameMatcher.SuffixEditDistance("milersc", "yomiurimilersc"));
    }

    [Fact]
    public void SuffixEditDistance_TrailingTextIsPaidFor()
    {
        // Free as a substring, but four unexplained trailing characters as a suffix.
        Assert.Equal(0, RaceNameMatcher.SubstringEditDistance("kyotoki", "kyotokineri"));
        Assert.Equal(4, RaceNameMatcher.SuffixEditDistance("kyotoki", "kyotokineri"));
    }

    [Fact]
    public void FindBest_WithoutAbbreviations_RejectsWordPrefixCollisions()
    {
        // "Champions Meeting" once matched the "championsc" abbreviation of Champions Cup at d=1.
        // Suffix anchoring now rejects it even with abbreviations enabled — the label does not end
        // there — but full-name matching must independently reject it too: that is the guarantee
        // the result screen relies on, and it must not quietly become a side effect of anchoring.
        var matcher = new RaceNameMatcher(new[] { (1020, "Champions Cup") });

        Assert.Null(matcher.FindBest("Champions Meeting", includeAbbreviations: true));
        Assert.Null(matcher.FindBest("Champions Meeting", includeAbbreviations: false));
    }

    [Fact]
    public void FindBest_ShortFullNameShownInFull_DoesNotAbbreviate()
    {
        // "Oka Sho" is short enough to always display in full; its initials must not become a
        // loose pattern ("okas") that risks matching unrelated screen text.
        var matcher = new RaceNameMatcher(new[] { (2020, "Oka Sho") });

        Assert.Null(matcher.FindBest("Oka S."));
        Assert.NotNull(matcher.FindBest("Oka Sho"));
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

    [Theory]
    [InlineData("Hanshin J.F.")]   // junk duplicate of "Hanshin Juvenile Fillies"
    [InlineData("Asahi Hai F.S.")] // junk duplicate of "Asahi Hai Futurity Stakes"
    [InlineData("Sprinters S.")]   // junk duplicate of "Sprinters Stakes"
    public void AbbreviationOnlyNames_AreExcludedEntirely(string junkName)
    {
        // Even matched against ONLY the junk entry, its own label must not resolve — otherwise a
        // garbled OCR of the real race's label sneaks a second, duplicate id onto the page.
        var matcher = new RaceNameMatcher(new[] { (5970, junkName) });

        Assert.Null(matcher.FindBest(junkName));
    }

    [Fact]
    public void FindBest_RealNameWithLeadingAbbreviation_IsNotExcluded()
    {
        // "St. Lite Kinen" contains "St." but is a real race — only trailing pure-initial tokens
        // mark the junk duplicates.
        var matcher = new RaceNameMatcher(new[] { (2023, "St. Lite Kinen") });

        var match = matcher.FindBest("St. Lite Kinen");

        Assert.NotNull(match);
        Assert.Equal(2023, match.RaceId);
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
