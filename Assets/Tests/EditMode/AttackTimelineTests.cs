using NUnit.Framework;

public sealed class AttackTimelineTests
{
    [Test]
    public void HeldPressNeverCommitsWithoutRelease()
    {
        var timeline = new AttackTimeline();
        Assert.That(timeline.Press(), Is.True);
        timeline.Advance(10d);
        Assert.That(timeline.Phase, Is.EqualTo(AttackTimelinePhase.Windup));
        Assert.That(timeline.IsCommitted, Is.False);
    }

    [Test]
    public void EarlyReleaseWaitsForMinimumThenRunsBoundedPhases()
    {
        var timeline = new AttackTimeline(.12, .09, .45, .33);
        timeline.Press();
        timeline.Advance(.05);
        timeline.Release();
        timeline.Advance(.069);
        Assert.That(timeline.Phase, Is.EqualTo(AttackTimelinePhase.Windup));
        timeline.Advance(.001);
        Assert.That(timeline.Phase, Is.EqualTo(AttackTimelinePhase.ForwardSpring));
        timeline.Advance(.09);
        Assert.That(timeline.Phase, Is.EqualTo(AttackTimelinePhase.Swing));
        timeline.Advance(.45);
        Assert.That(timeline.Phase, Is.EqualTo(AttackTimelinePhase.Recovery));
        timeline.Advance(.33);
        Assert.That(timeline.Phase, Is.EqualTo(AttackTimelinePhase.Ready));
    }

    [Test]
    public void SpamCannotRestartCommittedAttack()
    {
        var timeline = new AttackTimeline();
        timeline.Press();
        timeline.Advance(.12);
        timeline.Release();
        Assert.That(timeline.Press(), Is.False);
        Assert.That(timeline.Phase, Is.EqualTo(AttackTimelinePhase.ForwardSpring));
    }
}
