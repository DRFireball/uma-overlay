using System.Text.RegularExpressions;
using UmaCompanion.Core.Live;

namespace UmaCompanion.Core.Reading;

/// <summary>
/// Turns the OCR'd text of the race result screen into a <see cref="RaceResult"/>.
///
/// Detection is strictly gated: the lines must first be IDENTIFIED as the result screen (see
/// <see cref="IsRaceResultScreen"/>) before any race name or placement is read. Without that gate
/// every screen in the game is a candidate, and several of them look like results to a fuzzy
/// matcher — the career goal list ("Place 1st in Fuchu Umamusume S." above "…Japanese Oaks")
/// parses as a name plus a placement, and the pre-race card shows a grade and a race name. Rather
/// than special-case each of those, nothing outside a positively identified screen is read at all.
///
/// The screen does not identify the trainee, so <see cref="RaceResult.CharaId"/> is 0.
/// </summary>
public sealed partial class RaceResultScreenInterpreter
{
    // "1st" / "2nd" / "3rd" / "12th", tolerating the classic OCR misreads of 1 as l, I, or !.
    // Suffix is not cross-checked against the number: OCR mangles suffixes more often than digits.
    [GeneratedRegex(@"(?<![a-z0-9])(?<num>\d{1,2}|[lI!])\s?(st|nd|rd|th)(?![a-z])",
        RegexOptions.IgnoreCase)]
    private static partial Regex PlacementRegex();

    // The result screen's condition banner: "Kyoto Turf 2000m (Medium) Right / Inner". Surface and
    // distance must share ONE line — "Turf" alone is an aptitude grade on the pre-race card, and a
    // bare number is a stat. No other screen prints the pair together.
    [GeneratedRegex(@"\b(turf|dirt)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SurfaceRegex();

    [GeneratedRegex(@"\b\d{3,4}\s?m\b", RegexOptions.IgnoreCase)]
    private static partial Regex DistanceRegex();

    // How far above the condition banner the race-name pill can sit. The pill is the line directly
    // above it; the slack absorbs OCR emitting stray fragments of the artwork in between.
    private const int NameSearchWindow = 3;

    private readonly RaceNameMatcher _matcher;

    public RaceResultScreenInterpreter(RaceNameMatcher matcher)
    {
        _matcher = matcher;
    }

    /// <summary>
    /// Whether these lines are the race result screen. This is the on/off switch for result
    /// detection: anything else — home, goal list, pre-race card, training — reads as false.
    /// </summary>
    public static bool IsRaceResultScreen(IReadOnlyList<string> ocrLines)
        => ocrLines.Any(IsConditionBanner);

    private static bool IsConditionBanner(string line)
        => SurfaceRegex().IsMatch(line) && DistanceRegex().IsMatch(line);

    /// <summary>
    /// The race result shown on screen, or null when this is not the result screen or the name and
    /// placement cannot both be read from it.
    /// </summary>
    public RaceResult? Interpret(IReadOnlyList<string> ocrLines)
    {
        if (!IsRaceResultScreen(ocrLines))
        {
            return null;
        }

        var race = RaceOnResultScreen(ocrLines);
        if (race is null)
        {
            return null;
        }

        int? place = null;
        foreach (var line in ocrLines)
        {
            place ??= TryParsePlacement(line);
        }

        return place is not null
            ? new RaceResult(CharaId: 0, RaceId: race.RaceId, Won: place == 1)
            : null;
    }

    /// <summary>
    /// Finds the race name in the pill immediately above the condition banner. Scoping the search
    /// to those lines — rather than the whole screen — keeps runner names, commentary and the
    /// assistant's dialogue from fuzzily resolving to some unrelated race.
    /// </summary>
    public RaceNameMatch? RaceOnResultScreen(IReadOnlyList<string> ocrLines)
    {
        RaceNameMatch? best = null;

        for (var i = 0; i < ocrLines.Count; i++)
        {
            if (!IsConditionBanner(ocrLines[i]))
            {
                continue;
            }

            // The banner itself is included: OCR sometimes joins the pill onto it.
            for (var j = Math.Max(0, i - NameSearchWindow); j <= i; j++)
            {
                // Full names only — the result screen never shows the Trophy Room's abbreviations,
                // and matching those lets event names ("Champions Meeting") masquerade as a race.
                var candidate = _matcher.FindBest(ocrLines[j], includeAbbreviations: false);
                if (candidate is not null && (best is null || candidate.Distance < best.Distance))
                {
                    best = candidate;
                }
            }
        }

        return best;
    }

    /// <summary>The finishing place in a line ("1st", "2 nd", "lst"…), or null if none appears.</summary>
    internal static int? TryParsePlacement(string line)
    {
        var match = PlacementRegex().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var num = match.Groups["num"].Value;
        var place = char.IsDigit(num[0]) ? int.Parse(num) : 1; // l / I / ! are all misread 1s
        // Fields cap at 18 runners; larger numbers are something else on screen (a year, a time).
        return place is >= 1 and <= 18 ? place : null;
    }
}
