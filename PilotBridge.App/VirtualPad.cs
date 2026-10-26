using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using PilotBridge.Core;

namespace PilotBridge.App;

/// <summary>A virtual Xbox 360 pad on ViGEmBus.</summary>
internal sealed class VirtualPad : IDisposable
{
    private static readonly (GamepadButton Ours, Xbox360Button Theirs)[] ButtonMap =
    [
        (GamepadButton.A, Xbox360Button.A),
        (GamepadButton.B, Xbox360Button.B),
        (GamepadButton.X, Xbox360Button.X),
        (GamepadButton.Y, Xbox360Button.Y),
        (GamepadButton.LeftShoulder, Xbox360Button.LeftShoulder),
        (GamepadButton.RightShoulder, Xbox360Button.RightShoulder),
        (GamepadButton.Back, Xbox360Button.Back),
        (GamepadButton.Start, Xbox360Button.Start),
        (GamepadButton.Guide, Xbox360Button.Guide),
        (GamepadButton.LeftThumb, Xbox360Button.LeftThumb),
        (GamepadButton.RightThumb, Xbox360Button.RightThumb),
        (GamepadButton.DPadUp, Xbox360Button.Up),
        (GamepadButton.DPadDown, Xbox360Button.Down),
        (GamepadButton.DPadLeft, Xbox360Button.Left),
        (GamepadButton.DPadRight, Xbox360Button.Right),
    ];

    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _pad;

    public VirtualPad()
    {
        // Throws if ViGEmBus isn't installed.
        _client = new ViGEmClient();
        _pad = _client.CreateXbox360Controller();
        _pad.AutoSubmitReport = false;
        _pad.Connect();
    }

    public void Send(GamepadState s)
    {
        _pad.SetAxisValue(Xbox360Axis.LeftThumbX, Stick(s.LeftX));
        _pad.SetAxisValue(Xbox360Axis.LeftThumbY, Stick(s.LeftY));
        _pad.SetAxisValue(Xbox360Axis.RightThumbX, Stick(s.RightX));
        _pad.SetAxisValue(Xbox360Axis.RightThumbY, Stick(s.RightY));
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, Trigger(s.LeftTrigger));
        _pad.SetSliderValue(Xbox360Slider.RightTrigger, Trigger(s.RightTrigger));
        foreach (var (ours, theirs) in ButtonMap)
            _pad.SetButtonState(theirs, s.Buttons.HasFlag(ours));
        _pad.SubmitReport();
    }

    private static short Stick(double v) => (short)Math.Round(Math.Clamp(v, -1, 1) * 32767);
    private static byte Trigger(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);

    public void Dispose()
    {
        try { _pad.Disconnect(); } catch (Exception) { /* bus already gone */ }
        _client.Dispose();
    }
}
