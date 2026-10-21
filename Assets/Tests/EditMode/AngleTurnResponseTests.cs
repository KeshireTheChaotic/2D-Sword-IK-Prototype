using NUnit.Framework;

public sealed class AngleTurnResponseTests
{
    [TestCase(0f, .08f)]
    [TestCase(30f, .20f)]
    [TestCase(60f, .70f)]
    [TestCase(90f, 1f)]
    [TestCase(135f, 1.5f)]
    [TestCase(180f, 2f)]
    public void CalibrationPointsAreExact(float angle, float expected)
    {
        Assert.That(AngleTurnResponse.EvaluateMultiplier(angle), Is.EqualTo(expected).Within(1e-6f));
    }

    [Test]
    public void CurveIsMonotonicAndSymmetric()
    {
        float previous = 0f;
        for (int angle = 0; angle <= 180; angle++)
        {
            float current = AngleTurnResponse.EvaluateMultiplier(angle);
            Assert.That(current, Is.GreaterThanOrEqualTo(previous));
            Assert.That(AngleTurnResponse.EvaluateMultiplier(-angle), Is.EqualTo(current));
            previous = current;
        }
    }

    [Test]
    public void RearAmbiguityKeepsLastTurnSign()
    {
        Assert.That(AngleTurnResponse.ResolveTurnSign(180f, -1f), Is.EqualTo(-1f));
        Assert.That(AngleTurnResponse.ResolveTurnSign(-179f, 1f), Is.EqualTo(1f));
    }

    [Test]
    public void VelocityStepHonorsAccelerationLimit()
    {
        float result = AngleTurnResponse.StepAngularVelocity(0f, 90f, 100f, 1f, 20f, .02f, 1f);
        Assert.That(result, Is.EqualTo(.4f).Within(1e-6f));
    }
}
