using System;

/// <summary>Pure angle-dependent turning rules. All inputs are explicit.</summary>
public static class AngleTurnResponse
{
    private static readonly float[] Angles = { 0f, 30f, 60f, 90f, 135f, 180f };
    private static readonly float[] Multipliers = { .08f, .20f, .70f, 1f, 1.5f, 2f };

    public static float EvaluateMultiplier(float absoluteAngle)
    {
        float angle = Clamp(Math.Abs(absoluteAngle), 0f, 180f);
        for (int i = 1; i < Angles.Length; i++)
        {
            if (angle > Angles[i]) continue;
            float t = (angle - Angles[i - 1]) / (Angles[i] - Angles[i - 1]);
            t = t * t * (3f - 2f * t);
            return Lerp(Multipliers[i - 1], Multipliers[i], t);
        }
        return Multipliers[Multipliers.Length - 1];
    }

    public static float ResolveTurnSign(float signedAngle, float lastNonAmbiguousSign,
        float rearAmbiguityDegrees = 2f)
    {
        float fallback = lastNonAmbiguousSign < 0f ? -1f : 1f;
        if (Math.Abs(signedAngle) >= 180f - Math.Max(0f, rearAmbiguityDegrees)) return fallback;
        if (signedAngle > 0f) return 1f;
        if (signedAngle < 0f) return -1f;
        return fallback;
    }

    public static float StepAngularVelocity(float currentVelocity, float signedAngle,
        float baseSpeed, float stateMultiplier, float acceleration, float deltaTime,
        float lastNonAmbiguousSign)
    {
        if (deltaTime <= 0f) return currentVelocity;
        float absoluteAngle = Math.Abs(signedAngle);
        float speedLimit = Math.Max(0f, baseSpeed) * EvaluateMultiplier(absoluteAngle) *
            Math.Max(0f, stateMultiplier);
        float desired = absoluteAngle < .1f ? 0f :
            ResolveTurnSign(signedAngle, lastNonAmbiguousSign) *
            Math.Min(speedLimit, absoluteAngle / deltaTime);
        return MoveTowards(currentVelocity, desired, Math.Max(0f, acceleration) * deltaTime);
    }

    private static float Clamp(float value, float min, float max) =>
        Math.Max(min, Math.Min(max, value));
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float MoveTowards(float current, float target, float maximumDelta)
    {
        if (Math.Abs(target - current) <= maximumDelta) return target;
        return current + Math.Sign(target - current) * maximumDelta;
    }
}
