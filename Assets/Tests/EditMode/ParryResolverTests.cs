using NUnit.Framework;

public sealed class ParryResolverTests
{
    [Test]
    public void WindowsMatchPlanAndCap()
    {
        double[] expected = { .120, .155, .190, .225, .260, .280, .280 };
        for (int count = 1; count <= expected.Length; count++)
            Assert.That(ParryResolver.WindowSeconds(count), Is.EqualTo(expected[count - 1]).Within(1e-9));
    }

    [Test]
    public void PerfectParryRequiresEveryPhysicalAndStateGate()
    {
        AssertGateFailure(false, true, true, true);
        AssertGateFailure(true, false, true, true);
        AssertGateFailure(true, true, false, true);
        AssertGateFailure(true, true, true, false);
    }

    [Test]
    public void StationaryGuardAndExpiredPlacementCannotParry()
    {
        var resolver = new ParryResolver();
        Assert.That(ResolveValid(resolver, 1, 10d), Is.False);
        resolver.CompleteGuardPlacement(10d);
        Assert.That(ResolveValid(resolver, 1, 10.121d), Is.False);
    }

    [Test]
    public void PlacementAndSuccessfulParryRespectRearm()
    {
        var resolver = new ParryResolver(.42);
        Assert.That(resolver.CompleteGuardPlacement(1d), Is.True);
        Assert.That(ResolveValid(resolver, 6, 1.20d), Is.True);
        Assert.That(resolver.CompleteGuardPlacement(1.619d), Is.False);
        Assert.That(resolver.CompleteGuardPlacement(1.620d), Is.True);
    }

    private static void AssertGateFailure(bool committed, bool intersecting,
        bool elevationsOverlap, bool defenderEligible)
    {
        var resolver = new ParryResolver();
        resolver.CompleteGuardPlacement(2d);
        Assert.That(resolver.IsPerfectParry(committed, intersecting, elevationsOverlap,
            defenderEligible, 1, 2.05d), Is.False);
    }

    private static bool ResolveValid(ParryResolver resolver, int combo, double now) =>
        resolver.IsPerfectParry(true, true, true, true, combo, now);
}
