using System.Text.Json;
using System.Text.Json.Serialization;

namespace PilotBridge.Core;

/// <summary>
/// One game's mapping, loaded from a JSONC file (JSON with // comments) in
/// profiles\. Every setting has a default, so a profile only needs to name
/// what it changes. Profiles are meant to be shared: personal knob-feel
/// calibration does not belong here. Help text for each setting lives in
/// settings-help.jsonc, not in profiles.
/// </summary>
public sealed class Profile
{
    /// <summary>Bump when the format changes in a way older code can't read.</summary>
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public string Name { get; set; } = "Unnamed";
    public string Game { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public StickSettings Stick { get; set; } = new();
    public Dictionary<PilotButton, GamepadButton> Buttons { get; set; } = new();
    public Dictionary<PilotKnob, KnobSettings> Knobs { get; set; } = new();

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Profile Load(string path) => Parse(File.ReadAllText(path));

    public static Profile Parse(string json)
    {
        var profile = JsonSerializer.Deserialize<Profile>(json, Options)
            ?? throw new InvalidDataException("Profile is empty.");
        if (profile.FormatVersion > CurrentFormatVersion)
            throw new InvalidDataException(
                $"Profile format {profile.FormatVersion} is newer than this version of PilotBridge understands ({CurrentFormatVersion}). Update PilotBridge.");
        return profile;
    }
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

    // Rate mode. Defaults come from replaying recorded turning (2026-10-25):
    // Craig's slow / medium / fast were about 1.6 / 4.7 / 15 ticks per second.
    // Which of these a GUI exposes is still to be decided.
    /// <summary>Turning this fast (ticks per second) gives full deflection.</summary>
    public double FullScaleTicksPerSecond { get; set; } = 15;
    /// <summary>Slower than this counts as stopped.</summary>
    public double MinTicksPerSecond { get; set; } = 0.75;
    /// <summary>Speed is averaged over this many recent gaps between ticks.</summary>
    public int AverageIntervals { get; set; } = 4;
    /// <summary>Keep the speed until a tick is this many average gaps late, then stop.</summary>
    public double GraceFactor { get; set; } = 2.5;
    /// <summary>Output smoothing time constant; 0 = none.</summary>
    public double SmoothingSeconds { get; set; } = 0.25;
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
