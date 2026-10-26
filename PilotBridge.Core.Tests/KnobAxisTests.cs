using PilotBridge.Core;

namespace PilotBridge.Core.Tests;

public class KnobAxisTests
{
    private const double Dt = 0.004;   // a 250 Hz update loop

    /// <summary>
    /// Round numbers so expected values are easy to check by hand. Smoothing
    /// is off here so each test sees the estimator and ramps directly; the
    /// smoothing tests turn it on.
    /// </summary>
    private static KnobSettings Rate(Action<KnobSettings>? tweak = null)
    {
        var s = new KnobSettings
        {
            Mode = KnobMode.Rate,
            Axis = GamepadAxis.RightX,
            FullScaleTicksPerSecond = 10,
            MinTicksPerSecond = 0.75,
            AverageIntervals = 4,
            GraceFactor = 2.5,
            SmoothingSeconds = 0,
            RampUpPerSecond = 4,
            RampDownPerSecond = 2,
        };
        tweak?.Invoke(s);
        return s;
    }

    /// <summary>Runs the update loop from 'from' to 'to', ticking at the given times.</summary>
    private static List<(double T, double Out)> Run(KnobAxis axis, double from, double to, params (double T, int Dir)[] ticks)
    {
        var trace = new List<(double, double)>();
        var pending = new Queue<(double T, int Dir)>(ticks.OrderBy(k => k.T));
        for (var t = from; t <= to + 1e-9; t += Dt)
        {
            while (pending.Count > 0 && pending.Peek().T <= t)
                axis.Tick(pending.Peek().Dir, pending.Dequeue().T);
            trace.Add((t, axis.Update(t)));
        }
        return trace;
    }

    private static (double, int)[] Steady(double start, double perSecond, int count, int dir = +1) =>
        Enumerable.Range(0, count).Select(i => (start + i / perSecond, dir)).ToArray();

    [Fact]
    public void SteadyTurning_SettlesAtSpeedOverFullScale()
    {
        var axis = new KnobAxis(Rate());
        var trace = Run(axis, 0, 3, Steady(0, 4, 13));   // 4 ticks/s for 3 s
        Assert.Equal(0.4, trace.Last(p => p.T < 2.9).Out, 3);
    }

    [Fact]
    public void SlowSteadyTurning_HoldsPerfectlyStillBetweenTicks()
    {
        // The point of the interval-based estimate: no sawtooth at slow speeds.
        var axis = new KnobAxis(Rate());
        var trace = Run(axis, 0, 6, Steady(0, 1, 7));   // 1 tick/s
        var settled = trace.Where(p => p.T is > 3 and < 5.9).Select(p => p.Out).ToList();
        Assert.All(settled, v => Assert.Equal(0.1, v, 6));
    }

    [Fact]
    public void Starting_IsLimitedByRampUp()
    {
        var axis = new KnobAxis(Rate());
        var trace = Run(axis, 0, 1, Steady(0, 20, 21));   // fast: target is full scale
        for (var i = 1; i < trace.Count; i++)
            Assert.True(trace[i].Out - trace[i - 1].Out <= 4 * Dt + 1e-9);
        Assert.Equal(1.0, trace.Last().Out, 6);   // gets there in 0.25 s
    }

    [Fact]
    public void Stopping_GlidesDownAndNeverJumpsUp()
    {
        var axis = new KnobAxis(Rate());
        var trace = Run(axis, 0, 6, Steady(0, 4, 9));   // last tick at t = 2
        var after = trace.Where(p => p.T > 2).ToList();
        for (var i = 1; i < after.Count; i++)
        {
            Assert.True(after[i].Out <= after[i - 1].Out + 1e-12);
            Assert.True(after[i - 1].Out - after[i].Out <= 2 * Dt + 1e-9);   // RampDown
        }
        // Holds speed through the grace period (2.5 gaps = 0.625 s), then falls.
        Assert.Equal(0.4, after.First(p => p.T > 2.6).Out, 6);
        Assert.True(after.First(p => p.T > 2.75).Out < 0.4);
        Assert.Equal(0.0, after.Last().Out);
    }

    [Fact]
    public void UnevenTurning_QuickSlowRhythm_HoldsSteady()
    {
        // Craig's recorded "medium": pairs of ticks ~100 ms apart, then ~350 ms
        // gaps. Averaging 4 gaps spans two whole pairs, and the grace period
        // keeps the long gaps from counting as slowing down.
        var ticks = new List<(double, int)>();
        for (var t = 0.0; t < 6; t += 0.45) { ticks.Add((t, +1)); ticks.Add((t + 0.1, +1)); }
        var trace = Run(new KnobAxis(Rate()), 0, 6, ticks.ToArray());

        var steady = trace.Where(p => p.T is > 2 and < 5.9).Select(p => p.Out).ToList();
        var expected = (1 / 0.225) / 10;   // 4.44 ticks/s of 10 full scale
        Assert.All(steady, v => Assert.Equal(expected, v, 3));
    }

