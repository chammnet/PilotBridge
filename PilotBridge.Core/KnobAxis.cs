namespace PilotBridge.Core;

/// <summary>
/// Turns knob ticks into a smooth axis value (Rate and Position modes).
/// Times are in seconds on any monotonic clock.
/// </summary>
/// <remarks>
/// Rate mode estimates speed from the time between ticks rather than counting
/// ticks in a window: steady turning then gives a steady value at any speed
/// instead of a jump on every tick. Once a tick is overdue the estimate falls
/// as 1/(time since last tick), so stopping glides down instead of cutting
/// out. Ramp limits then cap how fast the output itself may change.
/// </remarks>
internal sealed class KnobAxis
{
    private readonly KnobSettings _s;
    private readonly List<double> _ticks = new();
    private int _direction;
    private double _output;
    private double? _lastUpdate;

    public KnobAxis(KnobSettings settings) => _s = settings;

    public double Output => _output;

    public void Tick(int direction, double t)
    {
        if (_s.Mode == KnobMode.Position)
        {
            var step = direction * _s.StepPerTick * (_s.Invert ? -1 : 1);
            var min = _s.Axis is GamepadAxis.LeftTrigger or GamepadAxis.RightTrigger ? 0 : -1;
            _output = Math.Clamp(_output + step, min, 1);
            return;
        }

        // A reversal, or a tick after the run already stopped, starts a new
        // run: the first tick of a run moves at the slowest speed.
        var stopped = _ticks.Count == 0 || t - _ticks[^1] > 1 / _s.MinTicksPerSecond;
        if (direction != _direction || stopped)
        {
            _ticks.Clear();
            _direction = direction;
        }
        _ticks.Add(t);
        var keep = Math.Max(1, _s.AverageIntervals) + 1;
        if (_ticks.Count > keep)
            _ticks.RemoveRange(0, _ticks.Count - keep);
    }

    /// <summary>Ticks per second, signed, as estimated at time t.</summary>
    internal double RateAt(double t)
    {
        if (_ticks.Count == 0)
            return 0;

        double rate;
        if (_ticks.Count >= 2)
            rate = (_ticks.Count - 1) / (_ticks[^1] - _ticks[0]);
        else if (_s.IgnoreSingleTicks)
            return 0;
        else
            rate = _s.MinTicksPerSecond;

        // Overdue: the next tick would have arrived by now at this speed.
        var since = t - _ticks[^1];
        if (since > 0)
            rate = Math.Min(rate, 1 / since);

        if (rate < _s.MinTicksPerSecond)
        {
            _ticks.Clear();
            return 0;
        }
        return _direction * rate;
    }

    public double Update(double t)
    {
        if (_s.Mode == KnobMode.Position)
            return _output;

        var dt = _lastUpdate is { } last ? Math.Max(0, t - last) : 0;
        _lastUpdate = t;

        var rate = RateAt(t);
        var target = Math.Sign(rate)
            * StickMapper.Shape(Math.Abs(rate) / _s.FullScaleTicksPerSecond, _s.Curve, _s.AntiDeadzone)
            * (_s.Invert ? -1 : 1);

        _output = Slew(_output, target, dt);
        return _output;
    }

    /// <summary>
    /// Move toward target, at RampUp while growing in the same direction and
    /// RampDown otherwise. A reversal first comes back to 0 at RampDown.
    /// </summary>
    private double Slew(double current, double target, double dt)
    {
        if (current == target)
            return current;

        var crossing = current != 0 && target != 0 && Math.Sign(current) != Math.Sign(target);
        if (crossing)
            target = 0;

        var growing = Math.Abs(target) > Math.Abs(current);
        var maxStep = (growing ? _s.RampUpPerSecond : _s.RampDownPerSecond) * dt;
        var delta = Math.Clamp(target - current, -maxStep, maxStep);
        return current + delta;
    }
}

/// <summary>Knob Buttons mode: each tick becomes one tap of a button.</summary>
internal sealed class KnobButtons
{
    // Cap the backlog so a fast spin doesn't keep tapping long after it stops.
    private const int MaxQueued = 10;

    private readonly KnobSettings _s;
    private readonly Queue<GamepadButton> _queue = new();
    private GamepadButton _current;
    private double _phaseEnd = double.NegativeInfinity;
    private bool _holding;

    public KnobButtons(KnobSettings settings) => _s = settings;

    public void Tick(int direction)
    {
        var button = direction > 0 ? _s.Clockwise : _s.CounterClockwise;
        if (button != GamepadButton.None && _queue.Count < MaxQueued)
            _queue.Enqueue(button);
    }

    /// <summary>Buttons held at time t: hold for one pulse, release for one pulse.</summary>
    public GamepadButton Update(double t)
    {
        if (t < _phaseEnd)
            return _current;

        var pulse = _s.PulseMilliseconds / 1000.0;
        if (_holding)
        {
            // Always release between taps, or the game sees one long press.
            _holding = false;
            _current = GamepadButton.None;
            _phaseEnd = t + pulse;
        }
        else if (_queue.Count > 0)
        {
            _holding = true;
            _current = _queue.Dequeue();
            _phaseEnd = t + pulse;
        }
        return _current;
    }
}
