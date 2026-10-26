using PilotBridge.Core;

namespace PilotBridge.Core.Tests;

public class MappingEngineTests
{
    [Fact]
    public void Stick_InsideDeadzone_IsZero()
    {
        var engine = new MappingEngine(new Profile { Stick = { Deadzone = 0.1 } });
        engine.Handle(new StickReport(30, -30), 0);   // radius 42/511 = 0.083
        Assert.Equal(GamepadState.Neutral, engine.Update(0));
    }

    [Fact]
    public void Stick_FullDeflection_IsFullScale_AndKeepsDirection()
    {
        var engine = new MappingEngine(new Profile());
        engine.Handle(new StickReport(511, 0), 0);
        Assert.Equal(1.0, engine.Update(0).LeftX, 6);

        engine.Handle(new StickReport(361, 361), 0);   // diagonal at the rim
        var s = engine.Update(0);
        Assert.Equal(s.LeftX, s.LeftY, 6);
        Assert.Equal(Math.Sqrt(0.5), s.LeftX, 2);
    }

    [Fact]
    public void Stick_AxesCanGoAnywhere_IncludingTheOtherStick()
    {
        // Craig's example: up/down -> left stick Y, left/right -> right stick Y.
        var engine = new MappingEngine(new Profile
        {
            Stick =
            {
                Deadzone = 0,
                X = new AxisTarget { Axis = GamepadAxis.RightY },
                Y = new AxisTarget { Axis = GamepadAxis.LeftY, Invert = true },
            },
        });
        engine.Handle(new StickReport(511, 0), 0);
        var right = engine.Update(0);
        Assert.Equal(1.0, right.RightY, 6);
        Assert.Equal(0.0, right.LeftX);

        engine.Handle(new StickReport(0, 511), 0);
        Assert.Equal(-1.0, engine.Update(0).LeftY, 6);
    }

    [Fact]
    public void Buttons_MapThroughTheProfile_AndRelease()
    {
        var engine = new MappingEngine(new Profile
        {
            Buttons =
            {
                [PilotButton.Side1] = GamepadButton.A,
                [PilotButton.DpadUp] = GamepadButton.DPadUp,
            },
        });
        engine.Handle(new ButtonReport(PilotButton.Side1 | PilotButton.DpadUp | PilotButton.Side2), 0);
        Assert.Equal(GamepadButton.A | GamepadButton.DPadUp, engine.Update(0).Buttons);   // Side2 unmapped

        engine.Handle(new ButtonReport(PilotButton.None), 0);
        Assert.Equal(GamepadButton.None, engine.Update(0).Buttons);
    }

    [Fact]
    public void KnobOnTriggers_ClockwiseIsRT_CounterClockwiseIsLT()
    {
        var settings = new KnobSettings { Mode = KnobMode.Position, Axis = GamepadAxis.Triggers, StepPerTick = 0.25 };
        var engine = new MappingEngine(new Profile { Knobs = { [PilotKnob.DialRing] = settings } });

        engine.Handle(new KnobTick(PilotKnob.DialRing, +1), 0);
        var cw = engine.Update(0);
        Assert.Equal((0.0, 0.25), (cw.LeftTrigger, cw.RightTrigger));

        for (var i = 0; i < 3; i++) engine.Handle(new KnobTick(PilotKnob.DialRing, -1), 0);
        var ccw = engine.Update(0);
        Assert.Equal((0.5, 0.0), (ccw.LeftTrigger, ccw.RightTrigger));
    }

    [Fact]
    public void SourcesOnTheSameAxis_AddAndClamp()
    {
        var engine = new MappingEngine(new Profile
        {
            Stick = { Deadzone = 0 },
            Knobs = { [PilotKnob.LargeKnob] = new KnobSettings { Mode = KnobMode.Position, Axis = GamepadAxis.LeftX, StepPerTick = 0.5 } },
        });
        engine.Handle(new StickReport(511, 0), 0);
        engine.Handle(new KnobTick(PilotKnob.LargeKnob, +1), 0);
        Assert.Equal(1.0, engine.Update(0).LeftX);
    }

    [Fact]
    public void KnobButtons_EachTickIsOneSeparateTap()
    {
        var settings = new KnobSettings
        {
            Mode = KnobMode.Buttons, Clockwise = GamepadButton.RightShoulder,
            CounterClockwise = GamepadButton.LeftShoulder, PulseMilliseconds = 40,
        };
        var engine = new MappingEngine(new Profile { Knobs = { [PilotKnob.SmallKnob] = settings } });
        engine.Handle(new KnobTick(PilotKnob.SmallKnob, +1), 0);
        engine.Handle(new KnobTick(PilotKnob.SmallKnob, +1), 0);
        engine.Handle(new KnobTick(PilotKnob.SmallKnob, -1), 0);

        // Sample every 4 ms and count rising edges per button.
        var presses = new Dictionary<GamepadButton, int>();
        var prev = GamepadButton.None;
        for (var t = 0.0; t < 0.5; t += 0.004)
        {
            var b = engine.Update(t).Buttons;
            foreach (var btn in new[] { GamepadButton.RightShoulder, GamepadButton.LeftShoulder })
                if (b.HasFlag(btn) && !prev.HasFlag(btn))
                    presses[btn] = presses.GetValueOrDefault(btn) + 1;
            prev = b;
        }
        Assert.Equal(2, presses[GamepadButton.RightShoulder]);
        Assert.Equal(1, presses[GamepadButton.LeftShoulder]);
        Assert.Equal(GamepadButton.None, prev);
    }
}
