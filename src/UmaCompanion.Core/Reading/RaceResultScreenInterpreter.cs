using System.Text.RegularExpressions;
using UmaCompanion.Core.Live;

namespace UmaCompanion.Core.Reading;

/// <summary>
/// Turns the OCR'd text of the race result screen into a <see cref="RaceResult"/>: finds the race
/// name (via <see cref="RaceNameMatcher"/>) and the finishing place anywhere in the lines. Both
/// must be present — a screen with a race name but no placement (e.g. the pre-race card) is not a
/// result. The screen does not identify the trainee, so <see cref="RaceResult.CharaId"/> is 0.
/// </summary>
public sealed partial class RaceResultScreenInterpreter
{
    // "1st" / "2nd" / "3rd" / "12th", tolerating the classic OCR misreads of 1 as l, I, or !.
    // Suffix is not cross-checked against the number: OCR mangles suffixes more often than digits.
    [GeneratedRegex(@"(?<![a-z0-9])(?<num>\d{1,2}|[lI!])\s?(st|nd|rd|th)(?![a-z])",
        RegexOptions.IgnoreCase)]
    private static partial Regex PlacementRegex();

    private readonly RaceNameMatcher _matcher;

    public RaceResultScreenInterpreter(RaceNameMatcher matcher)
    {
        _matcher = matcher;
    }

    /// <summary>
    /// The race result shown on screen, or null when the lines do not contain both a known race
    /// name and a finishing place.
    /// </summary>
    public RaceResult? Interpret(IReadOnlyList<string> ocrLines)
    {
        RaceNameMatch? race = null;
        int? place = null;

        foreach (var line in ocrLines)
        {
            // Keep the closest race match across ALL lines: a later exact match must beat an
            // earlier line that only fuzzily resembled some other race.
            var candidate = _matcher.FindBest(line);
            if (candidate is not null && (race is null || candidate.Distance < race.Distance))
            {
                race = candidate;
            }
            place ??= TryParsePlacement(line);
        }

        return race is not null && place is not null
            ? new RaceResult(CharaId: 0, RaceId: race.RaceId, Won: place == 1)
            : null;
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
