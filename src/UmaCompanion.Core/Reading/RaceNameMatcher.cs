using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UmaCompanion.Core.Reading;

/// <summary>A known race matched against an OCR'd line, with the edit distance that matched it.</summary>
public sealed record RaceNameMatch(int RaceId, string Name, int Distance);

/// <summary>
/// Matches noisy OCR text against the known race names from master.mdb. OCR output routinely
/// mangles a character or two and surrounds the name with other screen text, so matching is
/// fuzzy (bounded edit distance) and substring-based (the name may appear anywhere in the line).
/// Each race also matches under the game's Trophy Room label abbreviations, which truncate a
/// suffix of words to a 1-2 letter prefix to fit a fixed width — "Musashino S.", "Champions C.",
/// "Mile Ch.", "Victoria M.", "Hanshin J.F.", "Asahi Hai F.S." — modelled generally rather than
/// per-word, since the same word abbreviates or not depending on position ("NHK Mile Cup" ->
/// "NHK Mile C." keeps Mile, but "Victoria Mile" -> "Victoria M." drops it).
/// </summary>
public sealed class RaceNameMatcher
{
    // Abbreviated patterns below this length match as substrings inside unrelated labels ("milec"
    // landing inside "nhkmilec"), so over-abbreviated forms are dropped. This alone also keeps
    // truly short names out (Oka Sho -> "okas"/"okash" are both too short), so names that the game
    // always shows in full contribute no loose prefix patterns.
    private const int MinAbbreviationLength = 6;

    // A final token that is a pure run of single-letter initials ("S.", "F.S.", "J.F."). master.mdb
    // carries junk legend-race duplicates whose whole NAME is the trophy abbreviation ("Hanshin
    // J.F." alongside the real "Hanshin Juvenile Fillies"); matching to those double-counts a page.
    // The two-char "St." of "St. Lite Kinen" is not a single-letter initial, so real names survive.
    private static readonly Regex InitialsOnlyToken = new(@"^([A-Za-z]\.)+$", RegexOptions.Compiled);

    private readonly List<(int RaceId, string Name, string[] Patterns)> _races;

    public RaceNameMatcher(IEnumerable<(int RaceId, string Name)> races)
    {
        _races = races
            .GroupBy(r => r.RaceId)
            .Select(g => g.First())
            .Where(r => !IsAbbreviationName(r.Name))
            .Select(r => (r.RaceId, r.Name, BuildPatterns(r.Name)))
            .Where(r => r.Item3.Length > 0)
            .ToList();
    }

    /// <summary>Whether a name is a bare trophy abbreviation (its last token is only initials).</summary>
    private static bool IsAbbreviationName(string name)
    {
        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && InitialsOnlyToken.IsMatch(tokens[^1]);
    }

    /// <summary>
    /// The full normalized name plus every trophy-label abbreviation: keep the first i words in
    /// full and truncate each remaining word to its first 1 or 2 letters, for every split point.
    /// </summary>
    private static string[] BuildPatterns(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize).Where(w => w.Length > 0).ToArray();
        var full = string.Concat(words);
        if (full.Length == 0)
        {
            return Array.Empty<string>();
        }

        var patterns = new HashSet<string> { full };

        // Parenthetical alias: the game labels "Tokyo Yushun (Japanese Derby)" as just
        // "Japanese Derby". Add the inner text as its own pattern, but only when it is multi-word
        // — a single-word qualifier like "(Spring)"/"(Autumn)" would collide across variants and
        // is disambiguated by the full name instead.
        var alias = Normalize(ParentheticalContent(name, out var innerHasSpace));
        if (innerHasSpace && alias.Length >= MinAbbreviationLength)
        {
            patterns.Add(alias);
        }

        if (words.Length >= 2)
        {
            for (var i = 1; i < words.Length; i++) // words[0..i) kept full, words[i..] abbreviated
            {
                var head = string.Concat(words.Take(i));
                foreach (var prefixLen in new[] { 1, 2 })
                {
                    var tail = string.Concat(words.Skip(i).Select(w => w[..Math.Min(prefixLen, w.Length)]));
                    var pattern = head + tail;
                    if (pattern.Length >= MinAbbreviationLength)
                    {
                        patterns.Add(pattern);
                    }
                }
            }
        }
        return patterns.ToArray();
    }

    /// <summary>The text inside the first (…) group, or empty; reports whether it has a space.</summary>
    private static string ParentheticalContent(string name, out bool hasSpace)
    {
        hasSpace = false;
        var open = name.IndexOf('(');
        var close = open >= 0 ? name.IndexOf(')', open + 1) : -1;
        if (close < 0)
        {
            return string.Empty;
        }
        var inner = name[(open + 1)..close];
        hasSpace = inner.Contains(' ');
        return inner;
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
        var bestPatternLength = 0;
        foreach (var (raceId, name, patterns) in _races)
        {
            foreach (var pattern in patterns)
            {
                var distance = SubstringEditDistance(pattern, line);
                if (distance > pattern.Length / 8)
                {
                    continue;
                }
                // Rank by, in order: closeness; then the longer MATCHED pattern (a full
                // "nhkmilec" beats another race's stray "milec" substring); then the longer name
                // ("Tokyo Yushun (Japanese Derby)" over a shorter embedded name); then the LOWEST
                // race id — master.mdb repeats names for legend variants (Japan Cup = 1019 and
                // 5920…) and the career race is the low id.
                var better = best is null
                    || distance < best.Distance
                    || (distance == best.Distance && pattern.Length > bestPatternLength)
                    || (distance == best.Distance && pattern.Length == bestPatternLength && name.Length > best.Name.Length)
                    || (distance == best.Distance && pattern.Length == bestPatternLength && name.Length == best.Name.Length && raceId < best.RaceId);
                if (better)
                {
                    best = new RaceNameMatch(raceId, name, distance);
                    bestPatternLength = pattern.Length;
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
