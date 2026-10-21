using System;

/// <summary>Pure perfect-parry timing and eligibility, including placement rearm.</summary>
public sealed class ParryResolver
{
    public double RearmSeconds { get; }
    public double LastEligiblePlacementTime { get; private set; } = double.NegativeInfinity;
    public double NextPlacementTime { get; private set; } = double.NegativeInfinity;

    public ParryResolver(double rearmSeconds = .42)
    {
        RearmSeconds = Math.Max(0d, rearmSeconds);
    }

    public static double WindowSeconds(int comboCount)
    {
        return Math.Min(.280d, .120d + .035d * (Math.Max(1, comboCount) - 1));
    }

    public bool CompleteGuardPlacement(double now)
    {
        if (now < NextPlacementTime) return false;
        LastEligiblePlacementTime = now;
        NextPlacementTime = now + RearmSeconds;
        return true;
    }

    public bool IsPerfectParry(bool incomingCommitted, bool bladesIntersect,
        bool elevationsOverlap, bool defenderEligible, int attackerComboCount, double now)
    {
        if (!incomingCommitted || !bladesIntersect || !elevationsOverlap || !defenderEligible)
            return false;
        if (double.IsNegativeInfinity(LastEligiblePlacementTime)) return false;
        double age = now - LastEligiblePlacementTime;
        if (age < 0d || age > WindowSeconds(attackerComboCount)) return false;
        LastEligiblePlacementTime = double.NegativeInfinity;
        NextPlacementTime = Math.Max(NextPlacementTime, now + RearmSeconds);
        return true;
    }

    public void Reset()
    {
        LastEligiblePlacementTime = double.NegativeInfinity;
        NextPlacementTime = double.NegativeInfinity;
    }
}
