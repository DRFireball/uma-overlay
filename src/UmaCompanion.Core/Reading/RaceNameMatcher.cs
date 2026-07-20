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
    // Floor on abbreviated pattern length. Five, not six: the game abbreviates a trailing word to a
    // SINGLE letter ("Rose Stakes" -> "Rose S." -> "roses", "Fuji S." -> "fujis"), and a six-char
    // floor silently dropped every such label. Anything this short is still safe because
    // abbreviations must match at the END of the label (see SuffixEditDistance).
    private const int MinAbbreviationLength = 5;

    // The parenthetical alias is a full display name, not an abbreviation, and is matched anywhere
    // in a line — so it keeps the stricter floor.
    private const int MinAliasLength = 6;

    // A final token that is a pure run of single-letter initials ("S.", "F.S.", "J.F."). master.mdb
    // carries junk legend-race duplicates whose whole NAME is the trophy abbreviation ("Hanshin
    // J.F." alongside the real "Hanshin Juvenile Fillies"); matching to those double-counts a page.
    // The two-char "St." of "St. Lite Kinen" is not a single-letter initial, so real names survive.
    private static readonly Regex InitialsOnlyToken = new(@"^([A-Za-z]\.)+$", RegexOptions.Compiled);

    // Canonical = the full name plus any parenthetical alias (real display forms, on every screen).
    // Abbreviated = the trophy-label prefix truncations, which appear ONLY in the Trophy Room.
    private readonly List<(int RaceId, string Name, string[] Canonical, string[] Abbreviated)> _races;

    public RaceNameMatcher(IEnumerable<(int RaceId, string Name)> races)
    {
        _races = races
            .GroupBy(r => r.RaceId)
            .Select(g => g.First())
            .Where(r => !IsAbbreviationName(r.Name))
            .Select(r =>
            {
                var (canonical, abbreviated) = BuildPatterns(r.Name);
                return (r.RaceId, r.Name, canonical, abbreviated);
            })
            .Where(r => r.canonical.Length > 0)
            .ToList();
    }

    /// <summary>Whether a name is a bare trophy abbreviation (its last token is only initials).</summary>
    private static bool IsAbbreviationName(string name)
    {
        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && InitialsOnlyToken.IsMatch(tokens[^1]);
    }

    /// <summary>
    /// Builds the canonical patterns (full name + parenthetical alias — real display forms) and,
    /// separately, the trophy-label abbreviations (first i words kept, the rest truncated to 1-2
    /// letters, for every split point). Abbreviations are matched only where they occur, the
    /// Trophy Room, so "Champions Meeting" can't collide with "Champions C." off a result screen.
    /// </summary>
    private static (string[] Canonical, string[] Abbreviated) BuildPatterns(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize).Where(w => w.Length > 0).ToArray();
        var full = string.Concat(words);
        if (full.Length == 0)
        {
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        var canonical = new HashSet<string> { full };

        // Parenthetical alias: the game labels "Tokyo Yushun (Japanese Derby)" as just
        // "Japanese Derby". Add the inner text as its own pattern, but only when it is multi-word
        // — a single-word qualifier like "(Spring)"/"(Autumn)" would collide across variants and
        // is disambiguated by the full name instead.
        var alias = Normalize(ParentheticalContent(name, out var innerHasSpace));
        if (innerHasSpace && alias.Length >= MinAliasLength)
        {
            canonical.Add(alias);
        }

        var abbreviated = new HashSet<string>();
        for (var i = 1; i < words.Length; i++) // words[0..i) kept full, words[i..] abbreviated
        {
            var head = string.Concat(words.Take(i));
            foreach (var prefixLen in new[] { 1, 2 })
            {
                var tail = string.Concat(words.Skip(i).Select(w => w[..Math.Min(prefixLen, w.Length)]));
                var pattern = head + tail;
                if (pattern.Length >= MinAbbreviationLength && !canonical.Contains(pattern))
                {
                    abbreviated.Add(pattern);
                }
            }
        }
        return (canonical.ToArray(), abbreviated.ToArray());
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
    /// The best race whose name approximately appears in <paramref name="ocrLine"/>, or null when
    /// nothing matches within tolerance. Tolerance scales with pattern length (roughly one error
    /// per eight characters), so short names must match exactly and long names absorb OCR noise.
    /// <paramref name="includeAbbreviations"/> adds the Trophy Room's truncated-word forms; leave
    /// it off for other screens (result, race picker) that show full names, so an event like
    /// "Champions Meeting" can't collide with the "Champions C." trophy label.
    /// </summary>
    public RaceNameMatch? FindBest(string ocrLine, bool includeAbbreviations = true)
    {
        var line = Normalize(ocrLine);
        if (line.Length == 0)
        {
            return null;
        }

        RaceNameMatch? best = null;
        var bestPatternLength = 0;
        foreach (var (raceId, name, canonical, abbreviated) in _races)
        {
            // Canonical names may sit anywhere in a line (surrounded by grade chips, distances,
            // commentary). Abbreviations may not: the game builds them by truncating the tail of
            // the name, so the pattern always runs to the END of the label. Anchoring them there
            // is what stops one race's abbreviation from landing mid-way through another's label —
            // "Kyoto Ki(mpai)" sitting inside a smudged "Kyoto Kineri" and beating Kyoto Kinen's
            // own near-exact full name on distance alone.
            var patterns = canonical.Select(p => (Pattern: p, Anchored: false));
            if (includeAbbreviations)
            {
                patterns = patterns.Concat(abbreviated.Select(p => (Pattern: p, Anchored: true)));
            }

            foreach (var (pattern, anchored) in patterns)
            {
                var distance = anchored
                    ? SuffixEditDistance(pattern, line)
                    : SubstringEditDistance(pattern, line);
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
        => Align(pattern, text).Min();

    /// <summary>
    /// Like <see cref="SubstringEditDistance"/>, but the match must END where the text ends: text
    /// before the match is free, text after it is not. Trailing
    /// characters the pattern does not explain are paid for, which is what makes a truncated
    /// trophy label ("yomiurimilersc" ending in "milersc") match while a smudge that merely
    /// contains the pattern ("kyotokineri" containing "kyotoki") does not.
    /// </summary>
    internal static int SuffixEditDistance(string pattern, string text)
        => Align(pattern, text)[text.Length];

    /// <summary>
    /// Semi-global alignment DP. Returns the final row: entry j is the edit distance between
    /// <paramref name="pattern"/> and the best-matching substring of <paramref name="text"/> that
    /// ENDS at position j (skipping text before the match is free). Callers choose their anchoring
    /// by how they read the row — the minimum for a substring match, the last entry for a suffix.
    /// </summary>
    private static int[] Align(string pattern, string text)
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
        return previous;
    }
}