    [Fact]
    public void UnevenTurning_WithoutGrace_WouldSag()
    {
        // The same rhythm with no grace shows what the grace period is for.
        var ticks = new List<(double, int)>();
        for (var t = 0.0; t < 6; t += 0.45) { ticks.Add((t, +1)); ticks.Add((t + 0.1, +1)); }
        var trace = Run(new KnobAxis(Rate(s => s.GraceFactor = 1.0)), 0, 6, ticks.ToArray());
        Assert.Contains(trace, p => p.T is > 2 and < 5.9 && p.Out < 0.3);
    }

    [Fact]
    public void Smoothing_SoftensSteps_ButArrivesAtTheSameValue()
    {
        var raw = Run(new KnobAxis(Rate()), 0, 3, Steady(0, 4, 13));
        var smooth = Run(new KnobAxis(Rate(s => s.SmoothingSeconds = 0.25)), 0, 3, Steady(0, 4, 13));

        // Lags behind while starting...
        var early = smooth.First(p => p.T > 0.9).Out;
        Assert.True(early < raw.First(p => p.T > 0.9).Out);
        // ...but settles on the same value.
        Assert.Equal(0.4, smooth.Last(p => p.T < 2.9).Out, 3);
    }

    [Fact]
    public void Smoothing_StillComesToExactlyZero()
    {
        var axis = new KnobAxis(Rate(s => s.SmoothingSeconds = 0.25));
        var trace = Run(axis, 0, 6, Steady(0, 4, 9));
        Assert.Equal(0.0, trace.Last().Out);
    }

    [Fact]
    public void Reversal_PassesThroughZeroAtRampDown_ThenStartsSlow()
    {
        var axis = new KnobAxis(Rate());
        var ticks = Steady(0, 8, 9).Concat(Steady(1.05, 8, 9, -1)).ToArray();
        var trace = Run(axis, 0, 3, ticks);
        var reversing = trace.Where(p => p.T is > 1.05 and < 1.3).ToList();
        for (var i = 1; i < reversing.Count; i++)
            Assert.True(Math.Abs(reversing[i].Out - reversing[i - 1].Out) <= 2 * Dt + 1e-9);
        Assert.Contains(trace, p => p.T > 1.05 && p.Out < -0.5);   // then it does go the other way
    }

    [Fact]
    public void SingleTick_GivesBriefSlowestMovement()
    {
        var axis = new KnobAxis(Rate(s => s.RampUpPerSecond = 1000));
        var trace = Run(axis, 0, 3, (0.5, +1));
        Assert.Equal(0.075, trace.First(p => p.T > 0.6).Out, 6);   // 0.75 / 10
        Assert.Equal(0.0, trace.Last().Out);
    }

    [Fact]
    public void IgnoreSingleTicks_StrayTickDoesNothing_ButTurningStillWorks()
    {
        var axis = new KnobAxis(Rate(s => s.IgnoreSingleTicks = true));
        Assert.All(Run(axis, 0, 3, (0.5, +1)), p => Assert.Equal(0.0, p.Out));

        var turning = Run(new KnobAxis(Rate(s => s.IgnoreSingleTicks = true)), 0, 3, Steady(0, 4, 13));
        Assert.Equal(0.4, turning.Last(p => p.T < 2.9).Out, 3);
    }

    [Fact]
    public void AntiDeadzone_LiftsTheSlowestSpeed()
    {
        var axis = new KnobAxis(Rate(s => { s.AntiDeadzone = 0.2; s.RampUpPerSecond = 1000; }));
        var trace = Run(axis, 0, 1, (0.1, +1));
        Assert.Equal(0.2 + 0.8 * 0.075, trace.First(p => p.T > 0.2).Out, 6);
    }

    [Fact]
    public void Invert_FlipsTheOutput()
    {
        var axis = new KnobAxis(Rate(s => s.Invert = true));
        var trace = Run(axis, 0, 3, Steady(0, 4, 13));
        Assert.Equal(-0.4, trace.Last(p => p.T < 2.9).Out, 3);
    }

    [Fact]
    public void Position_StepsAndStays_ClampedToTriggerRange()
    {
        var axis = new KnobAxis(new KnobSettings
        {
            Mode = KnobMode.Position, Axis = GamepadAxis.RightTrigger, StepPerTick = 0.3,
        });
        axis.Tick(+1, 0); axis.Tick(+1, 0.1);
        Assert.Equal(0.6, axis.Update(5), 6);   // stays put
        axis.Tick(+1, 5); axis.Tick(+1, 5.1);
        Assert.Equal(1.0, axis.Update(6));
        for (var i = 0; i < 5; i++) axis.Tick(-1, 7);
        Assert.Equal(0.0, axis.Update(8));     // trigger floor is 0, not -1
    }

    [Fact]
    public void Position_OnAStick_GoesNegative()
    {
        var axis = new KnobAxis(new KnobSettings
        {
            Mode = KnobMode.Position, Axis = GamepadAxis.LeftX, StepPerTick = 0.5,
        });
        for (var i = 0; i < 3; i++) axis.Tick(-1, i);
        Assert.Equal(-1.0, axis.Update(5));
    }
}
