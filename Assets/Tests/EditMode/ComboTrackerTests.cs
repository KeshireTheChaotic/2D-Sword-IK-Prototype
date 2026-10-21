using NUnit.Framework;

public sealed class ComboTrackerTests
{
    [Test]
    public void AlternatingCommittedMissesIncreaseCountWithinDeadline()
    {
        var combo = new ComboTracker();
        combo.BeginWindup(-1, 0d);
        combo.CommitStrike(); // A miss is still a committed strike.
        combo.RecoveryEnded(1d);
        combo.BeginWindup(1, 1.65d);
        combo.CommitStrike();
        Assert.That(combo.Count, Is.EqualTo(2));
    }

    [Test]
    public void SameSideOrLateStrikeRestartsAtOne()
    {
        var combo = new ComboTracker();
        combo.BeginWindup(-1, 0d);
        combo.CommitStrike();
        combo.RecoveryEnded(1d);
        combo.BeginWindup(-1, 1.1d);
        combo.CommitStrike();
        Assert.That(combo.Count, Is.EqualTo(1));
        combo.RecoveryEnded(2d);
        combo.BeginWindup(1, 2.650001d);
        combo.CommitStrike();
        Assert.That(combo.Count, Is.EqualTo(1));
    }

    [Test]
    public void CancelledWindupDoesNotIncrement()
    {
        var combo = new ComboTracker();
        combo.BeginWindup(-1, 0d);
        combo.CancelWindup();
        Assert.That(combo.Count, Is.Zero);
    }

    [Test]
    public void GuardTapDoesNotResetButSustainedGuardDoes()
    {
        var combo = new ComboTracker();
        combo.BeginWindup(-1, 0d);
        combo.CommitStrike();
        combo.AdvanceGuard(true, .379d);
        Assert.That(combo.Count, Is.EqualTo(1));
        combo.AdvanceGuard(true, .001d);
        Assert.That(combo.Count, Is.Zero);
    }
}
