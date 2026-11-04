namespace Tappy.Graphics;

/// <summary>
/// Allocation-free easing helpers used by every animation in the app.
/// </summary>
public static class Ease
{
    public static float Clamp01(float t) => t < 0f ? 0f : (t > 1f ? 1f : t);

    public static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    public static float OutCubic(float t)
    {
        t = Clamp01(t);
        float u = 1f - t;
        return 1f - (u * u * u);
    }

    public static float InOutSine(float t)
    {
        t = Clamp01(t);
        return -(MathF.Cos(MathF.PI * t) - 1f) * 0.5f;
    }

    public static float OutBack(float t, float overshoot = 1.70158f)
    {
        t = Clamp01(t);
        float c3 = overshoot + 1f;
        float u = t - 1f;
        return 1f + (c3 * u * u * u) + (overshoot * u * u);
    }

    /// <summary>
    /// Frame-rate independent exponential smoothing toward a target value.
    /// </summary>
    public static float Damp(float current, float target, float rate, float dt)
        => Lerp(current, target, 1f - MathF.Exp(-rate * dt));
}

