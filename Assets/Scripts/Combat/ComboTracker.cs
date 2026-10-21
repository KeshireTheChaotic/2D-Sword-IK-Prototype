using System;

/// <summary>Tracks committed alternating strikes without Unity time or input.</summary>
public sealed class ComboTracker
{
    public int Count { get; private set; }
    public int LastCommittedStartSide { get; private set; }
    public double ContinuationDeadline { get; private set; } = double.NegativeInfinity;
    public double ContinuationSeconds { get; }
    public double GuardResetSeconds { get; }

    private int pendingSide;
    private bool pendingContinuation;
    private double sustainedGuardSeconds;

    public ComboTracker(double continuationSeconds = .65, double guardResetSeconds = .38)
    {
        ContinuationSeconds = Math.Max(0d, continuationSeconds);
        GuardResetSeconds = Math.Max(0d, guardResetSeconds);
    }

    public void BeginWindup(int startSide, double now)
    {
        pendingSide = NormalizeSide(startSide);
        pendingContinuation = Count > 0 && now <= ContinuationDeadline &&
            LastCommittedStartSide != 0 && pendingSide != LastCommittedStartSide;
    }

    public void CommitStrike()
    {
        if (pendingSide == 0) throw new InvalidOperationException("BeginWindup must precede commitment.");
        Count = pendingContinuation ? Count + 1 : 1;
        LastCommittedStartSide = pendingSide;
        ContinuationDeadline = double.NegativeInfinity;
        pendingSide = 0;
        pendingContinuation = false;
        sustainedGuardSeconds = 0d;
    }

    public void CancelWindup()
    {
        pendingSide = 0;
        pendingContinuation = false;
    }

    public void RecoveryEnded(double now)
    {
        if (Count > 0) ContinuationDeadline = now + ContinuationSeconds;
    }

    public void AdvanceGuard(bool guardReady, double deltaSeconds)
    {
        if (!guardReady)
        {
            sustainedGuardSeconds = 0d;
            return;
        }
        sustainedGuardSeconds += Math.Max(0d, deltaSeconds);
        if (Count > 0 && sustainedGuardSeconds >= GuardResetSeconds) Reset();
    }

    public void Expire(double now)
    {
        if (Count > 0 && now > ContinuationDeadline) Reset();
    }

    public void Reset()
    {
        Count = 0;
        LastCommittedStartSide = 0;
        ContinuationDeadline = double.NegativeInfinity;
        pendingSide = 0;
        pendingContinuation = false;
        sustainedGuardSeconds = 0d;
    }

    private static int NormalizeSide(int side)
    {
        if (side == 0) throw new ArgumentOutOfRangeException(nameof(side), "Strike side cannot be zero.");
        return side < 0 ? -1 : 1;
    }
}
