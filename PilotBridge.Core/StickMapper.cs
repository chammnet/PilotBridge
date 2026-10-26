namespace PilotBridge.Core;

internal static class StickMapper
{
    public static void Apply(StickSettings s, short rawX, short rawY, GamepadBuilder pad)
    {
        var x = Math.Clamp(rawX / PilotPro.StickFullScale, -1, 1);
        var y = Math.Clamp(rawY / PilotPro.StickFullScale, -1, 1);

        // Radial dead zone: shrink the vector's length, keep its direction, so
        // a diagonal doesn't snap to an axis the way per-axis dead zones do.
        var mag = Math.Sqrt(x * x + y * y);
        if (mag <= s.Deadzone || mag == 0)
            return;
        var scaled = Shape(Math.Min(1, (mag - s.Deadzone) / (1 - s.Deadzone)), s.Curve, s.AntiDeadzone);
        x = x / mag * scaled;
        y = y / mag * scaled;

        pad.Add(s.X.Axis, s.X.Invert ? -x : x);
        pad.Add(s.Y.Axis, s.Y.Invert ? -y : y);
    }

    /// <summary>0..1 in, 0..1 out: curve, then lift non-zero values past the anti-dead zone.</summary>
    public static double Shape(double v, double curve, double antiDeadzone)
    {
        if (v <= 0) return 0;
        v = Math.Pow(Math.Min(v, 1), curve);
        return antiDeadzone + (1 - antiDeadzone) * v;
    }
}
