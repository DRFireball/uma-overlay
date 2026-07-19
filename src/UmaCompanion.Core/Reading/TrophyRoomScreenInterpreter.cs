namespace UmaCompanion.Core.Reading;

/// <summary>
/// Reads the game's Trophy Room pages: every race name visible on a trophy-room screen is a race
/// the player has won, so browsing the room becomes a bulk sync of past wins. Interpretation is
/// gated on the screen actually BEING the trophy room — other screens full of race names (the
/// race picker, the career calendar) list races the player has NOT won, and must never match.
/// </summary>
public sealed class TrophyRoomScreenInterpreter
{
    // Screen-title markers that identify the trophy room, normalized. One OCR error tolerated.
    private static readonly string[] Markers = { "trophyroom", "trophies" };

    private readonly RaceNameMatcher _matcher;

    public TrophyRoomScreenInterpreter(RaceNameMatcher matcher)
    {
        _matcher = matcher;
    }

    /// <summary>Whether these OCR lines are a trophy-room page.</summary>
    public bool IsTrophyRoomScreen(IReadOnlyList<string> ocrLines)
        => ocrLines.Any(line =>
        {
            var normalized = RaceNameMatcher.Normalize(line);
            return normalized.Length > 0 && Markers.Any(marker =>
                RaceNameMatcher.SubstringEditDistance(marker, normalized) <= 1);
        });

    /// <summary>
    /// The distinct race ids whose trophies are visible on this page, or empty when the lines are
    /// not a trophy-room screen. Adjacent lines are also matched joined, so a long race name that
    /// OCR splits across two lines is still found.
    /// </summary>
    public IReadOnlyList<int> WonRaceIds(IReadOnlyList<string> ocrLines)
    {
        if (!IsTrophyRoomScreen(ocrLines))
        {
            return Array.Empty<int>();
        }

        var candidates = ocrLines.Concat(
            ocrLines.Zip(ocrLines.Skip(1), (a, b) => a + " " + b));

        return candidates
            .Select(line => _matcher.FindBest(line))
            .Where(match => match is not null)
            .Select(match => match!.RaceId)
            .Distinct()
            .ToList();
    }
}
