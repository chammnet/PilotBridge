namespace PilotBridge.Core;

/// <summary>
/// Replays recorded knob ticks through Rate-mode settings and scores the
/// output, so settings can be compared on real turning without the device.
/// </summary>
public static class KnobReplay
{
    public sealed record Score(
        int Ticks,
        double TicksPerSecond,
        double MeanOutput,
        /// <summary>Standard deviation of the output while turning, as a fraction of the mean.</summary>
        double Bounce,
        /// <summary>Seconds from the first tick until the output reaches 90% of its mean.</summary>
        double SecondsToSpeed,
        /// <summary>Seconds from the last tick until the output is below 5%.</summary>
        double SecondsToStop);

    private const double Step = 0.004;          // like a 250 Hz update loop
    private const double SettleSeconds = 1.5;   // ignore start-up when measuring bounce

    /// <param name="ticks">Tick times in seconds, all one direction, ascending.</param>
    public static Score Run(IReadOnlyList<(double T, int Direction)> ticks, KnobSettings settings)
    {
        if (ticks.Count < 3)
            return new Score(ticks.Count, 0, 0, 0, 0, 0);

        var axis = new KnobAxis(settings);
        var start = ticks[0].T;
        var end = ticks[^1].T;
        var next = 0;
        var trace = new List<(double T, double Out)>();
        for (var t = start; t < end + 5; t += Step)
        {
            while (next < ticks.Count && ticks[next].T <= t)
            {
                axis.Tick(ticks[next].Direction, ticks[next].T);
                next++;
            }
            trace.Add((t, Math.Abs(axis.Update(t))));
        }

        var turning = trace.Where(p => p.T > start + SettleSeconds && p.T < end).Select(p => p.Out).ToList();
        if (turning.Count == 0)
            turning = trace.Where(p => p.T < end).Select(p => p.Out).ToList();
        var mean = turning.Average();
        var sd = Math.Sqrt(turning.Sum(v => (v - mean) * (v - mean)) / turning.Count);

        var toSpeed = trace.FirstOrDefault(p => p.Out >= 0.9 * mean).T - start;
        var stopAt = trace.FirstOrDefault(p => p.T > end && p.Out < 0.05);
        var toStop = stopAt == default ? double.NaN : stopAt.T - end;

        return new Score(
            ticks.Count,
            (ticks.Count - 1) / (end - start),
            mean,
            mean > 0 ? sd / mean : 0,
            toSpeed,
            toStop);
    }
}
