namespace UmaCompanion.Core.Reading;

/// <summary>
/// Reads the game's Trophy Room pages. Every trophy slot — owned or not — shows its race name,
/// so text alone cannot tell a win from a gap: owned trophies are bright, spot-lit art, unowned
/// ones a near-black unlit silhouette. Each matched label is confirmed by probing the brightness
/// of the art region above it; the region reaches high enough to include the spotlight glow, so
/// even dark trophy art (the Tenno Sho (Autumn) plaque) reads bright. Measured on real captures:
/// silhouettes ≤ 48, darkest owned art 84, typical owned ≥ 90 — threshold 70. Interpretation is
/// also gated on the screen actually BEING the trophy room — race pickers and calendars list
/// races the player has NOT won, and must never match.
/// </summary>
public sealed class TrophyRoomScreenInterpreter
{
    /// <summary>Mean luminance above which the art region counts as an owned trophy.</summary>
    public const double OwnedBrightnessThreshold = 70;

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
    /// The trophy-art region above a label: 3 text-heights wide and 5 tall, centered, ending one
    /// text-height above the label. Tall enough to span the art AND its spotlight glow, so dark
    /// trophy art still probes bright, while silhouette slots (never lit) stay dark. Stays well
    /// short of the shelf above (~10 text-heights up).
    /// </summary>
    private static OcrRect ProbeRegionAbove(OcrRect label, double unitHeight)
    {
        var width = 3 * unitHeight;
        var height = 5 * unitHeight;
        return new OcrRect(
            label.X + label.Width / 2 - width / 2,
            label.Y - unitHeight - height,
            width,
            height);
    }
}
