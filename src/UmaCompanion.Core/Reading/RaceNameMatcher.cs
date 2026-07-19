using System.Globalization;
using System.Text;

namespace UmaCompanion.Core.Reading;

/// <summary>A known race matched against an OCR'd line, with the edit distance that matched it.</summary>
public sealed record RaceNameMatch(int RaceId, string Name, int Distance);

/// <summary>
/// Matches noisy OCR text against the known race names from master.mdb. OCR output routinely
/// mangles a character or two and surrounds the name with other screen text, so matching is
/// fuzzy (bounded edit distance) and substring-based (the name may appear anywhere in the line).
/// Each race also matches under the game's label abbreviations as used in the Trophy Room —
/// "Musashino S.", "Champions C.", "Mile Ch.", "Hanshin J.F.", "Asahi Hai F.S.".
/// </summary>
public sealed class RaceNameMatcher
{
    // Words the game's trophy labels abbreviate, observed on real Trophy Room captures.
    private static readonly Dictionary<string, string> WordAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Stakes"] = "S",
        ["Cup"] = "C",
        ["Championship"] = "Ch",
        ["Juvenile"] = "J",
        ["Fillies"] = "F",
        ["Futurity"] = "F",
    };

    private readonly List<(int RaceId, string Name, string[] Patterns)> _races;

    public RaceNameMatcher(IEnumerable<(int RaceId, string Name)> races)
    {
        _races = races
            .GroupBy(r => r.RaceId)
            .Select(g => g.First())
            .Select(r => (r.RaceId, r.Name, BuildPatterns(r.Name)))
            .Where(r => r.Item3.Length > 0)
            .ToList();
    }

    /// <summary>The full normalized name plus its abbreviated form when they differ.</summary>
    private static string[] BuildPatterns(string name)
    {
        var full = Normalize(name);
        if (full.Length == 0)
        {
            return Array.Empty<string>();
        }
        var abbreviated = Normalize(string.Join(' ',
            name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => WordAbbreviations.GetValueOrDefault(word, word))));
        return abbreviated == full ? new[] { full } : new[] { full, abbreviated };
    }

    /// <summary>
    /// The best race whose name (or abbreviation) approximately appears in
    /// <paramref name="ocrLine"/>, or null when nothing matches within tolerance. Tolerance
    /// scales with pattern length (roughly one error per eight characters), so short names must
    /// match exactly and long names absorb OCR noise.
    /// </summary>
    public RaceNameMatch? FindBest(string ocrLine)
    {
        var line = Normalize(ocrLine);
        if (line.Length == 0)
        {
            return null;
        }

        RaceNameMatch? best = null;
        foreach (var (raceId, name, patterns) in _races)
        {
            foreach (var pattern in patterns)
            {
                var distance = SubstringEditDistance(pattern, line);
                if (distance > pattern.Length / 8)
                {
                    continue;
                }
                // Prefer the closest match; on equal distance the longer (more specific) name
                // wins — "Tokyo Yushun (Japanese Derby)" must beat a shorter embedded name.
                // On a full tie take the LOWEST race id: master.mdb repeats names for legend-race
                // variants (Japan Cup = 1019 and 5920…), and the career race is the low id.
                if (best is null
                    || distance < best.Distance
                    || (distance == best.Distance && name.Length > best.Name.Length)
                    || (distance == best.Distance && name.Length == best.Name.Length && raceId < best.RaceId))
                {
                    best = new RaceNameMatch(raceId, name, distance);
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Lowercases, folds diacritics (OCR loves reading "Cup" as "Cüp"), and strips everything but
    /// letters and digits, collapsing OCR spacing noise.
    /// </summary>
    internal static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue; // the accent split off a base letter — drop it
            }
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Minimum edit distance between <paramref name="pattern"/> and any substring of
    /// <paramref name="text"/> (semi-global alignment: the pattern pays for its own edits, but
    /// skipping text before/after the match is free).
    /// </summary>
    internal static int SubstringEditDistance(string pattern, string text)
    {
        var previous = new int[text.Length + 1]; // dp row for pattern prefix i-1; row 0 is all zeros
        var current = new int[text.Length + 1];

        for (var i = 1; i <= pattern.Length; i++)
        {
            current[0] = i; // consuming pattern with no text costs deletions
            for (var j = 1; j <= text.Length; j++)
            {
                var substitution = previous[j - 1] + (pattern[i - 1] == text[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }
            (previous, current) = (current, previous);
        }
        return previous.Min();
    }
}
