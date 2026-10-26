namespace PilotBridge.Core;

/// <summary>Xbox 360 pad outputs a mapping can drive.</summary>
public enum GamepadAxis
{
    None,
    LeftX,
    LeftY,
    RightX,
    RightY,
    LeftTrigger,
    RightTrigger,
    /// <summary>Both triggers as one axis: positive drives RT, negative LT.</summary>
    Triggers,
}

[Flags]
public enum GamepadButton
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LeftShoulder = 1 << 4,
    RightShoulder = 1 << 5,
    Back = 1 << 6,
    Start = 1 << 7,
    Guide = 1 << 8,
    LeftThumb = 1 << 9,
    RightThumb = 1 << 10,
    DPadUp = 1 << 11,
    DPadDown = 1 << 12,
    DPadLeft = 1 << 13,
    DPadRight = 1 << 14,
}

/// <summary>
/// Sticks are -1..+1 (+Y = up, as XInput), triggers 0..1.
/// </summary>
public sealed record GamepadState(
    double LeftX, double LeftY, double RightX, double RightY,
    double LeftTrigger, double RightTrigger, GamepadButton Buttons)
{
    public static readonly GamepadState Neutral = new(0, 0, 0, 0, 0, 0, GamepadButton.None);
}

/// <summary>
/// Collects contributions from every mapping for one update. Several sources
/// may drive the same axis; they add, then clamp.
/// </summary>
internal sealed class GamepadBuilder
{
    private double _lx, _ly, _rx, _ry, _lt, _rt;
    private GamepadButton _buttons;

    public void Add(GamepadAxis axis, double value)
    {
        switch (axis)
        {
            case GamepadAxis.LeftX: _lx += value; break;
            case GamepadAxis.LeftY: _ly += value; break;
            case GamepadAxis.RightX: _rx += value; break;
            case GamepadAxis.RightY: _ry += value; break;
            case GamepadAxis.LeftTrigger: _lt += value; break;
            case GamepadAxis.RightTrigger: _rt += value; break;
            case GamepadAxis.Triggers:
                if (value > 0) _rt += value; else _lt -= value;
                break;
        }
    }

    public void Press(GamepadButton buttons) => _buttons |= buttons;

    public GamepadState Build() => new(
        Stick(_lx), Stick(_ly), Stick(_rx), Stick(_ry),
        Trigger(_lt), Trigger(_rt), _buttons);

    private static double Stick(double v) => Math.Clamp(v, -1, 1);
    private static double Trigger(double v) => Math.Clamp(v, 0, 1);
}
