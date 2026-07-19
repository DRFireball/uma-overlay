namespace UmaCompanion.App.Reading;

/// <summary>
/// Debounces trophy detections across capture frames. A trophy is only reported after it appears
/// in two consecutive trophy-room frames, which rejects the transients that single frames throw
/// off while the player swipes between pages — motion-blurred labels and the edge of the adjacent
/// page sliding through. Each id is reported at most once per session (until <see cref="Reset"/>).
/// </summary>
public sealed class ConsecutiveFrameConfirmer
{
    private readonly HashSet<int> _previousFrame = new();
    private readonly HashSet<int> _emitted = new();
    private bool _hasPreviousFrame;

    /// <summary>
    /// Feeds the ids matched on the current trophy-room frame; returns those newly confirmed
    /// (present this frame AND the immediately previous one, not previously emitted).
    /// </summary>
    public IReadOnlyList<int> Confirm(IReadOnlyCollection<int> currentFrameIds)
    {
        var confirmed = _hasPreviousFrame
            ? currentFrameIds.Where(id => _previousFrame.Contains(id) && _emitted.Add(id)).ToList()
            : new List<int>();

        _previousFrame.Clear();
        foreach (var id in currentFrameIds)
        {
            _previousFrame.Add(id);
        }
        _hasPreviousFrame = true;
        return confirmed;
    }

    /// <summary>
    /// Notes that the current frame is not the trophy room, so the confirmation streak breaks —
    /// a page must show two consecutive frames again after any other screen interrupts it.
    /// </summary>
    public void BreakStreak()
    {
        _previousFrame.Clear();
        _hasPreviousFrame = false;
    }

    /// <summary>Forgets everything, including what has already been emitted (win-tracker reset).</summary>
    public void Reset()
    {
        _previousFrame.Clear();
        _emitted.Clear();
        _hasPreviousFrame = false;
    }
}
