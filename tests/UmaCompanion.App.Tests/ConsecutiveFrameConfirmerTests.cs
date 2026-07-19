using UmaCompanion.App.Reading;

namespace UmaCompanion.App.Tests;

public class ConsecutiveFrameConfirmerTests
{
    [Fact]
    public void FirstSightingIsNotConfirmed_SecondConsecutiveSightingIs()
    {
        var confirmer = new ConsecutiveFrameConfirmer();

        Assert.Empty(confirmer.Confirm(new[] { 1, 2, 3 }));           // first frame: nothing yet
        Assert.Equal(new[] { 1, 2, 3 }, confirmer.Confirm(new[] { 1, 2, 3 }).OrderBy(x => x));
    }

    [Fact]
    public void TransientSingleFrameId_IsNeverConfirmed()
    {
        var confirmer = new ConsecutiveFrameConfirmer();
        confirmer.Confirm(new[] { 1, 2 });

        // 9 flickers through one frame (a swipe artifact) then is gone.
        var withFlicker = confirmer.Confirm(new[] { 1, 2, 9 });
        var afterFlicker = confirmer.Confirm(new[] { 1, 2 });

        Assert.Contains(1, withFlicker);
        Assert.Contains(2, withFlicker);
        Assert.DoesNotContain(9, withFlicker);   // only seen once so far
        Assert.DoesNotContain(9, afterFlicker);  // and never again → never confirmed
    }

    [Fact]
    public void ConfirmedIdIsEmittedOnlyOnce_EvenIfItKeepsShowing()
    {
        var confirmer = new ConsecutiveFrameConfirmer();
        confirmer.Confirm(new[] { 5 });
        Assert.Equal(new[] { 5 }, confirmer.Confirm(new[] { 5 }));

        Assert.Empty(confirmer.Confirm(new[] { 5 })); // already emitted
        Assert.Empty(confirmer.Confirm(new[] { 5 }));
    }

    [Fact]
    public void BreakStreak_RequiresTwoFreshConsecutiveFramesAgain()
    {
        var confirmer = new ConsecutiveFrameConfirmer();
        confirmer.Confirm(new[] { 7 });   // first sighting
        confirmer.BreakStreak();          // a non-trophy screen interrupts

        Assert.Empty(confirmer.Confirm(new[] { 7 }));               // streak reset: first again
        Assert.Equal(new[] { 7 }, confirmer.Confirm(new[] { 7 }));  // now confirmed
    }

    [Fact]
    public void Reset_ForgetsEmittedIds_SoTheyCanSyncAgain()
    {
        var confirmer = new ConsecutiveFrameConfirmer();
        confirmer.Confirm(new[] { 4 });
        confirmer.Confirm(new[] { 4 }); // emitted

        confirmer.Reset();

        Assert.Empty(confirmer.Confirm(new[] { 4 }));              // first sighting post-reset
        Assert.Equal(new[] { 4 }, confirmer.Confirm(new[] { 4 })); // confirmed again
    }
}
