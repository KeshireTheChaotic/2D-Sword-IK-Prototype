using System;

public enum AttackTimelinePhase { Ready, Windup, ForwardSpring, Swing, Recovery }

/// <summary>Bounded attack phase machine driven only by caller-provided deltas.</summary>
public sealed class AttackTimeline
{
    public AttackTimelinePhase Phase { get; private set; } = AttackTimelinePhase.Ready;
    public double PhaseSeconds { get; private set; }
    public bool IsCommitted => Phase == AttackTimelinePhase.ForwardSpring ||
        Phase == AttackTimelinePhase.Swing || Phase == AttackTimelinePhase.Recovery;

    public double MinimumWindupSeconds { get; }
    public double SpringSeconds { get; }
    public double SwingSeconds { get; }
    public double RecoverySeconds { get; }
    private bool releaseQueued;

    public AttackTimeline(double minimumWindupSeconds = .12, double springSeconds = .09,
        double swingSeconds = .45, double recoverySeconds = .33)
    {
        MinimumWindupSeconds = Math.Max(0d, minimumWindupSeconds);
        SpringSeconds = Math.Max(0d, springSeconds);
        SwingSeconds = Math.Max(0d, swingSeconds);
        RecoverySeconds = Math.Max(0d, recoverySeconds);
    }

    public bool Press()
    {
        if (Phase != AttackTimelinePhase.Ready) return false;
        Enter(AttackTimelinePhase.Windup);
        return true;
    }

    public void Release()
    {
        if (Phase != AttackTimelinePhase.Windup) return;
        if (PhaseSeconds >= MinimumWindupSeconds) Enter(AttackTimelinePhase.ForwardSpring);
        else releaseQueued = true;
    }

    public void Advance(double deltaSeconds)
    {
        double remaining = Math.Max(0d, deltaSeconds);
        while (remaining > 0d)
        {
            double duration = DurationForCurrentPhase();
            if (double.IsPositiveInfinity(duration))
            {
                PhaseSeconds += remaining;
                if (Phase == AttackTimelinePhase.Windup && releaseQueued &&
                    PhaseSeconds >= MinimumWindupSeconds)
                    Enter(AttackTimelinePhase.ForwardSpring);
                return;
            }
            double toBoundary = Math.Max(0d, duration - PhaseSeconds);
            if (remaining + 1e-6 >= toBoundary)
            {
                remaining = Math.Max(0d, remaining - toBoundary);
                PhaseSeconds = duration;
                Enter(NextPhase());
                continue;
            }
            double step = Math.Min(remaining, toBoundary);
            PhaseSeconds += step;
            remaining -= step;
            return;
        }
    }

    public void Interrupt() => Enter(AttackTimelinePhase.Ready);

    private double DurationForCurrentPhase()
    {
        switch (Phase)
        {
            case AttackTimelinePhase.Windup when releaseQueued: return MinimumWindupSeconds;
            case AttackTimelinePhase.ForwardSpring: return SpringSeconds;
            case AttackTimelinePhase.Swing: return SwingSeconds;
            case AttackTimelinePhase.Recovery: return RecoverySeconds;
            default: return double.PositiveInfinity;
        }
    }

    private AttackTimelinePhase NextPhase()
    {
        switch (Phase)
        {
            case AttackTimelinePhase.Windup: return AttackTimelinePhase.ForwardSpring;
            case AttackTimelinePhase.ForwardSpring: return AttackTimelinePhase.Swing;
            case AttackTimelinePhase.Swing: return AttackTimelinePhase.Recovery;
            case AttackTimelinePhase.Recovery: return AttackTimelinePhase.Ready;
            default: return Phase;
        }
    }

    private void Enter(AttackTimelinePhase phase)
    {
        Phase = phase;
        PhaseSeconds = 0d;
        if (phase != AttackTimelinePhase.Windup) releaseQueued = false;
    }
}
