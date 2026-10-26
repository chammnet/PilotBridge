using System.Text.Json;
using System.Text.Json.Serialization;

namespace PilotBridge.Core;

/// <summary>
/// One game's mapping, loaded from a JSON file in profiles\. Every setting has
/// a default, so a profile only needs to name what it changes.
/// </summary>
public sealed class Profile
{
    public string Name { get; set; } = "Unnamed";
    public StickSettings Stick { get; set; } = new();
    public Dictionary<PilotButton, GamepadButton> Buttons { get; set; } = new();
    public Dictionary<PilotKnob, KnobSettings> Knobs { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Profile Load(string path) => Parse(File.ReadAllText(path));

    public static Profile Parse(string json) =>
        JsonSerializer.Deserialize<Profile>(json, Options)
        ?? throw new InvalidDataException("Profile is empty.");
}

public sealed class StickSettings
{
    public AxisTarget X { get; set; } = new() { Axis = GamepadAxis.LeftX };
    public AxisTarget Y { get; set; } = new() { Axis = GamepadAxis.LeftY };

    /// <summary>Radial dead zone, 0..1 of full deflection.</summary>
    public double Deadzone { get; set; } = 0.08;

    /// <summary>Response curve exponent: 1 = linear, above 1 = finer near center.</summary>
    public double Curve { get; set; } = 1.0;

    /// <summary>Smallest non-zero output, to step over the game's own dead zone.</summary>
    public double AntiDeadzone { get; set; } = 0.0;
}

public sealed class AxisTarget
{
    public GamepadAxis Axis { get; set; } = GamepadAxis.None;
    public bool Invert { get; set; }
}

public enum KnobMode
{
    /// <summary>Turning speed drives the axis; it glides back to 0 when you stop.</summary>
    Rate,
    /// <summary>Each tick moves the axis one step and it stays there.</summary>
    Position,
    /// <summary>Each tick taps a button.</summary>
    Buttons,
}

public sealed class KnobSettings
{
    public KnobMode Mode { get; set; } = KnobMode.Rate;
    public GamepadAxis Axis { get; set; } = GamepadAxis.None;
    public bool Invert { get; set; }

    // Rate mode
    /// <summary>Turning this fast (ticks per second) gives full deflection.</summary>
    public double FullScaleTicksPerSecond { get; set; } = 10;
    /// <summary>Slower than this counts as stopped.</summary>
    public double MinTicksPerSecond { get; set; } = 0.75;
    /// <summary>Speed is averaged over this many recent tick intervals.</summary>
    public int AverageIntervals { get; set; } = 2;
    /// <summary>Wait for a second tick before moving (filters stray ticks).</summary>
    public bool IgnoreSingleTicks { get; set; }
    public double Curve { get; set; } = 1.0;
    public double AntiDeadzone { get; set; } = 0.0;
    /// <summary>Max output change per second while speeding up.</summary>
    public double RampUpPerSecond { get; set; } = 4.0;
    /// <summary>Max output change per second while slowing down or stopping.</summary>
    public double RampDownPerSecond { get; set; } = 2.0;

    // Position mode
    public double StepPerTick { get; set; } = 0.02;

    // Buttons mode
    public GamepadButton Clockwise { get; set; }
    public GamepadButton CounterClockwise { get; set; }
    /// <summary>How long each tap is held; the gap between taps is the same.</summary>
    public double PulseMilliseconds { get; set; } = 40;
}
