using PilotBridge.Core;

namespace PilotBridge.Core.Tests;

public class ProfileTests
{
    [Fact]
    public void Parse_ReadsNamesCaseInsensitively_WithCommentsAndDefaults()
    {
        var p = Profile.Parse("""
            {
              // comments are allowed, for notes in hand-edited profiles
              "name": "Test",
              "stick": { "x": { "axis": "RightY", "invert": true }, "deadzone": 0.12 },
              "buttons": { "Side1": "A", "DpadUp": "DPadUp" },
              "knobs": {
                "OuterRing": { "mode": "Rate", "axis": "Triggers", "rampDownPerSecond": 1.5 },
                "TopKnob": { "mode": "Buttons", "clockwise": "RightShoulder", "counterClockwise": "LeftShoulder" },
              },
            }
            """);

        Assert.Equal("Test", p.Name);
        Assert.Equal(GamepadAxis.RightY, p.Stick.X.Axis);
        Assert.True(p.Stick.X.Invert);
        Assert.Equal(GamepadAxis.LeftY, p.Stick.Y.Axis);   // default kept
        Assert.Equal(0.12, p.Stick.Deadzone);
        Assert.Equal(GamepadButton.A, p.Buttons[PilotButton.Side1]);
        Assert.Equal(1.5, p.Knobs[PilotKnob.OuterRing].RampDownPerSecond);
        Assert.Equal(4.0, p.Knobs[PilotKnob.OuterRing].RampUpPerSecond);   // default kept
        Assert.Equal(KnobMode.Buttons, p.Knobs[PilotKnob.TopKnob].Mode);
    }

    [Fact]
    public void Parse_UnknownControlName_Fails()
    {
        Assert.ThrowsAny<Exception>(() => Profile.Parse("""{ "buttons": { "Side9": "A" } }"""));
    }

    [Fact]
    public void ShippedRoadcraftProfile_Loads()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "profiles", "roadcraft.json");
        var p = Profile.Load(path);
        Assert.Equal("Roadcraft", p.Name);
    }
}
