namespace UmaCompanion.Core.Reading;

/// <summary>
/// Reads the game's Trophy Room pages. Every trophy slot — owned or not — shows its race name,
/// so text alone cannot tell a win from a gap: owned trophies are bright art, unowned ones a
/// near-black silhouette (measured on real captures: owned ≥ 104 mean luminance, silhouettes
/// ≤ 53, bare shelf ≈ 71). Each matched label is therefore confirmed by probing the brightness
/// of the art region above it. Interpretation is also gated on the screen actually BEING the
/// trophy room — race pickers and calendars list races the player has NOT won, and must never
/// match.
/// </summary>
public sealed class TrophyRoomScreenInterpreter
{
    /// <summary>Mean luminance above which the art region counts as an owned trophy.</summary>
    public const double OwnedBrightnessThreshold = 80;

    // Screen-title markers that identify the trophy room, normalized. One OCR error tolerated.
    private static readonly string[] Markers = { "trophyroom", "trophies" };

    private readonly RaceNameMatcher _matcher;

    public TrophyRoomScreenInterpreter(RaceNameMatcher matcher)
    {
        _matcher = matcher;
    }

    /// <summary>Whether these OCR lines are a trophy-room page.</summary>
    public bool IsTrophyRoomScreen(IReadOnlyList<OcrLine> ocrLines)
        => ocrLines.Any(line =>
        {
            var normalized = RaceNameMatcher.Normalize(line.Text);
            return normalized.Length > 0 && Markers.Any(marker =>
                RaceNameMatcher.SubstringEditDistance(marker, normalized) <= 1);
        });

    /// <summary>
    /// The distinct race ids whose trophies are visibly OWNED on this page (label matched and
    /// the art above it is bright), or empty when the lines are not a trophy-room screen.
    /// <paramref name="meanBrightness"/> samples the captured frame (0 = black … 255 = white).
    /// </summary>
    public IReadOnlyList<int> OwnedRaceIds(
        IReadOnlyList<OcrLine> ocrLines, Func<OcrRect, double> meanBrightness)
    {
        if (!IsTrophyRoomScreen(ocrLines))
        {
            return Array.Empty<int>();
        }

        // Best label per race across all candidates (a wrapped name matches via the joined pair).
        var byRace = new Dictionary<int, (int Distance, OcrRect Probe)>();
        foreach (var label in LabelCandidates(ocrLines))
        {
            var match = _matcher.FindBest(label.Text);
            if (match is null)
            {
                continue;
            }
            if (!byRace.TryGetValue(match.RaceId, out var current) || match.Distance < current.Distance)
            {
                byRace[match.RaceId] = (match.Distance, ProbeRegionAbove(label.Bounds, label.UnitHeight));
            }
        }

        return byRace
            .Where(kv => meanBrightness(kv.Value.Probe) >= OwnedBrightnessThreshold)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>
    /// Each line alone, plus each adjacent pair joined — a long race name that OCR splits across
    /// two stacked lines is matched via the join. UnitHeight stays a single line's height so the
    /// probe region above a joined label is not inflated.
    /// </summary>
    private static IEnumerable<(string Text, OcrRect Bounds, double UnitHeight)> LabelCandidates(
        IReadOnlyList<OcrLine> lines)
    {
        foreach (var line in lines)
        {
            yield return (line.Text, line.Bounds, line.Bounds.Height);
        }
        for (var i = 0; i + 1 < lines.Count; i++)
        {
            var (a, b) = (lines[i].Bounds, lines[i + 1].Bounds);
            yield return (
                lines[i].Text + " " + lines[i + 1].Text,
                Union(a, b),
                Math.Min(a.Height, b.Height));
        }
    }

    private static OcrRect Union(OcrRect a, OcrRect b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new OcrRect(
            x, y,
            Math.Max(a.X + a.Width, b.X + b.Width) - x,
            Math.Max(a.Y + a.Height, b.Y + b.Height) - y);
    }

    /// <summary>
    /// The trophy-art region above a label: a square of ~3 text-heights, centered, ending one
    /// text-height above the label — the middle of the trophy (or silhouette) art.
    /// </summary>
    private static OcrRect ProbeRegionAbove(OcrRect label, double unitHeight)
    {
        var side = 3 * unitHeight;
        return new OcrRect(
            label.X + label.Width / 2 - side / 2,
            label.Y - unitHeight - side,
            side,
            side);
    }
}
