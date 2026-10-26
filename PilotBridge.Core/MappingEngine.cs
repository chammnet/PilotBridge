namespace PilotBridge.Core;

/// <summary>
/// Applies a profile: feed it device reports as they arrive, call Update on a
/// steady clock, and send the returned state to the virtual pad. Update must
/// run even when no reports arrive, because knob axes keep changing.
/// Not thread-safe: call both from one thread.
/// </summary>
public sealed class MappingEngine
{
    private readonly Profile _profile;
    private readonly Dictionary<PilotKnob, KnobAxis> _axes = new();
    private readonly Dictionary<PilotKnob, KnobButtons> _knobButtons = new();
    private short _stickX, _stickY;
    private PilotButton _pressed;

    public MappingEngine(Profile profile)
    {
        _profile = profile;
        foreach (var (knob, s) in profile.Knobs)
        {
            if (s.Mode == KnobMode.Buttons)
                _knobButtons[knob] = new KnobButtons(s);
            else
                _axes[knob] = new KnobAxis(s);
        }
    }

    public void Handle(PilotReport report, double t)
    {
        switch (report)
        {
            case StickReport s:
                _stickX = s.X;
                _stickY = s.Y;
                break;
            case ButtonReport b:
                _pressed = b.Pressed;
                break;
            case KnobTick k when _axes.TryGetValue(k.Knob, out var axis):
                axis.Tick(k.Direction, t);
                break;
            case KnobTick k when _knobButtons.TryGetValue(k.Knob, out var buttons):
                buttons.Tick(k.Direction);
                break;
        }
    }

    public GamepadState Update(double t)
    {
        var pad = new GamepadBuilder();

        StickMapper.Apply(_profile.Stick, _stickX, _stickY, pad);

        foreach (var (source, target) in _profile.Buttons)
            if (_pressed.HasFlag(source))
                pad.Press(target);

        foreach (var (knob, axis) in _axes)
            pad.Add(_profile.Knobs[knob].Axis, axis.Update(t));

        foreach (var buttons in _knobButtons.Values)
            pad.Press(buttons.Update(t));

        return pad.Build();
    }
}
