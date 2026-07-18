namespace UmaCompanion.Core.Reading;

/// <summary>A known race matched against an OCR'd line, with the edit distance that matched it.</summary>
public sealed record RaceNameMatch(int RaceId, string Name, int Distance);

/// <summary>
/// Matches noisy OCR text against the known race names from master.mdb. OCR output routinely
/// mangles a character or two and surrounds the name with other screen text, so matching is
/// fuzzy (bounded edit distance) and substring-based (the name may appear anywhere in the line).
/// </summary>
public sealed class RaceNameMatcher
{
    private readonly List<(int RaceId, string Name, string Normalized)> _races;

    public RaceNameMatcher(IEnumerable<(int RaceId, string Name)> races)
    {
        _races = races
            .GroupBy(r => r.RaceId)
            .Select(g => g.First())
            .Select(r => (r.RaceId, r.Name, Normalize(r.Name)))
            .Where(r => r.Item3.Length > 0)
            .ToList();
    }

    /// <summary>
    /// The best race whose name approximately appears in <paramref name="ocrLine"/>, or null when
    /// nothing matches within tolerance. Tolerance scales with name length (roughly one error per
    /// eight characters), so short names must match exactly and long names absorb OCR noise.
    /// </summary>
    public RaceNameMatch? FindBest(string ocrLine)
    {
        var line = Normalize(ocrLine);
        if (line.Length == 0)
        {
            return null;
        }

        RaceNameMatch? best = null;
        foreach (var (raceId, name, normalized) in _races)
        {
            var maxDistance = normalized.Length / 8;
            var distance = SubstringEditDistance(normalized, line);
            if (distance > maxDistance)
            {
                continue;
            }
            // Prefer the closest match; on equal distance the longer (more specific) name wins —
            // "Tokyo Yushun (Japanese Derby)" must beat any shorter name embedded in the same line.
            if (best is null
                || distance < best.Distance
                || (distance == best.Distance && name.Length > best.Name.Length))
            {
                best = new RaceNameMatch(raceId, name, distance);
            }
        }
        return best;
    }

    /// <summary>Lowercases and strips everything but letters and digits, collapsing OCR spacing noise.</summary>
    internal static string Normalize(string text)
        => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

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
